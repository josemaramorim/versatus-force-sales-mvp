# Especificação: Integração ERP via FTP/Google Drive + PWA Offline

> **Para IA**: Este arquivo contém TUDO que você precisa para implementar a integração entre a nova app Versatus Force Sales e o ERP legado. **Foco principal: FTP/SFTP**. RabbitMQ e Google Drive estão analisados mas são fase posterior. Leia integralmente antes de começar. Use as histórias e tarefas como guia de execução.

---

## Decisões Técnicas (ADR)

| # | Decisão | Justificativa | Data |
|---|---------|---------------|------|
| ADR-INT-01 | **Implementar FTP/SFTP primeiro** | Legado Small já usava FTP — maior familiaridade da equipe. RabbitMQ e Google Drive ficam analisados para fase posterior. | 2026-06-17 |
| ADR-INT-02 | **Formato JSON** (não XML) | O legado usava XML, mas a nova app padroniza JSON. O adaptador .NET 8 faz a conversão. | 2026-06-17 |
| ADR-INT-03 | **Adaptador ERP como Worker .NET 8** | Projeto separado (`erp-adapter`), roda no servidor do ERP. Não é script PowerShell. | 2026-06-17 |
| ADR-INT-04 | **Interface IIntegrationTransport** | Strategy Pattern para trocar FTP/RabbitMQ/GDrive via config, sem alterar código de negócio. | 2026-06-17 |

---

## Resumo do Problema

A nova app **Versatus Force Sales MVP** precisa trocar dados com o **ERP legado** (módulo `Gestao.Small`, .NET Framework 4.7) em dois sentidos:

| Direção | Dados | Frequência |
|---------|-------|------------|
| **ERP → App** (Exportação) | Clientes, Produtos, Tabelas de Preço, Condições de Pagamento | Periódica (a cada N minutos) |
| **App → ERP** (Importação) | Pedidos criados (cabeçalho + itens + parcelas) | Sob demanda (quando pedido é enviado) |
| **ERP → App** (Retorno) | Status de processamento (processado/erro + IdDocumentoVenda) | Periódica (polling) |

### Mecanismos de transporte (configuráveis por tenant)

1. **🟢 FTP/SFTP** — **IMPLEMENTAR AGORA** — espelha o mecanismo que o Small legado já usava
2. **🟡 RabbitMQ** — ANALISADO, implementar em fase posterior (eventos `pedido.enviado.v1` / `pedido.resultado.v1`)
3. **🟡 Google Drive** — ANALISADO, implementar em fase posterior (alternativa cloud)

O formato dos dados (**JSON**) é **o mesmo** para todos os transportes. O operador escolhe qual usar via `appsettings.json`.

### Além disso: modo offline (PWA)

A app é **UMA aplicação só** (Next.js). PWA adiciona capacidades de instalar no celular e funcionar offline. Não é um projeto separado.

---

## Arquitetura

```
┌──────────────────────────────────────────────────────────────────────┐
│                        Nova App (Backend API)                        │
│                                                                      │
│  CriarPedidoCommand ──► IIntegrationTransport.PublishOrderAsync()     │
│                              │                                       │
│                    ┌─────────┼─────────┐                             │
│                    ▼         ▼         ▼                             │
│              RabbitMQ    FTP/SFTP   GoogleDrive                      │
│            (provider)   (provider)  (provider)                       │
└──────────────────────────────────────────────────────────────────────┘
                    │         │         │
                    ▼         ▼         ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     Worker / Adaptador ERP                           │
│                                                                      │
│  IIntegrationTransport.FetchPendingResultsAsync() ◄── polling        │
│  IIntegrationTransport.FetchCatalogAsync()        ◄── cron sync      │
│                                                                      │
│  ┌────────────────────────────────────────┐                          │
│  │  ERP Legado (.NET Framework 4.7)       │                          │
│  │  GerarDocumentoVendaVersatus()         │                          │
│  │  SQL: MobCliente, MobEstoque, etc.     │                          │
│  └────────────────────────────────────────┘                          │
└──────────────────────────────────────────────────────────────────────┘
```

### Interface comum (Strategy Pattern)

```csharp
namespace Versatus.ForcaVendas.Infrastructure.Integration;

public interface IIntegrationTransport
{
    Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct);
    Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct);
    Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct);
    Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct);
}
```

Seleção via DI:
```csharp
var transport = config["Integration:Transport"]; // "RabbitMq" | "Ftp" | "GoogleDrive"
```

---

## Contratos JSON (Comum a Todos os Transportes)

### Catálogo: ERP → App

**clientes.json**:
```json
{
  "exportedAt": "2026-06-17T15:00:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "version": "v1",
  "data": [
    {
      "clienteIdERP": 1234,
      "nome": "Comércio XYZ Ltda",
      "documento": "12.345.678/0001-90",
      "areaVendaId": 5,
      "condicaoPagamentoIdDefault": 3,
      "comissionadoAreaVendaId": 7
    }
  ]
}
```

**produtos.json**:
```json
{
  "exportedAt": "2026-06-17T15:00:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "version": "v1",
  "data": [
    {
      "produtoIdERP": 101,
      "descricao": "Produto Exemplo 500ml",
      "siglaUnidadeVenda": "UN",
      "saldo": 150.0,
      "controlaEstoque": true,
      "controlaDescontoMaximo": true,
      "aceitaDesconto": true,
      "descontoMaximoPercentual": 15.0,
      "marca": "MarcaX",
      "fabricante": "FabricanteY"
    }
  ]
}
```

**tabelas-preco.json**:
```json
{
  "exportedAt": "2026-06-17T15:00:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "version": "v1",
  "data": [
    {
      "tabelaPrecoEstoqueIdERP": 501,
      "produtoIdERP": 101,
      "tabelaPrecoIdERP": 2,
      "valorUnitario": 25.50,
      "percentualDescontoMaximo": 10.0,
      "controlaDescontoMaximo": false
    }
  ]
}
```

**condicoes-pagamento.json**:
```json
{
  "exportedAt": "2026-06-17T15:00:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "version": "v1",
  "data": [
    {
      "condicaoPagtoIdERP": 3,
      "descricao": "3x sem juros",
      "quantidadeParcela": 3,
      "diasParcelamento": 30,
      "acrescimo": 0.0,
      "desconto": 0.0,
      "formaCobrancaIdERP": 1,
      "usarMesComercial": false
    }
  ]
}
```

### Pedidos: App → ERP

**pedido-{pedidoId}.json**:
```json
{
  "eventType": "pedido.enviado",
  "eventVersion": "v1",
  "eventId": "uuid-único",
  "createdAt": "2026-06-17T15:30:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "pedidoId": "uuid-do-pedido",
  "payload": {
    "clienteIdERP": 1234,
    "condicaoPagamentoIdERP": 3,
    "dataEmissao": "2026-06-17",
    "observacao": "Entrega urgente",
    "orcamento": false,
    "origem": "web",
    "valorTotal": 500.00,
    "valorTotalDesconto": 50.00,
    "valorTotalAcrescimo": 0.00,
    "valorFinal": 450.00,
    "valorFrete": 0.00,
    "itens": [
      {
        "produtoIdERP": 101,
        "tabelaPrecoEstoqueIdERP": 501,
        "siglaUnidade": "UN",
        "quantidade": 10.0,
        "valorUnitario": 25.50,
        "percentualDesconto": 5.0,
        "valorDesconto": 12.75,
        "percentualAcrescimo": 0.0,
        "valorAcrescimo": 0.00,
        "valorFinal": 242.25
      }
    ],
    "parcelas": [
      {
        "numero": 1,
        "formaCobrancaIdERP": 1,
        "valor": 150.00,
        "vencimento": "2026-07-17"
      }
    ]
  }
}
```

### Resultado: ERP → App

**resultado-{pedidoId}.json**:
```json
{
  "eventType": "pedido.resultado",
  "eventVersion": "v1",
  "eventId": "uuid-único",
  "createdAt": "2026-06-17T15:31:00Z",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "pedidoId": "uuid-do-pedido",
  "payload": {
    "resultado": "processado",
    "documentoVendaId": "NF-2026-00042",
    "motivoRejeicao": null,
    "sourceEventId": "uuid-do-evento-original"
  }
}
```

---

## Estrutura de Pastas (FTP e Google Drive)

```
/{tenantId}/
├── catalogo/                    ← ERP exporta aqui
│   ├── clientes.json
│   ├── produtos.json
│   ├── tabelas-preco.json
│   └── condicoes-pagamento.json
│
├── pedidos/                     ← App envia pedidos aqui
│   ├── pendentes/               ← Aguardando processamento
│   ├── processando/             ← Worker moveu, está processando
│   └── concluidos/              ← Após processamento
│
└── resultados/                  ← ERP deposita resultados aqui
    ├── pendentes/
    └── processados/
```

### Regras de idempotência
- Nomes de arquivo com UUID: `pedido-{pedidoId}.json`
- Padrão Move-then-Process: mover de `pendentes/` para `processando/` ANTES de processar
- Deduplicação via `EventoIntegracaoPedidoEntity` com chave `(tenantId, pedidoId, sourceEventId)`

---

## Configuração (appsettings.json)

```json
{
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "ftp.empresa.com.br",
      "Port": 21,
      "UseSftp": true,
      "Username": "versatus-sync",
      "Password": "{{vault:ftp-password}}",
      "BasePath": "/versatus-sync",
      "CatalogPollIntervalSeconds": 300,
      "ResultPollIntervalSeconds": 30
    },
    "GoogleDrive": {
      "ServiceAccountKeyPath": "/secrets/google-drive-key.json",
      "RootFolderId": "1aBcDeFgHiJkLmNoPqRsTuVwXyZ",
      "CatalogPollIntervalSeconds": 300,
      "ResultPollIntervalSeconds": 60
    },
    "RabbitMq": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "guest",
      "OrderExchange": "pedido.enviado.v1",
      "ResultQueue": "pedido.resultado.v1"
    }
  }
}
```

---

## Comparação das Estratégias

| Critério | RabbitMQ | FTP/SFTP | Google Drive |
|----------|----------|----------|-------------|
| **Latência** | ~100ms (push) | 10-30s (polling) | 30-60s (polling) |
| **Infraestrutura** | Servidor RabbitMQ | Servidor FTP/SFTP | Conta Google + API |
| **Custo** | Self-hosted | Self-hosted | Gratuito até 15GB |
| **Familiaridade ERP** | Baixa | **Alta** (legado usava) | Média |
| **Ideal para** | Produção final | Transição | Sem infra própria |

---

## PWA e Modo Offline

### Conceito: É UMA aplicação só

PWA **não é** um projeto separado. É a mesma app Next.js com capacidades extras:

```
SEM PWA (MVP):                      COM PWA (P1):
┌─────────────────────┐            ┌─────────────────────┐
│   Next.js/React     │            │   Next.js/React     │
│   Precisa internet  │            │ + Service Worker     │
│   Roda no browser   │            │ + IndexedDB (Dexie)  │
└─────────────────────┘            │ + manifest.json      │
                                   └─────────────────────┘
                                    Mesmo código, mesma URL
```

### Comportamento por dispositivo

| Dispositivo | Como acessa | Offline (P1) |
|---|---|---|
| PC escritório | Browser normal | ✅ Pedidos offline + sync |
| Celular | Ícone na tela inicial (PWA) | ✅ Pedidos offline + sync |
| Notebook campo | Browser ou PWA | ✅ Pedidos offline + sync |
| Tablet | Ícone na tela inicial (PWA) | ✅ Pedidos offline + sync |

### Fluxo offline

```
ONLINE: Vendedor → API Backend → PostgreSQL + Redis

PERDA DE CONEXÃO:
  Service Worker detecta offline → banner "Modo Offline"
  Catálogo servido do IndexedDB (Dexie.js)
  Pedidos salvos no IndexedDB com status "pendente_sync"

RECONEXÃO:
  Service Worker detecta online
  Para cada pedido "pendente_sync": POST /pedidos
  Se sucesso: marca "enviado", remove do IndexedDB
  Se erro: marca "erro_sync", notifica vendedor
  Atualiza catálogo do servidor
```

---

# HISTÓRIAS E TAREFAS

## Story INT-01 — Abstração de Transporte de Integração

**Título**: `[INT][Infra] Criar interface IIntegrationTransport e DTOs de contrato`

**Critérios de aceite**:
1. Interface `IIntegrationTransport` criada com métodos para publicar pedido, buscar resultados e buscar catálogo
2. DTOs de contrato (`OrderExportPayload`, `OrderResultPayload`, `CatalogSnapshot`) criados conforme JSON schemas acima
3. Extensão de DI `AddIntegrationTransport()` registra provider com base em `Integration:Transport` do appsettings
4. Testes unitários da abstração passando

**Tarefas**:

### T-INT-01: Criar interface e DTOs (1 dia)
- **Objetivo**: Definir o contrato comum de transporte
- **Arquivos a criar**:
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/IIntegrationTransport.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/OrderExportPayload.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/OrderResultPayload.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/CatalogSnapshot.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/ClienteCatalogDto.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/ProdutoCatalogDto.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/TabelaPrecoCatalogDto.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/CondicaoPagamentoCatalogDto.cs`
- **Implementação**: Ver contrato da interface na seção "Arquitetura" acima. DTOs espelham os JSONs.
- **Teste**: Compilação + serialização/deserialização JSON dos DTOs

### T-INT-02: Criar extensão de DI para seleção de provider (0.5 dia)
- **Objetivo**: Permitir trocar o transporte via configuração
- **Arquivo a criar**: `src/backend/Versatus.ForcaVendas.Api/Extensions/IntegrationExtensions.cs`
- **Implementação**:
  ```csharp
  public static IServiceCollection AddIntegrationTransport(
      this IServiceCollection services, IConfiguration config)
  {
      var transport = config.GetValue<string>("Integration:Transport") ?? "RabbitMq";
      return transport switch
      {
          "Ftp" => services.AddSingleton<IIntegrationTransport, FtpIntegrationTransport>()
                           .Configure<FtpTransportOptions>(config.GetSection("Integration:Ftp")),
          "GoogleDrive" => services.AddSingleton<IIntegrationTransport, GoogleDriveIntegrationTransport>()
                                   .Configure<GoogleDriveTransportOptions>(config.GetSection("Integration:GoogleDrive")),
          _ => services.AddSingleton<IIntegrationTransport, RabbitMqIntegrationTransport>()
                       .Configure<RabbitMqTransportOptions>(config.GetSection("Integration:RabbitMq")),
      };
  }
  ```
- **Arquivo a modificar**: `src/backend/Versatus.ForcaVendas.Api/Program.Partial.cs` — adicionar `builder.Services.AddIntegrationTransport(builder.Configuration);`

### T-INT-03: Integrar despacho no CriarPedidoCommand (0.5 dia)
- **Objetivo**: Após persistir pedido, publicar via transporte configurado
- **Arquivo a modificar**: `src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoCommand.cs`
- **Implementação**: Injetar `IIntegrationTransport`, após `SaveChangesAsync()` chamar `PublishOrderAsync()`, transicionar status para `enviado`
- **Cuidado**: Não falhar o pedido se a publicação falhar — marcar como `rascunho` e logar erro

### T-INT-04: Testes da abstração (0.5 dia)
- **Arquivo a criar**: `src/backend/Versatus.ForcaVendas.Api.Tests/IntegrationTransportTests.cs`
- **Testes**: serialização dos DTOs, DI resolve corretamente baseado em config, mock do transport funciona

---

## Story INT-02 — Provider FTP/SFTP

**Título**: `[INT][FTP] Implementar transporte de integração via FTP/SFTP`

**Critérios de aceite**:
1. Pedidos são enviados para `/tenantId/pedidos/pendentes/` no servidor FTP
2. Resultados são lidos de `/tenantId/resultados/pendentes/` e movidos para `/processados/`
3. Catálogo é lido de `/tenantId/catalogo/`
4. SFTP suportado como opção segura
5. Teste de integração com container Docker FTP passando

**Tarefas**:

### T-FTP-01: Implementar FtpIntegrationTransport (2 dias)
- **Arquivos a criar**:
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Ftp/FtpTransportOptions.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Ftp/FtpIntegrationTransport.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Ftp/FtpFolderStructure.cs`
- **Dependência NuGet**: `FluentFTP` (FTP/FTPS) ou `SSH.NET` (SFTP)
- **FtpTransportOptions**: Host, Port, UseSftp, Username, Password, BasePath, CatalogPollIntervalSeconds, ResultPollIntervalSeconds
- **FtpFolderStructure**: Helper que resolve paths como `/{basePath}/{tenantId}/pedidos/pendentes/`
- **PublishOrderAsync**: Serializar JSON → Upload para `/pedidos/pendentes/pedido-{id}.json`
- **FetchPendingResultsAsync**: Listar `/resultados/pendentes/` → Download cada → Parse → Retornar lista
- **AcknowledgeResultAsync**: Mover de `/resultados/pendentes/` para `/resultados/processados/`
- **FetchCatalogAsync**: Download de `clientes.json`, `produtos.json`, `tabelas-preco.json`, `condicoes-pagamento.json` → Parse → Retornar `CatalogSnapshot`

### T-FTP-02: Testes de integração FTP (1 dia)
- **Arquivo a criar**: `src/backend/Versatus.ForcaVendas.Api.Tests/FtpIntegrationTests.cs`
- **Setup**: Docker container `fauria/vsftpd` ou `stilliard/pure-ftpd`
- **Testes**: Upload pedido, download catálogo, listar resultados, mover arquivo

---

## Story INT-03 — Provider Google Drive 🟡 FASE POSTERIOR

> **STATUS: ANALISADO — NÃO IMPLEMENTAR AGORA**. A análise completa está documentada abaixo para quando for necessário.

**Título**: `[INT][GDrive] Implementar transporte de integração via Google Drive API`

**Critérios de aceite**:
1. Pedidos são enviados como arquivos JSON para pasta `pedidos/pendentes/` no Drive
2. Resultados são lidos e movidos entre pastas via API
3. Autenticação via Service Account funciona
4. Testes com mock da API passando

**Tarefas** (quando implementar):

### T-GDRIVE-01: Implementar GoogleDriveIntegrationTransport (2 dias)
- **Arquivos a criar**:
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/GoogleDrive/GoogleDriveTransportOptions.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/GoogleDrive/GoogleDriveIntegrationTransport.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/GoogleDrive/GoogleDriveFolderResolver.cs`
- **Dependências NuGet**: `Google.Apis.Drive.v3`, `Google.Apis.Auth`
- **GoogleDriveTransportOptions**: ServiceAccountKeyPath, RootFolderId, CatalogPollIntervalSeconds, ResultPollIntervalSeconds
- **GoogleDriveFolderResolver**: Cache de folder IDs por tenant (busca ou cria pastas na primeira vez)
- **Operações**: `Files.Create()` para upload, `Files.Get()` para download, `Files.Update()` para mover (alterar parentId), `Files.List()` para listar

### T-GDRIVE-02: Testes Google Drive (1 dia)
- **Arquivo a criar**: `src/backend/Versatus.ForcaVendas.Api.Tests/GoogleDriveIntegrationTests.cs`
- **Testes**: Mock do `DriveService`, validar upload/download/move

---

## Story INT-04 — Provider RabbitMQ 🟡 FASE POSTERIOR

> **STATUS: ANALISADO — NÃO IMPLEMENTAR AGORA**. A análise completa está documentada abaixo para quando for necessário.

**Título**: `[INT][RabbitMQ] Adaptar transporte RabbitMQ para interface IIntegrationTransport`

**Critérios de aceite**:
1. RabbitMQ publica pedidos no exchange `pedido.enviado.v1`
2. Consome resultados da queue `pedido.resultado.v1`
3. Usa o mesmo formato JSON dos outros providers

**Tarefas** (quando implementar):

### T-RABBIT-01: Implementar RabbitMqIntegrationTransport (1.5 dia)
- **Arquivos a criar**:
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/RabbitMq/RabbitMqTransportOptions.cs`
  - `src/backend/Versatus.ForcaVendas.Infrastructure/Integration/RabbitMq/RabbitMqIntegrationTransport.cs`
- **Dependência NuGet**: `RabbitMQ.Client`
- **Nota**: Este provider preenche a pasta `Messaging/` que hoje está vazia

### T-RABBIT-02: Testes RabbitMQ (0.5 dia)
- **Testes**: Mock do `IConnection`, validar publicação e consumo

---

## Story INT-05 — Worker com Jobs Reais

**Título**: `[INT][Worker] Substituir loop vazio por jobs de sync e polling`

**Critérios de aceite**:
1. Worker faz sync de catálogo periodicamente (configurável)
2. Worker faz polling de resultados periodicamente (configurável)
3. Idempotência respeitada na aplicação de resultados
4. Worker registra DI corretamente com provider configurado

**Tarefas**:

### T-WORKER-01: Refatorar Worker.cs e Program.cs (1 dia)
- **Arquivo a modificar**: `src/worker/Versatus.ForcaVendas.Worker/Worker.cs` — remover loop vazio
- **Arquivo a modificar**: `src/worker/Versatus.ForcaVendas.Worker/Program.cs` — registrar DI (DbContext, IIntegrationTransport, Redis)
- **Registrar**: `CatalogSyncJob` e `ResultPollingJob` como `IHostedService`

### T-WORKER-02: Implementar CatalogSyncJob (1.5 dia)
- **Arquivo a criar**: `src/worker/Versatus.ForcaVendas.Worker/Jobs/CatalogSyncJob.cs`
- **Lógica**: Loop com delay configurável → `FetchCatalogAsync()` → Upsert Redis (`catalogo:{tenantId}:clientes`, `:produtos`, `:precos`) + opcional PostgreSQL

### T-WORKER-03: Implementar ResultPollingJob (1.5 dia)
- **Arquivo a criar**: `src/worker/Versatus.ForcaVendas.Worker/Jobs/ResultPollingJob.cs`
- **Lógica**: Loop com delay configurável → `FetchPendingResultsAsync()` → Para cada resultado:
  1. Verificar idempotência em `EventoIntegracaoPedidoEntity` (chave `tenantId, pedidoId, sourceEventId`)
  2. Aplicar transição de status no `Pedido` (enviado → processado OU erro)
  3. `AcknowledgeResultAsync()` (mover arquivo/ack mensagem)

---

## Story INT-06 — Adaptador ERP (Lado Legado)

**Título**: `[INT][ERP] Criar adaptador que exporta catálogo e importa pedidos no ERP legado`

**Critérios de aceite**:
1. Catálogo exportado do SQL Server do ERP para pasta de sync (FTP/GDrive)
2. Pedidos lidos da pasta de sync e processados via `GerarDocumentoVendaVersatus()`
3. Resultado depositado na pasta de resultados

**Tarefas**:

### T-ERP-01: Criar projeto erp-adapter (0.5 dia)
- **Diretório a criar**: `src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/`
- **Tipo**: .NET 8 Worker Service
- **Dependências**: mesmo `IIntegrationTransport`, SQL Server client

### T-ERP-02: Implementar CatalogExporter (2 dias)
- **Arquivo a criar**: `src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/CatalogExporter.cs`
- **Lógica**: `SELECT * FROM MobCliente WHERE IdGloFilial = @filial` → serializar JSON → upload via `IIntegrationTransport` ou direto FTP/GDrive
- **Tabelas legadas**: `MobCliente`, `MobEstoque`, `MobTabelaPrecoEstoque`, `MobCondicaoPagamento`

### T-ERP-03: Implementar OrderImporter (2 dias)
- **Arquivo a criar**: `src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/OrderImporter.cs`
- **Lógica**: Polling pasta `pedidos/pendentes/` → Parse JSON → Converter para chamada ERP (`GerarDocumentoVendaVersatus` via SQL direto ou referência ao assembly legado) → Depositar resultado em `resultados/pendentes/`

### T-ERP-04: Teste E2E com ERP legado (1 dia)
- **Teste manual**: Criar pedido na nova app → Verificar arquivo em `pendentes/` → Processar no ERP → Verificar resultado → Status atualizado na app

---

## Story PWA-01 — PWA e Modo Offline

**Título**: `[PWA] Habilitar instalação e modo offline para vendedores de campo`

**Critérios de aceite**:
1. App pode ser instalada como PWA em celular/notebook (ícone na tela inicial)
2. Catálogo disponível offline via IndexedDB
3. Pedidos podem ser criados offline e sincronizados quando online
4. Indicador visual de modo offline
5. Conflitos de estoque tratados no retorno online

**Tarefas**:

### T-PWA-01: Configurar next-pwa + manifest.json (1 dia)
- **Arquivos a criar/modificar**:
  - `src/frontend/app/next.config.js` — integrar `next-pwa`
  - `src/frontend/app/public/manifest.json` — nome, ícones, cores, display: standalone
  - `src/frontend/app/public/icons/` — ícones 192x192 e 512x512
- **Dependência npm**: `next-pwa`
- **Resultado**: Browser oferece "Instalar" e app abre em tela cheia

### T-PWA-02: Cache do catálogo no IndexedDB ao login (2 dias)
- **Arquivos a criar**:
  - `src/frontend/app/src/lib/offlineDb.ts` — Schema Dexie.js com tabelas `clientes`, `produtos`, `precos`
  - `src/frontend/app/src/lib/catalogSync.ts` — Após login, buscar catálogo da API e salvar no Dexie
- **Dependência npm**: `dexie`
- **Lógica**: `POST /auth/login` sucesso → `GET /catalogo/clientes` + `GET /catalogo/produtos` → `db.clientes.bulkPut(data)` + `db.produtos.bulkPut(data)`

### T-PWA-03: Salvar pedidos localmente quando offline (2 dias)
- **Arquivo a criar**: `src/frontend/app/src/lib/offlineOrders.ts`
- **Lógica**: Detectar `navigator.onLine === false` → em vez de `POST /pedidos`, salvar no IndexedDB com status `"pendente_sync"` → exibir confirmação local
- **Arquivo a modificar**: `src/frontend/app/src/lib/vendaApi.ts` — wrapper que decide se envia online ou salva offline

### T-PWA-04: Fila de sincronização automática (2-3 dias)
- **Arquivo a criar**: `src/frontend/app/src/lib/syncQueue.ts`
- **Lógica**: Listener `window.addEventListener('online', ...)` → buscar pedidos com status `"pendente_sync"` do IndexedDB → `POST /pedidos` para cada → se sucesso: marcar `"enviado"` e remover → se erro: marcar `"erro_sync"` e notificar
- **Background Sync API** (se browser suportar): registrar sync event no Service Worker

### T-PWA-05: Service Worker para interceptar requests (1-2 dias)
- **Configuração**: `next-pwa` com estratégias de cache:
  - **Cache-first**: assets estáticos (JS, CSS, imagens), catálogo
  - **Network-first**: pedidos, autenticação, status
  - **Stale-while-revalidate**: dados de referência
- **Precache**: páginas principais (login, dashboard, novo pedido)

### T-PWA-06: Indicador visual de modo offline (1 dia)
- **Arquivo a criar**: `src/frontend/app/src/components/OfflineBanner.tsx`
- **Lógica**: Hook `useOnlineStatus()` → se offline, exibir banner fixo "📡 Modo Offline — pedidos serão sincronizados quando conectar"
- **Badge**: Exibir contagem de pedidos pendentes de sync na tela de pedidos
- **Arquivo a modificar**: `src/frontend/app/src/store/uiStore.ts` — adicionar estado `isOffline`, `pendingSyncCount`

### T-PWA-07: Tratamento de conflitos de estoque (1-2 dias)
- **Lógica**: Pedido criado offline pode ter produto sem estoque quando sincronizar. No sync:
  - Se `POST /pedidos` retorna 422 (validação) → marcar como `"erro_sync"` com mensagem do backend
  - Exibir na tela de pedidos com ícone ⚠️ e opção de editar/reenviar
- **Arquivo a modificar**: `src/frontend/app/src/lib/syncQueue.ts`

### T-PWA-08: Testes de PWA e offline (2 dias)
- **Testes manuais**: Chrome DevTools → Application → Service Workers → Offline checkbox
- **Testes automatizados**: Simular `navigator.onLine = false`, verificar que pedido salva no IndexedDB, simular reconexão e verificar sync

### Story PWA-02: Controle Híbrido de Estoque (P2 — 5-7 dias)

#### Contexto e Funcionamento:
Permite ativar/desativar as validações de estoque com base em uma flag global combinada às propriedades individuais dos produtos exportadas do ERP.

#### Parâmetro no Backend (`appsettings.json`):
```json
{
  "Sales": {
    "StockControlMode": "Strict" 
  }
}
```
*   `Strict`: Habilita a validação de estoque físico no sistema para produtos com `ControlaEstoque=true`.
*   `Disabled`: Desativa todas as validações de estoque de vendas.

#### Rota da API (`GET /api/sales/config`):
Expor a configuração operacional ativa do tenant para o frontend no login e em cache local:
```json
{
  "tenantId": "uuid",
  "stockControlMode": "Strict"
}
```

#### Validação no Frontend:
No `ItemModal.tsx`, se `StockControlMode` for `Strict` e `produto.controlaEstoque === true`, o app impede a inserção de itens no carrinho caso a quantidade solicitada seja maior do que o `saldo` em cache local no IndexedDB.

#### Validação no Adaptador ERP:
O importador de pedidos (`OrderImporter.cs`) verifica o saldo físico no ERP se o modo for `Strict` antes de faturar a transação.

#### Tarefas:
*   `T-STOCK-01`: Criar parâmetro `Sales:StockControlMode` no backend (`appsettings.json`).
*   `T-STOCK-02`: Criar endpoint `/api/sales/config` para expor regras do tenant.
*   `T-STOCK-03`: Integrar consumo das regras na store de autenticação do Frontend.
*   `T-STOCK-04`: Validar saldo local no `ItemModal.tsx` se o modo for `Strict` e o produto controlar estoque.
*   `T-STOCK-05`: Atualizar o `OrderImporter.cs` no Adaptador ERP para rejeitar pedidos sem saldo físico caso o modo seja `Strict`.
*   `T-STOCK-06`: Escrever testes de validação de estoque com o modo habilitado e desabilitado.

---

## Ordem de Execução Recomendada

```
══════════════════════════════════════════════════════
  IMPLEMENTAR AGORA (Fases 1-4)
══════════════════════════════════════════════════════

Fase 1 (2-3 dias):  INT-01 — Abstração de transporte
                     ├── T-INT-01: Interface + DTOs
                     ├── T-INT-02: Extensão DI
                     ├── T-INT-03: Integrar no CriarPedidoCommand
                     └── T-INT-04: Testes

Fase 2 (3-4 dias):  INT-02 — Provider FTP 🟢
                     ├── T-FTP-01: Implementação
                     └── T-FTP-02: Testes

Fase 3 (3-4 dias):  INT-05 — Worker real
                     ├── T-WORKER-01: Refatorar Worker/Program
                     ├── T-WORKER-02: CatalogSyncJob
                     └── T-WORKER-03: ResultPollingJob

Fase 4 (4-5 dias):  INT-06 — Adaptador ERP (.NET 8)
                     ├── T-ERP-01: Criar projeto
                     ├── T-ERP-02: CatalogExporter
                     ├── T-ERP-03: OrderImporter
                     └── T-ERP-04: Teste E2E

  Subtotal implementação imediata: 13-16 dias (2.5-3 semanas)

══════════════════════════════════════════════════════
  FASE POSTERIOR (Analisado, implementar quando necessário)
══════════════════════════════════════════════════════

Fase 5 (3-4 dias):  INT-03 — Provider Google Drive 🟡
                     ├── T-GDRIVE-01: Implementação
                     └── T-GDRIVE-02: Testes

Fase 6 (2 dias):    INT-04 — Provider RabbitMQ 🟡
                     ├── T-RABBIT-01: Implementação
                     └── T-RABBIT-02: Testes

Fase 7 (12-15 dias): PWA-01 — Offline (P1)
                     ├── T-PWA-01: next-pwa + manifest
                     ├── T-PWA-02: Cache catálogo IndexedDB
                     ├── T-PWA-03: Pedidos offline
                     ├── T-PWA-04: Fila de sync
                     ├── T-PWA-05: Service Worker
                     ├── T-PWA-06: Indicador offline
                     ├── T-PWA-07: Conflitos de estoque
                     └── T-PWA-08: Testes

Fase 8 (5-7 dias):  PWA-02 — Controle Híbrido de Estoque (P2)
                     ├── T-STOCK-01: Parâmetro no backend
                     ├── T-STOCK-02: Endpoint /api/sales/config
                     ├── T-STOCK-03: Integração no Frontend Store
                     ├── T-STOCK-04: Validação no ItemModal.tsx
                     ├── T-STOCK-05: OrderImporter erp-adapter update
                     └── T-STOCK-06: Testes de estoque

  Subtotal fase posterior: 22-28 dias (4.5-5.5 semanas)

Total geral: 35-44 dias (7-8.5 semanas)
```

---

## Regras para a IA

1. **Ler antes de implementar**: Antes de cada tarefa, ler os arquivos existentes referenciados
2. **Manter convenções**: Namespaces, estilo de código e padrões de teste do projeto
3. **Branch por story**: Cada story = uma branch `feature/002-int-{slug}`
4. **Testes proporcionais**: Unitários para lógica, integração para cross-boundary
5. **Respeitar constituição**: `.specify/memory/constitution.md` — 5 princípios obrigatórios
6. **Program.cs fino**: Não adicionar lógica — usar extensões
7. **Isolamento multi-tenant**: Todo endpoint/job DEVE filtrar por tenant
8. **Contratos versionados**: Não alterar JSONs sem bumpar versão
9. **Documentação**: Atualizar `docs/sdd/04-interfaces-integracao.md` ao finalizar
