# Configuração e Execução do ERP Adapter (Conectando ao Painel ICP)

Este documento descreve como configurar, publicar e executar o **`Versatus.ForcaVendas.ErpAdapter`** em um ambiente local (servidor on-premise do cliente) integrado ao **Painel ICP** (onde o Frontend e a API estão publicados).

---

## 🗺️ Índice

* [1. Visão Geral do Fluxo](#1-visão-geral-do-fluxo)
* [2. Pré-requisitos do Ambiente Local](#2-pré-requisitos-do-ambiente-local)
* [3. Configuração do Arquivo `appsettings.Production.json`](#3-configuração-do-arquivo-appsettingsproductionjson)
* [4. Compilação e Publicação Local](#4-compilação-e-publicação-local)
* [5. Como Executar o Adaptador](#5-como-executar-o-adaptador)
  * [5.1. Execução Manual via Terminal](#51-execução-manual-via-terminal)
  * [5.2. Execução como Serviço do Windows (Recomendado)](#52-execução-como-serviço-do-windows-recomendado)
* [6. Validação do Funcionamento](#6-validação-do-funcionamento)
* [7. Solução de Problemas](#7-solução-de-problemas)

---

## 1. Visão Geral do Fluxo

O **ERP Adapter** é uma aplicação .NET do tipo *Worker Service* (serviço de segundo plano) desenvolvida para rodar **dentro da infraestrutura local do cliente**. 

Ela atua como uma ponte de comunicação:
1. **Origem (Local):** Conecta-se diretamente ao banco de dados SQL Server do ERP legado (`versatus`).
2. **Destino (Nuvem/ICP):** Conecta-se via **SFTP seguro (porta 2022)** ao servidor **SFTPGo** instalado no Painel ICP.

```
┌─────────────────────────────────────────┐               ┌─────────────────────────────────────────┐
│        INFRAESTRUTURA LOCAL (ERP)       │               │        PAINEL ICP (NUVEM / VPS)         │
│                                         │               │                                         │
│   ┌────────────┐        ┌────────────┐  │               │  ┌──────────┐   FTP/SFTP   ┌──────────┐  │
│   │ SQL Server │ <====> │ ERP Adapter│  │ <===========  │  │  SFTPGo  │ <==========> │   API    │  │
│   │ (versatus) │        │  (.NET 8)  │  │  (Porta 2022) │  │ (Serviço)│              │ (.NET 8) │  │
│   └────────────┘        └────────────┘  │               │  └──────────┘              └──────────┘  │
└─────────────────────────────────────────┘               └─────────────────────────────────────────┘
```

* **Exportação (Catálogo):** A cada 5 minutos, o adaptador lê os clientes, produtos, preços e condições de pagamento do SQL Server, gera arquivos JSON e faz o upload para o SFTPGo no ICP.
* **Importação (Pedidos):** A cada 10 segundos, o adaptador baixa os novos pedidos de venda gerados pelo aplicativo que estão na pasta `/pedidos/pendentes` do SFTPGo e os insere no SQL Server (tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA`).

---

## 2. Pré-requisitos do Ambiente Local

Para que o ERP Adapter rode corretamente na máquina/servidor local, certifique-se de possuir:

1. **.NET 8 Runtime ou SDK** instalado no servidor local ([Download do .NET 8](https://dotnet.microsoft.com/pt-br/download/dotnet/8.0)).
2. **Acesso ao SQL Server do ERP**: String de conexão com usuário que possua permissões de `SELECT` nas views de catálogo e `INSERT/UPDATE` nas tabelas `MOBVENDA*` (Consulte o guia [INTEGRACAO_LEGADO_ERP.md](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/docs/INTEGRACAO_LEGADO_ERP.md) para o script de permissões e criação da coluna `CODIGOINTEGRACAO`).
3. **Credenciais do SFTPGo no Painel ICP**:
   * **Host:** `vps9526.panel.icontainer.net` (ou o IP/Domínio da sua VPS).
   * **Porta SFTP:** `2022` (Esta é a porta configurada no painel ICP para acesso SFTP seguro).
   * **Usuário:** `versatus` (ou o usuário de integração criado no SFTPGo).
   * **Senha:** A senha do usuário de integração definida na etapa de instalação da API (consulte o [DEPLOY_ICP_API.md](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/docs/DEPLOY_ICP_API.md#33-instalar-o-sftpgo-servidor-ftpsftp)).

---

## 3. Configuração do Arquivo `appsettings.Production.json`

Na pasta do projeto local do adaptador (`src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/`), edite ou crie o arquivo **`appsettings.Production.json`**. Ele deve conter as configurações apontando para o banco local e para o servidor SFTP do ICP.

### Exemplo de Configuração (`appsettings.Production.json`):
```json
{
  "ConnectionStrings": {
    "ErpDatabase": "Server=NOME_DO_SERVIDOR_SQL\\SQLEXPRESS;Database=versatus;User Id=USUARIO_SQL;Password=SENHA_SQL;TrustServerCertificate=True;"
  },
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "vps9526.panel.icontainer.net",
      "Port": 2022,
      "UseSftp": true,
      "Username": "versatus",
      "Password": "SUA_SENHA_SFTPGO_DEFINIDA_NO_ICP",
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
    "UseSimulatedCatalog": false,
    "Tenants": {
      "00000000-0000-0000-0000-000000000001": {
        "FilialId": 1,
        "FullSyncHour": 3
      }
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting.Lifetime": "Information",
      "Versatus.ForcaVendas": "Information"
    }
  }
}
```

### Detalhes das Chaves de Configuração:
* **`ConnectionStrings.ErpDatabase`**: Aponta para a instância SQL Server do ERP legado na rede local.
* **`Integration.Ftp.Host`**: O endereço público do Painel ICP (`vps9526.panel.icontainer.net`).
* **`Integration.Ftp.Port`**: A porta externa do SFTPGo configurada no ICP (obrigatoriamente `2022`).
* **`Integration.Ftp.UseSftp`**: Deve ser `true` para garantir que o tráfego seja criptografado por SSH (SFTP).
* **`ErpAdapter.Tenants.<UUID>.FilialId`**: Identifica qual código de filial (`IDGLOFILIAL`) no seu ERP corresponde ao Tenant da aplicação.

---

## 4. Compilação e Publicação Local

Para implantar a aplicação no servidor do cliente, gere o pacote de publicação compilado:

1. Abra o terminal (PowerShell) e navegue até a pasta do adaptador:
   ```powershell
   cd "c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\erp-adapter\Versatus.ForcaVendas.ErpAdapter"
   ```

2. Execute o comando de compilação e publicação em modo Release:
   ```powershell
   dotnet publish -c Release -o ./publish
   ```

Toda a aplicação compilada, incluindo o executável `Versatus.ForcaVendas.ErpAdapter.exe` (no Windows) ou `.dll` (no Linux/Windows) e o arquivo `appsettings.Production.json` estarão localizados na pasta `./publish/`.

---

## 5. Como Executar o Adaptador

### 5.1. Execução Manual via Terminal
Útil para testar a primeira conexão e acompanhar a saída dos logs diretamente na tela.

1. No terminal do servidor local, navegue até a pasta de publicação:
   ```powershell
   cd ./publish
   ```

2. Defina a variável de ambiente para carregar as configurações de produção (`appsettings.Production.json`):
   * **No Windows (PowerShell):**
     ```powershell
     $env:DOTNET_ENVIRONMENT="Production"
     .\Versatus.ForcaVendas.ErpAdapter.exe
     ```
   * **No Windows (Prompt CMD):**
     ```cmd
     set DOTNET_ENVIRONMENT=Production
     Versatus.ForcaVendas.ErpAdapter.exe
     ```
   * **No Linux (Bash):**
     ```bash
     export DOTNET_ENVIRONMENT=Production
     dotnet Versatus.ForcaVendas.ErpAdapter.dll
     ```

### 5.2. Execução como Serviço do Windows (Recomendado)
Para ambientes de produção, a aplicação deve rodar como um serviço do Windows para iniciar automaticamente junto com o sistema operacional e continuar rodando sem depender de um usuário logado.

1. Abra o **PowerShell como Administrador**.
2. Registre o executável como um serviço utilizando o utilitário do Windows `sc.exe`:
   ```powershell
   sc.exe create "VersatusErpAdapter" binPath= "C:\Caminho\Para\Sua\Pasta\publish\Versatus.ForcaVendas.ErpAdapter.exe --contentRoot C:\Caminho\Para\Sua\Pasta\publish" start= auto
   ```
   > [!IMPORTANT]
   > Certifique-se de incluir o parâmetro `--contentRoot` apontando para a pasta onde os arquivos `appsettings*.json` estão localizados, garantindo que o serviço consiga ler as configurações ao iniciar.
   > 
   > Observe o espaço obrigatório após o caractere `=` (ex: `binPath= "..."` e `start= auto`).

3. Configure a descrição do serviço para identificação futura:
   ```powershell
   sc.exe description "VersatusErpAdapter" "Serviço de integração local do Versatus Force Sales MVP com o ERP."
   ```

4. Inicie o serviço:
   ```powershell
   sc.exe start "VersatusErpAdapter"
   ```

---

## 6. Validação do Funcionamento

Após iniciar o serviço, verifique se tudo está funcionando conforme o esperado:

1. **Checar os logs locais**:
   * Se rodou via console, você deve ver mensagens como:
     ```text
     info: Versatus.ForcaVendas.ErpAdapter.Jobs.CatalogExporter[0]
           Starting catalog export job for tenant 00000000-0000-0000-0000-000000000001
     info: Versatus.ForcaVendas.ErpAdapter.Jobs.CatalogExporter[0]
           Catalog exported successfully for tenant 00000000-0000-0000-0000-000000000001
     info: Versatus.ForcaVendas.ErpAdapter.Jobs.OrderImporter[0]
           Checking for pending orders in SFTP for tenant 00000000-0000-0000-0000-000000000001
     ```
   * Se rodando como serviço do Windows, os logs são gravados diretamente no **Visualizador de Eventos** do Windows (Event Viewer -> *Aplicativos*).

2. **Verificar os arquivos no Servidor SFTP**:
   * Conecte-se ao SFTPGo via cliente SFTP (como FileZilla) usando o Host `vps9526.panel.icontainer.net:2022`.
   * Verifique se dentro de `/integration-sync/{TenantId}/catalogo/` foram criados os arquivos:
     * `clientes.json`
     * `produtos.json`
     * `tabelas-preco.json`
     * `condicoes-pagamento.json`
   * Se os arquivos existirem com data recente, a exportação do catálogo está operando com sucesso!

---

## 7. Solução de Problemas

### Erro: `System.Net.Sockets.SocketException: Connection refused` ou tempo limite esgotado
* **Causa:** O ERP Adapter não conseguiu alcançar o servidor SFTPGo no IP/porta especificado.
* **O que verificar:**
  1. Verifique se o endereço `vps9526.panel.icontainer.net` e a porta `2022` estão corretos no seu `appsettings.Production.json`.
  2. Verifique se o firewall da sua rede local permite conexões de **saída** na porta `2022`.
  3. Confirme se o serviço **SFTPGo** está ativo e rodando normalmente no painel ICP.

### Erro: `Microsoft.Data.SqlClient.SqlException: A connection was successfully established with the server, but then an error occurred during the login process`
* **Causa:** Problema de autenticação ou permissão de rede com o SQL Server local.
* **O que verificar:**
  1. Certifique-se de que a string de conexão está correta e que o usuário/senha do SQL Server está correto.
  2. Se a conexão exigir criptografia, garanta que o parâmetro `TrustServerCertificate=True` está incluído no final da connection string.
  3. Verifique se o protocolo TCP/IP está habilitado no *SQL Server Configuration Manager* do seu servidor SQL local.
