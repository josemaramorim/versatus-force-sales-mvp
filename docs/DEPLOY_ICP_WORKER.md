# Deploy do Worker na VPS (Painel ICP - icontainer) via GitHub

Este documento descreve como publicar e configurar o serviço **`Versatus.ForcaVendas.Worker`** utilizando a integração nativa do **Painel ICP** com o GitHub. O Worker executa em segundo plano na VPS, sendo o responsável por ler os catálogos enviados pelo ERP Adapter ao SFTPGo e importá-los para o cache do Redis, além de gerenciar o retorno de pedidos.

> [!NOTE]
> Este guia é complementar aos tutoriais da API e do Frontend, seguindo os mesmos padrões de infraestrutura da icontainer.

---

## 🗺️ Índice do Tutorial de Deploy

Selecione a etapa que deseja consultar ou siga o passo a passo na ordem sugerida:

### 🏁 Preparação e Fluxo
* [1. Visão Geral](#1-visão-geral) — O papel do Worker na arquitetura e portas.
* [2. Pré-requisitos](#2-pré-requisitos) — Validação local antes de subir para a nuvem.

### 🚀 Deploy Nível 1 — Fluxo Contínuo (Git + Auto-compilação)
* [3. Criar App .NET no ICP](#3-criar-app-net-no-icp) — Configuração do assistente para o Worker.
* [4. Variáveis de Ambiente](#4-variáveis-de-ambiente) — Chaves e credenciais de infraestrutura em CAIXA ALTA.
* [5. Configurações de Porta e Acesso Externo](#5-configurações-de-porta-e-acesso-externo) — Por que o Worker não expõe portas públicas.
  * [5.1. O Worker precisa de Domínio ou SSL próprio?](#51-esclarecimento-o-worker-precisa-de-domínio-próprio-ou-certificado-ssl) — Esclarecimento sobre segurança e tráfego de background.

### 🛡️ Diagnóstico e Monitoramento
* [6. Verificação do Funcionamento (Logs)](#6-verificação-do-funcionamento-logs) — Como checar se o Worker está importando os arquivos.
* [7. Resolução de Erros Comuns](#7-resolução-de-erros-comuns) — O que fazer se a conexão com banco ou Redis falhar.

### 🔌 Deploy Nível 2 — Método ZIP Local (Solução para conexões instáveis)
* [8. Por que usar o ZIP?](#8-por-que-usar-o-zip) — Vantagens da compilação local.
* [9. Compilar e Gerar o ZIP do Worker](#9-compilar-e-gerar-o-zip-do-worker) — Comandos rápidos em PowerShell.
* [10. Configurar o Worker via ZIP no ICP](#10-configurar-o-worker-via-zip-no-icp) — Passo a passo do formulário utilizando o ZIP.

---

## 1. Visão Geral

O **Worker** é uma aplicação .NET do tipo *Worker Service* que roda em segundo plano na VPS. Ele não possui interface gráfica nem endpoints HTTP. Sua função é puramente operacional:

* **Diretório do projeto:** `src/worker/Versatus.ForcaVendas.Worker`
* **Porta padrão:** Não utiliza (não expõe serviços web).
* **Fluxo de Integração:**
  1. Detecta novos arquivos de catálogo (`clientes.json`, `produtos.json`, etc.) na pasta do SFTPGo.
  2. Processa o conteúdo e realiza a carga no **Redis**, permitindo que a API responda instantaneamente ao aplicativo.
  3. Gerencia o fluxo assíncrono de pedidos e retornos.

---

## 2. Pré-requisitos

Antes de iniciar o deploy do Worker na VPS, garanta que:
* [ ] A API (`force-sales-api`) já esteja publicada e conectada ao PostgreSQL e Redis.
* [ ] O **SFTPGo** esteja instalado e operacional no Painel ICP (porta `2022`).
* [ ] O seu repositório GitHub esteja atualizado e conectado ao Painel ICP.

---

## 3. Criar App .NET no ICP

### 3.1. Configurar o repositório do Worker
1. Acesse o **Painel ICP** (`https://vps9526.panel.icontainer.net`).
2. Vá em **Aplicações** → **Nova Aplicação** → selecione o tipo **.NET** (ou **ASP.NET Core**).
3. Preencha as configurações do repositório exatamente como abaixo:

| Campo | Valor |
|---|---|
| **Repositório GitHub** | `josemaramorim/versatus-force-sales-mvp` |
| **Branch** | `develop` (homologação) ou `main` (produção) |
| **Diretório do projeto** | `src/worker/Versatus.ForcaVendas.Worker` |
| **Arquivo .csproj** | `Versatus.ForcaVendas.Worker.csproj` |
| **Versão do .NET** | `.NET 8` |

### 3.2. Configurar o comando de inicialização
Como o Worker é compilado em uma DLL independente, defina o comando de inicialização como:

```bash
dotnet Versatus.ForcaVendas.Worker.dll
```

---

## 4. Variáveis de Ambiente

O Worker precisa se conectar à mesma base de dados PostgreSQL, ao mesmo Redis e ao mesmo servidor SFTPGo da API. Adicione as seguintes variáveis de ambiente em **CAIXA ALTA** no painel ICP para o Worker:

```env
ASPNETCORE_ENVIRONMENT=Production

# Banco de dados PostgreSQL (Usar o mesmo container e credenciais da API)
CONNECTIONSTRINGS__DEFAULTCONNECTION=Host=postgresql-forca-venda;Port=5432;Database=forca_vendas;Username=versatus;Password=SUA_SENHA_POSTGRES

# Cache do Redis (Destino final do catálogo de produtos e clientes)
CONNECTIONSTRINGS__REDIS=redis-forca-venda:6379,password=SUA_SENHA_REDIS,abortConnect=false

# Transporte de integração (Ftp/SFTPGo)
INTEGRATION__TRANSPORT=Ftp

# SFTPGo — Credenciais de acesso interno da VPS
INTEGRATION__FTP__HOST=ftp-forca-venda
INTEGRATION__FTP__PORT=2022
INTEGRATION__FTP__USESFTP=true
INTEGRATION__FTP__USERNAME=versatus
INTEGRATION__FTP__PASSWORD=SUA_SENHA_SFTPGO_DEFINIDA_NO_ICP
INTEGRATION__FTP__BASEPATH=/integration-sync
INTEGRATION__FTP__CATALOGPOLLINTERVALSECONDS=300
INTEGRATION__FTP__RESULTPOLLINTERVALSECONDS=30

# O Worker não necessita da configuração AUTH__TENANTS, pois busca todos
# os tenants ativos de forma dinâmica diretamente do banco de dados (assinaturas).
```

---

## 5. Configurações de Porta e Acesso Externo

Como o Worker é um serviço de background que não recebe requisições externas, a configuração de rede deve ser restrita por segurança:

1. **Porta da Aplicação / Porta Externa:** Caso o painel exija obrigatoriamente preencher uma porta no formulário de criação, defina uma porta genérica que não esteja em uso (exemplo: **`8081`** ou **`5001`**).
2. **Acesso Externo:** **Desative** o switch de *Acesso Externo* (mantenha-o desligado). Isso garante que nenhuma porta seja aberta no firewall da VPS para este contêiner, aumentando a segurança.
3. **Domínio:** Não é necessário vincular nenhum domínio ou subdomínio para esta aplicação.

### 5.1. Esclarecimento: O Worker precisa de Domínio Próprio ou Certificado SSL?

> [!NOTE]
> **Resposta Curta: Não.**
> 
> Como o Worker é um serviço interno executado em segundo plano (*Background Service*), ele nunca recebe conexões externas vindas da internet ou de navegadores de usuários. Portanto:
> 
> * **Sem Necessidade de Domínio:** Ele não precisa de um endereço como `worker.versatusapp.com.br`. Ele se comunica internamente dentro da rede da VPS.
> * **Sem Necessidade de SSL próprio:** Como ele não expõe portas HTTP/HTTPS para fora, não há necessidade de gerar um certificado SSL (Let's Encrypt) para o Worker.
> * **Comunicação Segura Interna:** Ele se conecta ao banco de dados e ao Redis utilizando os hostnames internos da rede isolada do painel ICP (ex: `Host=postgresql-forca-venda` e `redis-forca-venda`), o que já é 100% seguro contra interceptações externas.
> * **Chamadas para a API**: O Worker utiliza internamente a URL da API para se comunicar quando necessário. Se a API estiver configurada com SSL (ex: `https://api.versatusapp.com.br`), o Worker fará chamadas seguras para a API de forma automática, validando o certificado de produção.

Clique em **Confirmar** ou **Criar** para iniciar o primeiro deploy do Worker.

---

## 6. Verificação do Funcionamento (Logs)

Após a conclusão da publicação do Worker na VPS, confirme se ele está rodando e processando os catálogos corretamente:

1. Na listagem de aplicações do painel ICP, localize o `force-sales-worker` e clique em **Visualizar** na coluna **Log**.
2. O log deve exibir mensagens indicando que o Worker iniciou e está monitorando o SFTPGo:

```text
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Production
info: Versatus.ForcaVendas.Worker.Jobs.CatalogSyncJob[0]
      Iniciando CatalogSyncJob com intervalo de 300 segundos.
info: Versatus.ForcaVendas.Worker.Jobs.CatalogSyncJob[0]
      Buscando arquivo de catálogo no SFTP para o tenant 00000000-0000-0000-0000-000000000001
info: Versatus.ForcaVendas.Worker.Jobs.CatalogSyncJob[0]
      Catálogo importado com sucesso para o Redis para o tenant 00000000-0000-0000-0000-000000000001. Clientes: 142, Produtos: 890
```

Se você visualizar mensagens de importação com sucesso, o fluxo completo de sincronização local -> nuvem está 100% operacional.

---

## 7. Resolução de Erros Comuns

### Erro: `Renci.SshNet.Common.SshAuthenticationException: Permission denied (password)` nos logs do Worker
* **Causa:** O Worker não conseguiu se conectar ao SFTPGo devido a credenciais inválidas.
* **Solução:** Confirme se a variável `INTEGRATION__FTP__PASSWORD` no painel ICP para o Worker foi preenchida com a mesma senha que a API e o ERP Adapter utilizam para se conectar ao SFTPGo.

### Erro: `StackExchange.Redis.RedisConnectionException: It was not possible to connect`
* **Causa:** O Worker não conseguiu acessar o servidor Redis interno da VPS.
* **Solução:** Verifique se o endereço do host do Redis (`redis-forca-venda` ou o nome do contêiner do Redis no seu painel) está correto e se a senha configurada na variável `CONNECTIONSTRINGS__REDIS` está correta.

### Erro de Compilação no Container: `MSB1003: Specify a project or solution file` (Projetos Não Encontrados)
Se ao tentar fazer o deploy do Worker no painel ICP o build falhar com erros de namespaces ausentes (como `Application` ou `Infrastructure` não encontrados) ou apresentar a seguinte mensagem de erro:
`MSBUILD : error MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file.`

* **Causa:** O Worker depende de projetos irmãos localizados na pasta `src/backend/` (como `Application` e `Infrastructure`). Se a **Pasta do projeto** estiver configurada como `/` (para que o painel monte o repositório completo), o compilador executará o build a partir da raiz. Como não existe um arquivo `.sln` ou `.csproj` na raiz, o MSBuild falha por não saber qual projeto deve ser compilado.
* **Solução (Ajustar caminhos de Build):**
  1. Acesse as configurações da aplicação do Worker no painel ICP.
  2. No campo **Pasta do projeto** (no topo), altere para a **raiz do repositório** (deixe em branco, ou coloque `/` ou `.`). Isso fará o painel copiar o repositório completo para o container de build.
  3. No campo **Comando de build** (logo abaixo do switch de compilação), altere para especificar o caminho completo do `.csproj` a partir da raiz, garantindo que o MSBuild saiba o que compilar:
     ```bash
     dotnet publish src/worker/Versatus.ForcaVendas.Worker/Versatus.ForcaVendas.Worker.csproj -c Release -o /app
     ```
  4. Salve e force uma nova compilação no painel.

---

## Método Alternativo — Publicação via Arquivo ZIP (Recomendado para conexões instáveis)

Caso ocorram instabilidades de rede ao baixar pacotes durante o build automático na VPS, você pode compilar o Worker no seu computador local e enviar o pacote compilado pronto.

### 8. Por que usar o ZIP?
* Elimina a compilação na VPS, reduzindo o consumo de memória durante o deploy.
* O build local é rápido e gera um arquivo muito pequeno (cerca de 5 MB).

### 9. Compilar e Gerar o ZIP do Worker
Execute os seguintes passos no terminal (PowerShell) do seu computador local:

1. Navegue até a pasta do worker:
   ```powershell
   cd "c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\worker\Versatus.ForcaVendas.Worker"
   ```

2. Execute o comando para compilar e gerar a pasta de publicação:
   ```powershell
   Remove-Item -Path ./publish -Recurse -ErrorAction SilentlyContinue
   dotnet publish Versatus.ForcaVendas.Worker.csproj -c Release -o ./publish
   ```

3. Compacte o conteúdo gerado em um arquivo ZIP chamado `publish-worker.zip`:
   ```powershell
   Compress-Archive -Path ./publish/* -DestinationPath ./publish-worker.zip -Force
   ```

O arquivo `publish-worker.zip` será gerado na pasta do projeto do Worker.

### 10. Configurar o Worker via ZIP no ICP
1. No Painel ICP, vá em **Aplicações** → **Nova Aplicação** (ou edite a existente).
2. Defina a versão do .NET como **8.0**.
3. Em **Arquivos do Projeto**, selecione a aba **Enviar arquivos**.
4. Faça o upload do arquivo `publish-worker.zip`.
5. Preencha as configurações básicas:
   * **Script de Execução:** `dotnet Versatus.ForcaVendas.Worker.dll`
   * **Porta da aplicação:** `8081` (desative o *Acesso Externo*)
6. Adicione as mesmas variáveis de ambiente em **CAIXA ALTA** especificadas na [Etapa 4](#4-variáveis-de-ambiente).
7. Clique em **Confirmar** para salvar e iniciar o serviço.
