# Gerenciamento de Serviços em Background (.NET / PowerShell)

Este documento descreve como gerenciar os processos e serviços em background da aplicação **Versatus Force Sales** rodando em ambiente Windows/PowerShell.

---

## 1. Os Serviços do Ecossistema

A aplicação é composta por três serviços principais em C# (.NET 8/10):
1.  **API Gateway (`Versatus.ForcaVendas.Api`)**: Interface principal que atende o frontend e gerencia as conexões de banco de dados e autenticação.
2.  **Worker (`Versatus.ForcaVendas.Worker`)**: Executa em background gerenciando o processamento assíncrono (RabbitMQ/FTP) e cache do Redis.
3.  **ERP Adapter (`Versatus.ForcaVendas.ErpAdapter`)**: Conecta o sistema local à base de dados legada do SQL Server da filial.

---

## 2. Comandos do PowerShell (Windows)

Ao trabalhar localmente, esses serviços podem bloquear arquivos DLL comuns (como `Versatus.ForcaVendas.Domain.dll`), impedindo novos builds se já estiverem em execução. Use os comandos abaixo no PowerShell para gerenciá-los:

### 2.1. Listar Serviços em Execução
Para checar quais processos da aplicação estão rodando no momento:

```powershell
Get-Process | Where-Object { $_.Name -like "*ForcaVendas*" }
```

### 2.2. Parar/Encerrar Todos os Serviços (Recomendado antes de novos builds)
Para encerrar todos os três serviços simultaneamente de forma forçada:

```powershell
Get-Process | Where-Object { $_.Name -like "*ForcaVendas*" } | Stop-Process -Force
```

### 2.3. Parar um Serviço Específico
Caso queira fechar apenas um dos processos:

*   **Parar apenas a API:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.Api" -Force
    ```
*   **Parar apenas o Worker:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.Worker" -Force
    ```
*   **Parar apenas o Adaptador ERP:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.ErpAdapter" -Force
    ```

---

## 3. Como Inicializar os Serviços

### 3.1. Modo Interativo (Terminal Aberto)
Execute na pasta raiz do projeto:

*   **API:** `dotnet run --project src/backend/Versatus.ForcaVendas.Api`
*   **Worker:** `dotnet run --project src/worker/Versatus.ForcaVendas.Worker`
*   **ERP Adapter:** `dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter`

### 3.2. Modo Background (Executar sem travar o terminal)
Se você deseja iniciar os serviços em segundo plano no PowerShell sem precisar manter três abas do terminal abertas:

*   **API:**
    ```powershell
    Start-Job -Name "FV-Api" -ScriptBlock { dotnet run --project src/backend/Versatus.ForcaVendas.Api }
    ```
*   **Worker:**
    ```powershell
    Start-Job -Name "FV-Worker" -ScriptBlock { dotnet run --project src/worker/Versatus.ForcaVendas.Worker }
    ```
*   **ERP Adapter:**
    ```powershell
    Start-Job -Name "FV-Adapter" -ScriptBlock { dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter }
    ```

*   **Para listar os jobs ativos:** `Get-Job`
*   **Para parar os jobs ativos:** `Get-Job | Stop-Job`
