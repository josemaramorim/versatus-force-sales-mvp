# Arquitetura de Deploy — O que Fica na VPS e o que Fica no Cliente

Este documento explica a decisão arquitetural de onde cada componente do **Versatus Force Sales** é instalado, considerando que o banco de dados SQL Server do ERP legado fica na **rede interna do cliente** e não é exposto para a internet.

---

## 🗺️ Índice de Arquitetura e Instalação

Selecione o tópico que deseja consultar ou acompanhe a documentação na ordem proposta:

### 📐 Visão Arquitetural
* [1. O Problema Central](#1-o-problema-central) — O motivo da divisão e a responsabilidade de cada ambiente (VPS vs Cliente).
* [2. Diagrama Completo](#2-diagrama-completo-da-arquitetura) — Representação visual do fluxo de dados e conectividade.
* [3. Distribuição de Componentes](#3-onde-cada-componente-é-instalado) — Detalhamento de onde cada serviço é executado:
  * [3.1. Componentes na VPS](#31-componentes-que-ficam-na-vps) — Nginx, API, Frontend, PostgreSQL, Redis, RabbitMQ e FTP.
  * [3.2. Componentes no Cliente](#32-componente-que-fica-na-máquina-do-cliente) — O ERP Adapter local.

### 🔄 Fluxo de Integração e Comunicação
* [4. Fluxo Completo de Dados](#4-fluxo-completo-de-dados) — Como os dados trafegam de ponta a ponta:
  * [4.1. Exportação do Catálogo](#41-exportação-do-catálogo-do-erp-para-o-aplicativo) — Envio do catálogo do ERP para o app.
  * [4.2. Recebimento de Pedidos](#42-recebimento-de-pedidos-do-aplicativo-para-o-erp) — Gravação de pedidos no ERP.
  * [4.3. Retorno do Faturamento](#43-retorno-do-faturamento-do-erp-para-o-aplicativo) — Atualização do status dos pedidos.

### 🔒 Segurança e Instalação
* [5. Requisitos de Firewall](#5-requisitos-de-firewall) — Portas e regras de segurança necessárias:
  * [5.1. Firewall do Cliente](#51-firewall-do-cliente-rede-interna) — Conexões de saída (outbound).
  * [5.2. Firewall da VPS](#52-firewall-da-vps) — Portas públicas e privadas recomendadas.
* [6. Instalação do ERP Adapter no Cliente](#6-instalação-do-erpadapter-na-máquina-do-cliente) — Passo a passo para o ambiente local:
  * [6.1. Pré-requisitos](#61-pré-requisitos-na-máquina-do-cliente) — Requisitos básicos do sistema.
  * [6.2. Obtenção de Arquivos](#62-obter-os-arquivos-do-erpadapter) — Compilação e publicação.
  * [6.3. Configuração do appsettings.json](#63-configurar-o-arquivo-appsettingsjson) — Ajuste de variáveis obrigatórias.
  * [6.4. Teste de Execução Manual](#64-testar-a-execução-manual) — Validação rápida em console.
  * [6.5. Instalação como Serviço Windows](#65-registrar-o-erpadapter-como-serviço-do-windows) — Execução persistente em background.
  * [6.6. Verificação de Logs](#66-verificar-os-logs-no-windows) — Diagnóstico pelo Visualizador de Eventos.

### 📋 Resumo e Checklists
* [7. Resumo Visual Rápido](#7-resumo-visual-rápido) — Diagrama e regras simples de memorização.
* [8. Checklist de Instalação Completa](#8-checklist-de-instalação-completa) — Lista de tarefas finais para a VPS e o Cliente.

---

## 1. O Problema Central

O ERP Adapter precisa se conectar ao SQL Server do cliente para ler o catálogo de clientes, produtos e preços, e para gravar os pedidos recebidos. Como o SQL Server fica na rede interna da empresa (sem acesso externo), **o ERP Adapter não pode rodar na VPS** — ele não conseguiria alcançar o banco de dados.

A solução é dividir a instalação em dois ambientes:

| Ambiente | Responsabilidade |
|---|---|
| **VPS (nuvem)** | Toda a plataforma web — API, frontend, banco de dados da aplicação, cache, filas |
| **Máquina do cliente (LAN)** | Apenas o ERP Adapter — que faz a ponte entre o ERP legado e a VPS via FTP |

---

## 2. Diagrama Completo da Arquitetura

```
┌────────────────────────────────────────────────────────────────────┐
│                     REDE INTERNA DO CLIENTE                        │
│                                                                    │
│  ┌─────────────────────┐          ┌──────────────────────────────┐ │
│  │   SQL Server (ERP)  │◄────────►│      ErpAdapter .NET         │ │
│  │   porta 1433 (LAN)  │  lê e    │  (roda em qualquer máquina   │ │
│  │   banco "versatus"  │  escreve │   Windows/Linux da empresa)  │ │
│  └─────────────────────┘          └──────────────┬───────────────┘ │
│                                                  │                 │
└──────────────────────────────────────────────────┼─────────────────┘
                                                   │
                              FTP (porta 21 ou 22) │ conexão SAINDO
                              JSON de catálogos    │ da rede do cliente
                              JSON de pedidos      │ (geralmente não
                              JSON de resultados   │ bloqueada por firewall)
                                                   │
┌──────────────────────────────────────────────────▼─────────────────┐
│                            VPS (nuvem)                             │
│                                                                    │
│  ┌──────────────────┐  ┌────────────────┐  ┌───────────────────┐  │
│  │  Nginx (público) │  │  API .NET      │  │  Worker .NET      │  │
│  │  porta 80 / 443  │  │  porta 5000    │  │  (background)     │  │
│  └────────┬─────────┘  └───────┬────────┘  └────────┬──────────┘  │
│           │                    │                     │             │
│           ▼                    ▼                     ▼             │
│  ┌──────────────────┐  ┌──────────────┐  ┌─────────────────────┐  │
│  │ Frontend Next.js │  │  PostgreSQL  │  │  Redis   RabbitMQ   │  │
│  │  porta 3000      │  │  porta 5432  │  │  :6379   :5672      │  │
│  └──────────────────┘  └──────────────┘  └─────────────────────┘  │
│                                                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │  FTP (Docker)  porta 21  ◄── recebe arquivos do ErpAdapter   │  │
│  │  /integration-sync/{tenantId}/catalogo/                      │  │
│  │  /integration-sync/{tenantId}/pedidos/                       │  │
│  │  /integration-sync/{tenantId}/resultados/                    │  │
│  └──────────────────────────────────────────────────────────────┘  │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘
                          │
                  internet (HTTPS 443)
                          │
                          ▼
            Vendedores acessam via browser
            (smartphone, tablet, notebook)
```

---

## 3. Onde Cada Componente é Instalado

### 3.1. Componentes que ficam na VPS

| Componente | Porta | Como roda | Descrição |
|---|---|---|---|
| **Nginx** | 80, 443 (públicas) | systemd | Proxy reverso e terminação SSL |
| **Frontend Next.js** | 3000 (interna) | systemd | Interface web dos vendedores |
| **API .NET** | 5000 (interna) | systemd | Gateway da aplicação |
| **Worker .NET** | — (background) | systemd | Processa filas e sincroniza catálogo |
| **PostgreSQL** | 5432 (interna) | Docker | Banco de dados da aplicação Force Sales |
| **Redis** | 6379 (interna) | Docker | Cache de catálogo por tenant |
| **RabbitMQ** | 5672, 15672 (interna) | Docker | Fila de mensagens |
| **FTP** | 21 (pública) | Docker | Ponto de troca de arquivos com o cliente |

> [!IMPORTANT]
> Somente as portas **80**, **443** e **21** são expostas para a internet. Todo o resto (PostgreSQL, Redis, RabbitMQ, API, Frontend) fica acessível **apenas internamente** dentro da VPS, protegido pelo Nginx.

---

### 3.2. Componente que fica na máquina do cliente

| Componente | Onde | Descrição |
|---|---|---|
| **ErpAdapter .NET** | Qualquer máquina na rede interna do cliente | Lê o SQL Server e troca arquivos com a VPS via FTP |

O ErpAdapter é o **único componente** instalado fora da VPS. Ele roda como um serviço em background e não precisa de interface gráfica, servidor web, Docker ou qualquer outra infraestrutura.

---

## 4. Fluxo Completo de Dados

### 4.1. Exportação do Catálogo (do ERP para o aplicativo)

```
1. ErpAdapter acorda a cada 5 minutos (configurável)
2. Conecta ao SQL Server local (porta 1433, dentro da LAN)
3. Executa queries nas views VWCLIENTE, VWRITEMESTOQUE, VENTABELAPRECOESTOQUE, GLOCONDICAOPAGAMENTO
4. Gera arquivos JSON com os dados (clientes.json, produtos.json, tabelas-preco.json, condicoes-pagamento.json)
5. Conecta ao FTP da VPS (porta 21, saindo da rede do cliente para a internet)
6. Envia os arquivos JSON para /integration-sync/{tenantId}/catalogo/
7. Worker na VPS detecta os arquivos novos no FTP
8. Worker carrega os dados no Redis (cache)
9. Vendedor abre o aplicativo → API busca do Redis → catálogo aparece instantaneamente
```

### 4.2. Recebimento de Pedidos (do aplicativo para o ERP)

```
1. Vendedor fecha um pedido no aplicativo
2. API salva o pedido no PostgreSQL e publica evento no RabbitMQ
3. Worker consome o evento do RabbitMQ
4. Worker gera um arquivo JSON do pedido e salva no FTP: /integration-sync/{tenantId}/pedidos/pendentes/
5. ErpAdapter no cliente acorda a cada 10 segundos
6. Conecta ao FTP da VPS e verifica se há pedidos novos em /pedidos/pendentes/
7. Baixa o arquivo JSON do pedido
8. Conecta ao SQL Server local e grava nas tabelas MOBVENDA, MOBVENDAITEM, MOBVENDAPARCELA
9. Move o arquivo de /pendentes/ para /concluidos/ no FTP (confirmação atômica)
```

### 4.3. Retorno do Faturamento (do ERP para o aplicativo)

```
1. ERP legado processa o pedido e seta PROCESSADA = 1 e IDVENDOCUMENTO na MOBVENDA
2. ErpAdapter detecta registros com PROCESSADA = 1 e EXPORTADA = 0
3. Gera arquivo JSON com o resultado e o número do documento gerado
4. Envia para /integration-sync/{tenantId}/resultados/pendentes/ no FTP da VPS
5. Worker na VPS detecta o arquivo de resultado
6. Atualiza o status do pedido no PostgreSQL para "processado"
7. Vendedor atualiza a tela do aplicativo → status aparece como "faturado"
8. ErpAdapter marca EXPORTADA = 1 na MOBVENDA (evita reprocessar)
```

---

## 5. Requisitos de Firewall

### 5.1. Firewall do cliente (rede interna)

Não é necessário abrir nenhuma porta de entrada. O ErpAdapter apenas faz conexões **saindo** da rede:

| Conexão | Direção | Porta | Destino | Por quê |
|---|---|---|---|---|
| SQL Server | Saindo da máquina | 1433 | LAN interna | Acessa o banco do ERP |
| FTP | Saindo da rede | 21 | IP da VPS | Troca arquivos com a plataforma |
| FTP (passivo) | Saindo da rede | 21000–21010 | IP da VPS | Transferência de dados FTP passivo |

> [!NOTE]
> Conexões de saída (outbound) raramente são bloqueadas por firewalls corporativos. Em geral, não é necessária nenhuma configuração especial no firewall do cliente.

### 5.2. Firewall da VPS

Abrir apenas as portas necessárias para o público externo:

```bash
sudo ufw allow 22    # SSH (administração)
sudo ufw allow 80    # HTTP (Nginx)
sudo ufw allow 443   # HTTPS (Nginx + SSL)
sudo ufw allow 21    # FTP (ErpAdapter do cliente se conecta aqui)
sudo ufw enable
```

> [!WARNING]
> **Não abrir** as portas 5432 (PostgreSQL), 6379 (Redis), 5672 (RabbitMQ), 3000 (Frontend) ou 5000 (API) diretamente para a internet. Esses serviços ficam acessíveis apenas internamente na VPS.

---

## 6. Instalação do ErpAdapter na Máquina do Cliente

### 6.1. Pré-requisitos na máquina do cliente

- **Sistema operacional:** Windows 10/11 ou Windows Server 2016+ (ou Linux)
- **.NET 8 Runtime** instalado ([download](https://dotnet.microsoft.com/download/dotnet/8.0))
- Acesso de rede ao SQL Server (porta 1433 na LAN)
- Acesso à internet para se conectar ao FTP na VPS (porta 21 saindo)

> [!NOTE]
> Não é necessário instalar Docker, Node.js, Nginx, ou qualquer outra dependência. O ErpAdapter é um executável .NET autossuficiente.

### 6.2. Obter os arquivos do ErpAdapter

**Opção A — Compilar a partir do código-fonte** (requer .NET 8 SDK):

```bash
# Na máquina de desenvolvimento ou na própria máquina do cliente
dotnet publish src/erp-adapter/Versatus.ForcaVendas.ErpAdapter \
  --configuration Release \
  --output C:\ForcaVendas\erp-adapter
```

**Opção B — Copiar os arquivos publicados** da pasta `publish/erp-adapter` gerada durante o deploy da VPS para uma pasta na máquina do cliente (ex: via pen drive, rede compartilhada ou download).

### 6.3. Configurar o arquivo `appsettings.json`

Edite o arquivo `appsettings.json` na pasta do ErpAdapter com os dados do ambiente do cliente:

```json
{
  "ConnectionStrings": {
    "ErpDatabase": "Server=NOME_DO_SERVIDOR\\SQLEXPRESS2008;Database=versatus;User Id=sa;Password=SENHA_DO_BANCO;TrustServerCertificate=True;"
  },
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "vps6755.panel.icontainer.net",
      "Port": 21,
      "UseSftp": false,
      "Username": "test",
      "Password": "test",
      "BasePath": "/integration-sync",
      "CatalogPollIntervalSeconds": 300,
      "ResultPollIntervalSeconds": 30
    }
  },
  "Auth": {
    "Tenants": [
      "00000000-0000-0000-0000-000000000001"
    ]
  },
  "ErpAdapter": {
    "CatalogExportIntervalSeconds": 300,
    "OrderImportIntervalSeconds": 10,
    "Tenants": {
      "00000000-0000-0000-0000-000000000001": {
        "FilialId": 1,
        "FullSyncHour": 3,
        "TabelaPrecoIdDefault": 1,
        "PermiteAlterarTabelaPreco": true
      }
    }
  }
}
```

**Valores a ajustar obrigatoriamente:**

| Campo | O que colocar |
|---|---|
| `ConnectionStrings.ErpDatabase` → `Server=` | Nome ou IP do servidor SQL Server na rede interna |
| `ConnectionStrings.ErpDatabase` → `Password=` | Senha do usuário SQL Server |
| `Integration.Ftp.Host` | Endereço da VPS (ex: `vps6755.panel.icontainer.net`) |
| `Auth.Tenants` | UUID do tenant para integração no ERP Adapter (configuração opcional na API) |
| `ErpAdapter.Tenants.<UUID>.FilialId` | ID da filial (`IDGLOFILIAL`) no banco do ERP |

### 6.4. Testar a execução manual

Antes de registrar como serviço, teste a execução manualmente:

```bash
# Windows (PowerShell ou CMD na pasta do ErpAdapter)
dotnet Versatus.ForcaVendas.ErpAdapter.dll

# Linux
dotnet /opt/erp-adapter/Versatus.ForcaVendas.ErpAdapter.dll
```

Verifique nos logs que aparecem mensagens como:
```
Iniciando CatalogExporter com intervalo de 300 segundos.
Iniciando OrderImporter com intervalo de 10 segundos.
Exportando catálogo para o tenant ... Modo: Full (Carga Total).
Arquivos de catálogo enviados ao FTP com sucesso.
```

Se aparecer erro de conexão com o SQL Server, revise a string de conexão. Se aparecer erro no FTP, verifique se a porta 21 está acessível para o IP da VPS.

### 6.5. Registrar o ErpAdapter como serviço do Windows

Para que o ErpAdapter inicie automaticamente com o Windows (sem precisar manter um terminal aberto):

```powershell
# Abrir PowerShell como Administrador
# Criar o serviço Windows
sc.exe create "FVS-ErpAdapter" `
  binPath= "C:\ForcaVendas\erp-adapter\Versatus.ForcaVendas.ErpAdapter.exe" `
  start= auto `
  DisplayName= "Versatus Force Sales - ERP Adapter"

# Iniciar o serviço
sc.exe start "FVS-ErpAdapter"

# Verificar o status
sc.exe query "FVS-ErpAdapter"
```

Para parar ou reiniciar o serviço:

```powershell
sc.exe stop "FVS-ErpAdapter"
sc.exe start "FVS-ErpAdapter"
```

### 6.6. Verificar os logs no Windows

Os logs do ErpAdapter aparecem no **Visualizador de Eventos do Windows**:

1. Abra o menu Iniciar → pesquise por **Visualizador de Eventos**
2. Vá em **Logs do Windows** → **Aplicativo**
3. Filtre por **Origem**: `FVS-ErpAdapter`

Ou use o PowerShell para ver os últimos eventos em tempo real:

```powershell
Get-EventLog -LogName Application -Source "FVS-ErpAdapter" -Newest 20
```

---

## 7. Resumo Visual Rápido

```
CLIENTE (rede interna)          VPS (nuvem)
─────────────────────          ──────────────────────────────────────
SQL Server (ERP) ──┐           Nginx ──► Frontend / API
                   │           API .NET ──► PostgreSQL
ErpAdapter .NET ───┼──FTP──►   Worker .NET ──► Redis / RabbitMQ
                   │           FTP (Docker) ◄── arquivos de integração
VENDEDORES ────────┼──HTTPS──► Frontend (browser)
(smartphones,      │
 tablets,          │
 notebooks)        │
                   │
```

**Regra simples:**
- Tudo que é **acessado pelos vendedores via internet** → **VPS**
- O que **precisa acessar o SQL Server interno** → **Máquina do cliente**

---

## 8. Checklist de Instalação Completa

### Na VPS
- [ ] Ubuntu 24.04 instalado e atualizado
- [ ] Docker e Docker Compose instalados
- [ ] .NET 8 SDK instalado
- [ ] Node.js 20 instalado
- [ ] Repositório clonado em `/opt/versatus/`
- [ ] Arquivo `.env` configurado com senhas de produção
- [ ] `docker compose up -d` executado (PostgreSQL, Redis, RabbitMQ, FTP rodando)
- [ ] API, Worker e Frontend compilados e publicados
- [ ] Serviços `fvs-api`, `fvs-worker`, `fvs-frontend` registrados e rodando no systemd
- [ ] Nginx instalado e configurado como proxy reverso
- [ ] SSL Let's Encrypt configurado e renovação automática habilitada
- [ ] Firewall com portas 22, 80, 443, 21 abertas (apenas essas)

### Na máquina do cliente
- [ ] .NET 8 Runtime instalado
- [ ] Arquivos do ErpAdapter copiados para a máquina
- [ ] `appsettings.json` configurado com conexão SQL Server e endereço do FTP na VPS
- [ ] Execução manual testada com sucesso (catálogo enviado ao FTP)
- [ ] Serviço Windows `FVS-ErpAdapter` criado e iniciando automaticamente
- [ ] Coluna `CODIGOINTEGRACAO` verificada/criada na tabela `MOBVENDA` do SQL Server
