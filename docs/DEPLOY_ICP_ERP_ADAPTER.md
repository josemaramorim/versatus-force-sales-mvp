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
    "Tenants": {
      "00000000-0000-0000-0000-000000000001": {
        "FilialId": 1,
        "FullSyncHour": 3,
        "TabelaPrecoIdDefault": 1,
        "PermiteAlterarTabelaPreco": true
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

2. Gere o pacote de publicação em modo Release. Você tem duas opções de publicação:

   * **Opção A: Publicação Autossuficiente como Arquivo Único (Altamente Recomendado)**
     Esta opção embutirá o próprio runtime do .NET 10 e todas as dependências nativas (como o driver do SQL Server) dentro de um **único arquivo executável `.exe`**. Isso evita erros de arquivos ausentes (como a pasta `runtimes`) ao copiar os arquivos para o cliente!
     ```powershell
     dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o ./publish
     ```
     *Com esta opção, você só precisará copiar o arquivo `Versatus.ForcaVendas.ErpAdapter.exe` e os arquivos `appsettings*.json` para o servidor.*
   
   * **Opção B: Publicação Dependente do Framework**
     Esta opção gera múltiplos arquivos `.dll` mais leves, mas exige que o servidor do cliente tenha o runtime do .NET 10.0 (x64) pré-instalado.
     ```powershell
     dotnet publish -c Release -o ./publish
     ```

Toda a aplicação compilada e o arquivo `appsettings.Production.json` estarão localizados na pasta `./publish/`.

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

### Erro: `Renci.SshNet.Common.SshAuthenticationException: Permission denied (password)`
* **Causa:** O ERP Adapter local tentou se conectar ao servidor SFTPGo na VPS via SSH/SFTP, mas a senha fornecida para o usuário de integração foi rejeitada.
* **O que verificar:**
  1. No arquivo `appsettings.Production.json` do ERP Adapter local, certifique-se de que a chave `Integration.Ftp.Password` foi preenchida com a **senha real** do usuário `versatus` que você definiu no painel ICP (e não com o texto de exemplo `"SUA_SENHA_SFTPGO_DEFINIDA_NO_ICP"`).
  2. Confirme se o usuário `versatus` está devidamente cadastrado no **SFTPGo** no painel ICP e se ele possui permissão para acessar a pasta `/integration-sync`.
  3. Certifique-se de que não há espaços extras ou caracteres incorretos na senha no arquivo de configuração do ERP Adapter.

### Erro: `You must install .NET to run this application` ao iniciar o executável
* **Causa:** O computador/servidor local não possui o runtime do .NET 10 instalado para rodar a aplicação compilada de forma dependente do framework.
* **Como resolver:**
  1. **Recomendado (Publicação Autossuficiente)**: Recompile a aplicação no seu computador local usando a flag `--self-contained true` para embutir o runtime do .NET na própria pasta:
     ```powershell
     dotnet publish -c Release -r win-x64 --self-contained true -o ./publish
     ```
     Depois, limpe a pasta destino no servidor, copie os arquivos novos e inicie o `.bat`.
  2. **Alternativa (Instalação manual)**: Baixe e instale o **.NET 10.0 Runtime (x64)** no servidor do cliente a partir do site oficial da Microsoft: https://dotnet.microsoft.com/pt-br/download/dotnet/10.0 (escolha a opção *Run console apps - x64*).

