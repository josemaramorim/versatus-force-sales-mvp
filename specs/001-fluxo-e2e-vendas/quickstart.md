# Quickstart - Fluxo E2E de Forca de Vendas MVP

> **Nota de login (contrato v1.1.0)**: o formulario de login aceita somente `email` + `senha`. O `tenantId` e resolvido internamente pelo backend a partir do email cadastrado — nao ha campo tenant visivel ao usuario.
>
> **Janela de transicao de contrato**: clientes que enviavam `username` tem compatibilidade garantida ate **2026-05-13**. Apos essa data somente `email` sera aceito. Veja `contracts/rest-e2e-vendas.openapi.yaml` secao `x-contract-migration` para detalhes.

## 1. Pre-requisitos

| Dependencia | Versao minima | Verificacao |
|---|---|---|
| .NET SDK | 8.x | `dotnet --version` |
| Node.js | 20+ | `node --version` |
| Docker Desktop | qualquer recente | `docker --version` |
| PostgreSQL | via Docker | veja secao 2 |
| Redis | via Docker | veja secao 2 |
| RabbitMQ | via Docker | veja secao 2 |

Configuracao de conexoes em `src/backend/Versatus.ForcaVendas.Api/appsettings.Development.json`.

## 2. Subir dependencias

Na raiz do repositorio:

```powershell
docker compose up -d
```

Servicos iniciados:
- **PostgreSQL** — porta `5432`
- **Redis** — porta `6379`
- **RabbitMQ** — AMQP `5672`, Management UI `http://localhost:15672`

## 3. Backend API (.NET 8)

```powershell
dotnet run --project src/backend/Versatus.ForcaVendas.Api
```

API disponivel em `http://localhost:5000`. Swagger UI em `http://localhost:5000/swagger`.

**Endpoints do fluxo E2E:**

| Metodo | Rota | Historia | Descricao |
|---|---|---|---|
| POST | `/auth/login` | US1 | Login por email+senha; tenant resolvido internamente |
| PATCH | `/auth/heartbeat` | US1 | Renova sessao/seat |
| POST | `/auth/logout` | US1 | Encerra sessao e libera seat |
| GET | `/catalogo/clientes` | US2 | Busca clientes do tenant autenticado |
| GET | `/catalogo/produtos` | US2 | Busca produtos do tenant autenticado |
| POST | `/pedidos` | US2 | Cria pedido com status inicial `rascunho` |
| GET | `/pedidos` | US2 | Lista historico paginado de pedidos |
| GET | `/pedidos/{id}` | US2 | Detalha pedido por UUID |

## 4. Worker (.NET 8)

```powershell
dotnet run --project src/worker/Versatus.ForcaVendas.Worker
```

> **US1/US2**: worker em modo baseline (loop de background sem consumo ativo).
> **US3 em diante**: worker passa a consumir eventos `pedido.enviado` do RabbitMQ e publicar `pedido.resultado`.

## 5. Frontend Next.js

```powershell
cd src/frontend/app
npm install
npm run dev
```

Aplicacao em `http://localhost:3000`. CORS liberado no backend para portas `3000` e `3001`.

## 6. Executar testes automatizados backend

Na raiz do repositorio:

```powershell
# Suite completa
dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/

# Com log detalhado
dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/ --logger "console;verbosity=detailed"

# Apenas testes de auth (US1)
dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/ --filter "FullyQualifiedName~AuthTests"

# Apenas testes de catalogo/pedidos (US2)
dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/ --filter "FullyQualifiedName~CatalogTests|FullyQualifiedName~PedidosTests"
```

## 7. Roteiro E2E de validacao rapida

### Passo 1 — Login (US1)

```http
POST http://localhost:5000/auth/login
Content-Type: application/json

{
  "email": "vendedor@tenant-demo.com",
  "senha": "Senha@123"
}
```

> Tenant e resolvido internamente. Resposta inclui `accessToken` e `refreshToken`.

### Passo 2 — Consultar catalogo (US2)

```http
GET http://localhost:5000/catalogo/clientes?q=empresa&limit=10
Authorization: Bearer <accessToken>
```

### Passo 3 — Criar pedido (US2)

```http
POST http://localhost:5000/pedidos
Authorization: Bearer <accessToken>
Content-Type: application/json

{
  "clienteId": "<id-do-cliente>",
  "itens": [
    {
      "produtoId": "<id-produto>",
      "sku": "PROD-001",
      "nome": "Produto Demo",
      "quantidade": 2,
      "precoUnitario": 100.00,
      "desconto": 5.00
    }
  ],
  "condicaoPagamento": {
    "quantidadeParcelas": "3",
    "primeiraDataVencimento": "2026-05-01",
    "formaPagamento": "boleto"
  }
}
```

### Passo 4 — Verificar historico (US2)

```http
GET http://localhost:5000/pedidos?page=1&pageSize=10
Authorization: Bearer <accessToken>
```

### Passo 5 — Publicar retorno de integracao (US3)

Apenas apos implementacao da US3. Publica evento `pedido.resultado` via RabbitMQ e valida transicao para `processado` ou `erro`.

### Passo 6 — Logout

```http
POST http://localhost:5000/auth/logout
Authorization: Bearer <accessToken>
```

## 8. Evidencias minimas para aceite

| Historia | Evidencia obrigatoria |
|---|---|
| US1 | Testes de auth passando; login com email sem campo tenant; logs com `tenantId` resolvido |
| US2 | Testes de catalogo/pedidos passando; `tenantId` presente nos logs de consulta e criacao |
| US3 | Idempotencia confirmada em retorno duplicado; transicao de status preservada fora de ordem |
| US4 | Roteiro executado sem modificacao de codigo; evidencias de logs e metricas capturadas |
