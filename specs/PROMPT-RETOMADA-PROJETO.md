# PROMPT DE RETOMADA — Versatus Force Sales MVP

> **Como usar**: Copie TODO o conteúdo deste arquivo e cole como mensagem inicial para qualquer IA assistente de código (Claude, ChatGPT, Gemini, Copilot, Cursor, etc). O prompt contém todo o contexto necessário para que a IA entenda o projeto e comece a trabalhar imediatamente.

---

## CONTEXTO DO PROJETO

Você é um assistente sênior de desenvolvimento que vai me ajudar a retomar e concluir o projeto **Versatus Force Sales MVP**. Este é um sistema **SaaS multi-tenant de Força de Vendas** com integração assíncrona a ERP legado.

### Objetivo do MVP
Entregar um fluxo ponta a ponta demonstrável para stakeholders:
1. Login por email/senha com resolução automática de tenant e controle de licenças simultâneas
2. Consulta de catálogo (clientes e produtos) isolado por tenant
3. Criação de pedidos com itens, parcelas e cálculo automático de totais
4. Integração assíncrona com ERP via RabbitMQ (envio e retorno de status)
5. Demonstração guiada do fluxo completo

### Origem do Projeto — ERP Legado "Small" (Módulo de Força de Venda)

Este MVP substitui o módulo **Small** do ERP Versatus — uma aplicação **WinForms offline-first** para vendedores de campo que sincronizava pedidos via FTP. As entidades legadas mapeadas são:

| Tabela legada | Equivalente na nova app | Observação |
|---|---|---|
| `MobVenda` | `Pedido` | Flags `exportada`/`processada` viraram enum de status |
| `MobVendaItem` | `PedidoItem` | Cálculo com desconto/acréscimo mantido |
| `MobVendaParcela` | `PedidoParcela` | Condição de pagamento do ERP |
| `MobCliente` | Catálogo (cache Redis) | Sincronizado previamente |
| `MobEstoque` | Catálogo (cache Redis) | Estoque controlado pelo ERP, não pela nova app |
| `MobConfiguracao` | Configuração por tenant/usuário | `descontoMaximo` vira política por perfil |

**Integração com faturamento ERP** — Método `VendaBase.GerarDocumentoVendaVersatus()`:
```
Código-fonte legado: Gestao.Small/VendaBase.cs (linha 1396)
1. ValidarVendaVersatus(transacao)  → se tem observação de erro, retorna
2. Instancia IDocumentoVenda via Factory
3. Seta: IdTipoDocumento, DataEmissao, IdTipoPlataforma
4. Resolve IdCliente (via lookup ou IdClienteVersatus direto)
5. Resolve IdCondicaoPagamento (via lookup ou IdCondicaoPagtoVersatus)
6. Para cada item: vsi.AddItemVendaVersatus(transacao, ref dv)
7. OnGerarDocumentoVendaVersatus(dv, transacao, msgErro)  ← abstract hook
8. Se sem erro: dv.Persist(transacao), marca Processada=true, preenche IdDocumentoVenda
9. Se erro: salva ObservacaoGeracaoVenda com mensagem
```
Na nova app, esse método é substituído por evento assíncrono `pedido.enviado` → Worker consome → chama ERP → retorna `pedido.resultado` (processado/erro).

**Cálculo de preço legado** (extraído do código-fonte `VendaSmallItem.cs`):

```csharp
// VendaSmallItem.CalcularItem(bool calcularDescontoValor) — LÓGICA REAL:
private void CalcularItem(bool calcularDescontoValor)
{
    CalcularValorTotal();           // valorTotal = Arredondar(quantidade * valorUnitario, 2)
    this.valorFinal = this.valorTotal;
    CalcularDesconto(calcularDescontoValor);  // valorFinal -= valorDesconto
    CalcularAcrescimo();                      // valorFinal += valorAcrescimo
}

// CalcularDesconto:
//   Se calcularDescontoValor: valorDesconto = valorTotal * (percentualDesconto / 100)
//   Se não: percentualDesconto = (valorDesconto / valorTotal) * 100
//   valorFinal = Arredondar(valorFinal - valorDesconto, 2)

// CalcularAcrescimo:
//   valorAcrescimo = valorFinal * (percentualAcrescimo / 100)
//   valorFinal = Arredondar(valorFinal + valorAcrescimo, 2)

// Desconto validado por PercentualDescontoPermitido():
//   → Verifica ConfiguracaoSmall.ControlaDescontoMaximoSmall
//   → Compara com ConfiguracaoSmall.PercentualDescontoMaximoSmall
//   → Ou compara com TabelaPrecoEstoque.PercentualDescontoMaximo
```

**Totais do pedido** (extraído de `VendaSmall.cs`):
```csharp
// Propriedades calculadas (não armazenadas diretamente):
ValorTotal        = Itens.ValorTotal;              // soma dos valorTotal de cada item
ValorTotalDesconto = Itens.TotalDescontos;          // soma dos valorDesconto de cada item
ValorTotalAcrescimo = Itens.TotalAcrescimos;        // soma dos valorAcrescimo de cada item
ValorFinal        = Arredondar((ValorTotal - ValorTotalDesconto + ValorTotalAcrescimo) + ValorFrete, 2);
```

**Parcelas** — Geradas por condição de pagamento:
```csharp
// VendaSmall.AplicarCondicaoPagto():
//   1. GerarParcelas(): limpa parcelas, cria N parcelas com base em CondicaoPagtoSmall.QuantidadeParcela
//   2. CalcularValorParcelas(): distribui ValorFinal entre parcelas
//   3. SetarAcrescimoDesconto(): aplica acréscimo/desconto da condição nos itens via CondicaoPagtoSmall.Acrescimo/Desconto
```

**Campos da entidade MobVenda** (`VendaSmallBase.cs`):
- `idVendaSmall`, `idClienteSmall`, `nomePreCliente`, `idCondicaoPagamentoSmall`
- `dataEmissao`, `valorTotal`, `desconto`, `acrescimo`
- `nomeUsuario`, `chaveDispositivo`, `observacao`
- `orcamento` (bool), `exportada` (bool), `processada` (bool), `idComissionado`

**Campos da entidade MobVendaItem** (`VendaSmallItemBase.cs`, tabela `MobVendaItem`):
- `IdMobVendaItem` (PK), `IdGloFilial`, `IdMobVenda` (FK), `IdMobTabelaPrecoEstoque`, `IdMobEstoque`
- `SiglaUnidade`, `Quantidade` (double), `ValorUnitario` (double)
- `Desconto` (valorDesconto, double), `Acrescimo` (valorAcrescimo, double)
- `ValorTotal` (valorFinal = líquido do item, double)

**Campos da entidade MobVendaParcela** (`VendaSmallParcelaBase.cs`, tabela `MobVendaParcela`):
- `IdMobVendaParcela` (PK), `IdGloFilial`, `IdMobVenda` (FK)
- `NumeroParcela` (int), `IdMobFormaCobranca` (FK), `Valor` (double), `Vencimento` (DateTime)

**Flags de controle de sincronização** (em `VendaSmall.cs`):
- `exportada = false` → pedido ainda no dispositivo
- `exportada = true, processada = false` → recebido pelo servidor, aguardando `GerarDocumentoVendaVersatus()`
- `exportada = true, processada = true` → DocumentoVenda gerado; `idDocumentoVenda` preenchido

**Controle de desconto** (`ConfiguracaoSmall.cs`):
- `ControlaDescontoMaximoSmall` (bool) — habilita validação
- `PercentualDescontoMaximoSmall` (double) — limite percentual global
- Alternativa: `TabelaPrecoEstoqueSmall.PercentualDescontoMaximo` — limite por produto/tabela

> Código-fonte legado completo em: `C:\...\Gestao.Small\` (32 arquivos C#)
> Documentação de análise: `Analise/06-app-forca-venda-web.md` (841 linhas)


### Constituição do Projeto (.specify/memory/constitution.md)

O projeto usa o framework **SpecKit** para governança. A constituição define 5 princípios obrigatórios que TODA mudança DEVE respeitar:

**I. MVP Value Slice First** — Cada mudança DEVE entregar uma fatia demonstrável do fluxo MVP. Trabalho DEVE ser divisível em incrementos validáveis independentemente.

**II. Tenant Isolation and Session Licensing** — Isolamento multi-tenant obrigatório em dados, cache, eventos e controle de acesso. Sessões simultâneas controladas por Redis com rejeição determinística. Fail-closed na falta de contexto de tenant.

**III. Contract-Driven Integration and Status Flow** — Contratos de API e eventos DEVEM ser explícitos, versionados e testáveis antes do merge. Transições de status do pedido DEVEM ser observáveis de ponta a ponta. Handlers de integração DEVEM ser idempotentes.

**IV. Test and CI Quality Gates** — CI verde obrigatório para merge. Testes proporcionais ao risco (unitários para domínio, integração para cross-boundary, contrato para API/eventos). Testes existentes NÃO podem ser desabilitados.

**V. Observability and Operational Traceability** — Logs estruturados com correlação (tenantId, pedidoId, correlationId). Falhas de auth, tenant, licença, mensageria e status DEVEM ser logadas com contexto acionável.

**Regras de governança adicionais**:
- `Program.cs` DEVE permanecer fino (orquestração). Arquivos >350 linhas exigem decomposição.
- Endpoints DEVEM ser agrupados por contexto (Auth, Catalogo, Pedidos, Admin, Health).
- Documentação em `docs/sdd/` e `Analise/` DEVE ser atualizada quando arquitetura/contratos mudarem.

### Documentação Existente (Referência Obrigatória)

**Pasta `Analise/`** — Análise completa do ERP legado e planejamento do MVP:
- `01-diagnostico-legado.md` — Diagnóstico técnico do estado atual
- `02-arquitetura-alvo-net8.md` — Recomendação de stack .NET 8
- `03-priorizacao-dominios.md` — Ordem de estrangulamento dos domínios
- `04-roadmap-execucao.md` — Plano faseado de migração
- `05-plano-piloto-acesso-global.md` — Primeiro módulo migrado
- `06-app-forca-venda-web.md` — **Principal** — Análise completa do módulo Small, domínio, arquitetura, banco, SaaS (36 KB)
- `07-conducao-projeto-mvp.md` — Governança, sprints de 1 semana, DoR/DoD, branches, PRs
- `08-backlog-mvp-historias-tarefas.md` — Backlog GitHub com 7 stories e tasks por issue

**Pasta `docs/sdd/`** — Software Design Document corporativo:
- `01-visao-geral.md` — Escopo e contexto
- `02-arquitetura.md` — Componentes e diagrama
- `03-modelo-dados-estado.md` — PostgreSQL, multi-tenant, Redis
- `04-interfaces-integracao.md` — API, webhooks, mensageria
- `05-seguranca-requisitos-nfs.md` — JWT, performance, offline, licenciamento

**Pasta `specs/001-fluxo-e2e-vendas/`** — Especificação da feature de fluxo E2E:
- `spec.md` — 4 User Stories com cenários de aceite (14 KB)
- `plan.md` — Plano de implementação com roadmap por PR (11 KB)
- `tasks.md` — 72 tarefas detalhadas com status (18 KB)
- `data-model.md` — Modelo de dados com regras
- `research.md` — Decisões técnicas (broker, idempotência, observabilidade)
- `quickstart.md` — Guia de início rápido com roteiro E2E
- `contracts/` — OpenAPI 3.0.3 + JSON Schema de eventos

**Pasta `specs/002-integracao-erp-ftp-gdrive/`** — Integração ERP + PWA Offline:
- `spec.md` — Especificação completa com 7 Stories e ~30 tasks, incluindo:
  - **Decisões**: FTP primeiro (🟢), RabbitMQ e Google Drive analisados mas adiados (🟡), formato JSON, adaptador ERP em .NET 8
  - Interface `IIntegrationTransport` (Strategy Pattern para trocar provider via config)
  - Contratos JSON de catálogo, pedidos e resultados
  - **Implementar agora (Fases 1-4, 13-16 dias)**: Abstração + FTP + Worker + Adaptador ERP
  - **Fase posterior**: Google Drive, RabbitMQ e PWA/Offline (17-21 dias adicionais)

**Governança de condução** (de `07-conducao-projeto-mvp.md`):
- Sprint curta: 1 semana
- Story max 3 dias úteis; Task max 1 dia
- Branch max 2 dias aberta sem PR
- Merge squash, PR pequeno (≤400 linhas)
- Demo semanal obrigatória
- Documentação atualizada a cada entrega

### Backlog GitHub — Status Atual (de `08-backlog-mvp-historias-tarefas.md`)

| Story | Issue | Status |
|-------|-------|--------|
| MVP-01 Auth | #1 | ✅ Tasks #8-#10 fechadas — candidata a fechamento |
| MVP-02 Licença | #2 | ✅ Tasks #11-#14 fechadas — candidata a fechamento |
| MVP-03 Catálogo | #3 | ✅ Tasks #15-#17 implementadas — candidata a fechamento |
| MVP-04 Pedidos | #4 | ⚠️ Tasks #18-#21 abertas — **próximo foco** |
| MVP-05 Integração | #5 | ❌ Tasks #22-#24 abertas |
| MVP-06 Frontend | #6 | ❌ Tasks #25-#28 abertas |
| MVP-07 Qualidade | #7 | ❌ Tasks #29-#31 abertas |

### Stack Tecnológica
- **Backend API**: C# 12 / .NET 8 — ASP.NET Core Minimal APIs, EF Core 8, MediatR, FluentValidation
- **Domain/Application**: .NET 8 — Entidades, interfaces, contratos de serviço
- **Infrastructure**: PostgreSQL (Npgsql), Redis (StackExchange.Redis), RabbitMQ
- **Worker**: .NET 8 BackgroundService — Consumidor de eventos de integração
- **Frontend**: TypeScript 5, React 19, Next.js 16, Zustand, Axios
- **Testes**: xUnit + FluentAssertions
- **Infraestrutura local**: Docker Compose (PostgreSQL 5432, Redis 6379, RabbitMQ 5672/15672)

### Estrutura do Repositório

```
versatus-force-sales-mvp/
├── specs/001-fluxo-e2e-vendas/          # Especificações da feature
│   ├── spec.md                          # Especificação funcional (4 User Stories, 18 FR)
│   ├── plan.md                          # Plano de implementação
│   ├── tasks.md                         # Lista de 72 tarefas com status
│   ├── data-model.md                    # Modelo de dados
│   ├── research.md                      # Decisões técnicas (ADRs)
│   ├── quickstart.md                    # Guia de início rápido
│   └── contracts/
│       ├── rest-e2e-vendas.openapi.yaml # Contrato REST OpenAPI 3.0.3 v1.1.0
│       └── eventos-integracao-pedidos.schema.json  # Contrato eventos assíncronos
│
├── src/backend/
│   ├── Versatus.ForcaVendas.Api/        # API principal
│   │   ├── Auth/                        # Endpoints auth (login/heartbeat/logout/evict)
│   │   │   ├── AuthEndpoints.cs         # ✅ Implementado (12.8 KB)
│   │   │   ├── AuthModels.cs            # ✅ Implementado
│   │   │   ├── AuthOptions.cs           # ✅ Implementado
│   │   │   ├── JwtTokenService.cs       # ✅ Implementado
│   │   │   ├── InMemoryRefreshTokenStore.cs # ✅ Implementado
│   │   │   ├── LogoutRequest.cs         # ✅ Implementado
│   │   │   └── EvictRequest.cs          # ✅ Implementado
│   │   ├── Pedidos/
│   │   │   ├── PedidosEndpoints.cs      # ✅ Implementado (POST/GET/GET{id})
│   │   │   ├── CriarPedidoCommand.cs    # ✅ Implementado (MediatR handler)
│   │   │   ├── CriarPedidoRequest.cs    # ✅ Implementado (com validações)
│   │   │   ├── CriarPedidoRequestValidator.cs # ✅ Implementado (FluentValidation)
│   │   │   ├── PedidoResponse.cs        # ✅ Implementado (DTOs)
│   │   │   ├── IPedidoCache.cs          # ✅ Implementado
│   │   │   └── InMemoryPedidoCache.cs   # ✅ Implementado
│   │   ├── Middleware/
│   │   │   ├── TenantContextMiddleware.cs # ✅ Implementado
│   │   │   └── TenantContext.cs         # ✅ Implementado
│   │   ├── CatalogoEndpoints.cs         # ✅ Implementado
│   │   ├── Program.cs                   # ✅ Bootstrap fino
│   │   └── Program.Partial.cs           # ✅ Composição de DI (8.3 KB)
│   │
│   ├── Versatus.ForcaVendas.Application/
│   │   ├── Catalogo/                    # ✅ Interfaces de catálogo
│   │   ├── Licenca/                     # ✅ ITenantSubscriptionRepository
│   │   ├── Sessao/                      # ✅ ISessionStore, SessionInfo
│   │   └── Pedidos/
│   │
│   ├── Versatus.ForcaVendas.Domain/
│   │   ├── Pedidos/
│   │   │   ├── Pedido.cs                # ⚠️ PRECISA ATUALIZAR (apenas POCOs, sem lógica de domínio)
│   │   │   ├── PedidoItem.cs            # ⚠️ PRECISA ATUALIZAR
│   │   │   ├── PedidoParcela.cs         # ⚠️ PRECISA ATUALIZAR
│   │   │   ├── PedidoStatus.cs          # ⚠️ FALTA: AguardandoProcessamentoId
│   │   │   └── Services/
│   │   │       └── IPaymentConditionService.cs # ✅ Interface implementada
│   │   └── Licenca/                     # ✅ Implementado
│   │
│   ├── Versatus.ForcaVendas.Infrastructure/
│   │   ├── Data/
│   │   │   ├── PedidosDbContext.cs       # ✅ Mapeamento EF Core (9.8 KB)
│   │   │   ├── EventoIntegracaoPedidoEntity.cs # ✅ Entity criada
│   │   │   ├── SessionAuditEventEntity.cs # ✅ Implementado
│   │   │   ├── TenantSubscriptionEntity.cs # ✅ Implementado
│   │   │   ├── UsuarioEntity.cs          # ✅ Implementado
│   │   │   ├── Services/
│   │   │   │   └── MockPaymentConditionService.cs # ✅ Implementado
│   │   │   └── Migrations/              # ✅ Migrações existentes
│   │   ├── Messaging/                   # ❌ VAZIO — apenas .gitkeep
│   │   └── Redis/                       # ✅ Sessões em Redis
│   │
│   └── Versatus.ForcaVendas.Api.Tests/
│       ├── AuthTests.cs                 # ✅ (9.4 KB)
│       ├── AuthContractTests.cs         # ✅ (4.3 KB)
│       ├── CatalogTests.cs              # ✅ (6.8 KB)
│       ├── PedidosTests.cs              # ✅ (9.0 KB)
│       ├── PedidosContractTests.cs      # ✅ (4.9 KB)
│       ├── PedidoDomainRulesTests.cs    # ✅ (2.1 KB)
│       └── Stubs/                       # ✅ InMemory stubs
│
├── src/frontend/app/src/
│   ├── app/
│   │   ├── page.tsx                     # Página principal
│   │   ├── (auth)/login/page.tsx        # Tela de login
│   │   └── (admin)/                     # Área admin
│   ├── components/
│   │   ├── auth/LoginForm.tsx           # ✅ Componente de login
│   │   └── vendas/
│   │       ├── ClientSearch.tsx          # ⚠️ Precisa integrar com API real
│   │       ├── ItemModal.tsx            # ⚠️ Precisa integrar com API real
│   │       └── OrderTable.tsx           # ⚠️ Precisa integrar com API real
│   ├── lib/
│   │   ├── api.ts                       # ✅ Axios instance
│   │   ├── auth.ts                      # ✅ Auth helpers
│   │   ├── vendaApi.ts                  # ✅ API calls (catálogo + pedidos)
│   │   └── mocks.ts                     # Dados mock
│   ├── store/
│   │   ├── authStore.ts                 # ✅ Zustand auth store
│   │   └── uiStore.ts                   # ⚠️ Básico, falta status de integração
│   └── types/vendas.ts                  # ✅ Tipos TypeScript
│
└── src/worker/Versatus.ForcaVendas.Worker/
    ├── Program.cs                       # ❌ Baseline (apenas AddHostedService)
    ├── Worker.cs                        # ❌ Loop vazio (log a cada 1s)
    └── Consumers/                       # ❌ VAZIO — apenas .gitkeep
```

---

## O QUE JÁ FOI IMPLEMENTADO (58% — 42 de 72 tarefas)

### ✅ US1 — Autenticação e Licenciamento (100% COMPLETA)
- Login por `email` + `senha` (sem campo tenant — tenant resolvido internamente via lookup de email)
- JWT com claims `sub=userId`, `email`, `tenantId`, `role`
- Refresh token + revogação
- Controle de seats simultâneos em Redis por tenant
- Heartbeat para renovar sessão
- Logout com liberação de seat
- Eviction administrativa
- Auditoria de sessão (login/heartbeat/logout/evict) persistida no PostgreSQL
- TenantContext middleware fail-closed
- Testes: login válido/inválido, concorrência de seat, contrato OpenAPI

### ✅ US2 — Catálogo e Pedidos (60% — Falta domain/persistência/frontend)
**Implementado:**
- Endpoints REST: `POST /pedidos`, `GET /pedidos`, `GET /pedidos/{id}`
- Endpoints catálogo: `GET /catalogo/clientes`, `GET /catalogo/produtos`
- DTOs: `CriarPedidoRequest`, `CriarPedidoResponse`, `PedidoResponse`, `PedidoSummaryResponse`
- Validações: campos obrigatórios, quantidade > 0, desconto <= bruto
- MediatR handler `CriarPedidoCommand` com cálculo de totais e parcelamento
- Testes: catálogo, pedidos, contratos, regras de domínio
- Frontend: `vendaApi.ts` com chamadas API, componentes vendas (ClientSearch, ItemModal, OrderTable)

**NÃO implementado (tarefas T036-T041):**
- Agregados de domínio precisam de métodos de cálculo/validação (hoje são apenas POCOs)
- `PedidoStatus` falta constante `AguardandoProcessamentoId = 5` para US3
- Frontend precisa integração real com API (ainda usa mocks em algumas telas)

### ❌ US3 — Integração Assíncrona ERP (0% — 12 tarefas pendentes)
NADA foi implementado:
- Pasta `Messaging/` está vazia (apenas .gitkeep)
- Pasta `Consumers/` no worker está vazia
- Worker é apenas um loop vazio que loga a cada segundo
- Entity `EventoIntegracaoPedidoEntity` existe mas migração/lógica de idempotência faltam

### ❌ US4 — Demo Guiada (0% — 5 tarefas pendentes)

### ❌ Final Phase — Polish (30% — 7 tarefas pendentes)

---

## CÓDIGO-FONTE ATUAL DOS ARQUIVOS CRÍTICOS PENDENTES

### Domain: Pedido.cs (ATUAL — precisa de métodos de domínio)
```csharp
namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class Pedido
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ClienteId { get; set; } = string.Empty;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? AtualizadoEm { get; set; }
    public int StatusId { get; set; }
    public decimal TotalBruto { get; set; }
    public decimal TotalDesconto { get; set; }
    public decimal TotalLiquido { get; set; }
    public string? Observacao { get; set; }

    public PedidoStatus? Status { get; set; }
    public ICollection<PedidoItem> Itens { get; set; } = new List<PedidoItem>();
    public ICollection<PedidoParcela> Parcelas { get; set; } = new List<PedidoParcela>();
}
```

### Domain: PedidoItem.cs (ATUAL)
```csharp
namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoItem
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public string ProdutoId { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string Nome { get; set; } = string.Empty;
    public decimal Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal Desconto { get; set; }
    public decimal Total { get; set; }

    public Pedido? Pedido { get; set; }
}
```

### Domain: PedidoParcela.cs (ATUAL)
```csharp
namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoParcela
{
    public Guid Id { get; set; }
    public Guid PedidoId { get; set; }
    public int Numero { get; set; }
    public DateTime DataVencimento { get; set; }
    public decimal Valor { get; set; }
    public string FormaPagamento { get; set; } = string.Empty;
    public DateTimeOffset? PagoEm { get; set; }

    public Pedido? Pedido { get; set; }
}
```

### Domain: PedidoStatus.cs (ATUAL — falta AguardandoProcessamentoId)
```csharp
namespace Versatus.ForcaVendas.Domain.Pedidos;

public sealed class PedidoStatus
{
    public const int RascunhoId = 1;
    public const int EnviadoId = 2;
    public const int ProcessadoId = 3;
    public const int ErroId = 4;
    // FALTA: public const int AguardandoProcessamentoId = 5;

    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;

    public ICollection<Pedido> Pedidos { get; set; } = new List<Pedido>();
}
```

### Infrastructure: EventoIntegracaoPedidoEntity.cs (ATUAL)
```csharp
namespace Versatus.ForcaVendas.Infrastructure.Data;

public sealed class EventoIntegracaoPedidoEntity
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public Guid PedidoId { get; set; }
    public string SourceEventId { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? ProcessadoEm { get; set; }
    public bool? Sucesso { get; set; }
}
```

### Infrastructure: Messaging/ (VAZIO — precisa criar)
Precisa de:
- `PedidoEnviadoEvent.cs` — Evento de despacho para RabbitMQ
- `PedidoResultadoEvent.cs` — Evento de retorno do ERP
- `PedidoIntegrationPublisher.cs` — Publicador no RabbitMQ

### Worker: Worker.cs (ATUAL — loop vazio)
```csharp
namespace Versatus.ForcaVendas.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

### Worker: Program.cs (ATUAL — baseline)
```csharp
using Versatus.ForcaVendas.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
```

### Frontend: uiStore.ts (ATUAL — falta status de integração)
```typescript
import { create } from 'zustand'

interface UIState {
  isMobileMenuOpen: boolean
  setMobileMenuOpen: (open: boolean) => void
  toggleMobileMenu: () => void
}

export const useUIStore = create<UIState>((set) => ({
  isMobileMenuOpen: false,
  setMobileMenuOpen: (open) => set({ isMobileMenuOpen: open }),
  toggleMobileMenu: () => set((state) => ({ isMobileMenuOpen: !state.isMobileMenuOpen })),
}))
```

---

## CONTRATOS DE API (REFERÊNCIA)

### Endpoints REST (OpenAPI v1.1.0)

| Método | Rota | US | Descrição |
|--------|------|-----|-----------|
| `POST` | `/auth/login` | US1 | Login por email+senha; tenant resolvido internamente |
| `PATCH` | `/auth/heartbeat` | US1 | Renova sessão/seat |
| `POST` | `/auth/logout` | US1 | Encerra sessão e libera seat |
| `GET` | `/catalogo/clientes?q=&limit=` | US2 | Busca clientes do tenant |
| `GET` | `/catalogo/produtos?q=&limit=` | US2 | Busca produtos do tenant |
| `POST` | `/pedidos` | US2 | Cria pedido com status `rascunho` |
| `GET` | `/pedidos?clienteId=&status=&page=&pageSize=` | US2 | Lista histórico paginado |
| `GET` | `/pedidos/{id}` | US2 | Detalha pedido por UUID |

### Eventos Assíncronos (JSON Schema)

**pedido.enviado.v1** (API → RabbitMQ → Worker):
```json
{
  "eventType": "pedido.enviado",
  "eventVersion": "v1",
  "eventId": "uuid",
  "occurredAt": "ISO-8601",
  "correlationId": "string",
  "tenantId": "string",
  "pedidoId": "uuid",
  "payload": {
    "clienteId": "string",
    "itens": [{ "produtoId": "string", "quantidade": 2, "precoUnitario": 100.00, "total": 200.00 }],
    "totalLiquido": 200.00
  }
}
```

**pedido.resultado.v1** (Worker → RabbitMQ → API):
```json
{
  "eventType": "pedido.resultado",
  "eventVersion": "v1",
  "eventId": "uuid",
  "occurredAt": "ISO-8601",
  "correlationId": "string",
  "tenantId": "string",
  "pedidoId": "uuid",
  "payload": {
    "resultado": "processado|erro",
    "documentoVendaId": "NF-2026-00042",     // obrigatório se processado
    "motivoRejeicao": "Sem estoque no ERP",  // obrigatório se erro
    "sourceEventId": "uuid"                  // chave de idempotência
  }
}
```

### Transições de Status do Pedido
```
rascunho → enviado → aguardando_processamento → processado
                                                → erro
```
- Idempotência: chave `(tenantId, pedidoId, sourceEventId)` — evento duplicado = no-op
- Fora de ordem: registrar como inconsistência controlada sem corromper estado final

---

## MODELO DE DADOS

| Entidade | Tabela | Campos-chave |
|----------|--------|-------------|
| TenantSubscription | `assinaturas` | `tenant_id (PK)`, `nome_empresa`, `max_usuarios_simultaneos`, `ativo` |
| Usuario | `usuarios` | `id (PK)`, `tenant_id`, `email (unique global)`, `password_hash`, `role`, `ativo` |
| SessaoAtiva | Redis | `sessionId`, `tenantId`, `userId`, `loginAt`, `lastHeartbeatAt`, `expiresAt` |
| SessionAuditEvent | `audit_events` | `id`, `user_id`, `tenant_id`, `event_type`, `timestamp` |
| Pedido | `pedidos` | `id (PK)`, `tenant_id`, `cliente_id`, `status_id (FK)`, `total_bruto/desconto/liquido` |
| PedidoItem | `pedido_itens` | `id (PK)`, `pedido_id (FK)`, `produto_id`, `sku`, `quantidade`, `preco_unitario`, `desconto`, `total` |
| PedidoParcela | `pedido_parcelas` | `id (PK)`, `pedido_id (FK)`, `numero`, `data_vencimento`, `valor`, `forma_pagamento` |
| PedidoStatus | `pedido_status` | `id (PK)`, `codigo`, `descricao` — Seeds: rascunho(1), enviado(2), processado(3), erro(4) |
| EventoIntegracaoPedido | `eventos_integracao_pedidos` | `id (PK)`, `tenant_id`, `pedido_id`, `source_event_id`, `tipo`, `payload` — Índice único idempotência |

### Dados Seed (Usuários de Demo)
- **admin@demo1.versatus.com** / Senha@123 — tenant Demo Tenant 1 (4 seats)
- **gestor@demo2.versatus.com** / Senha@123 — tenant Demo Tenant 2 (4 seats)

---

## TAREFAS PENDENTES — ORDEM DE EXECUÇÃO

### 🔴 BLOCO 1: Finalizar US2 (3-5 dias)

**T036** — Atualizar agregados de domínio em `src/backend/Versatus.ForcaVendas.Domain/Pedidos/`:
- Adicionar métodos de cálculo `CalcularTotalItem()`, `RecalcularTotais()` em `Pedido.cs`
- Adicionar validação de domínio em `PedidoItem.cs` (quantidade > 0, preço >= 0)
- Fórmulas: `total_item = quantidade * precoUnitario - desconto`, `total_liquido = total_bruto - total_desconto`

**T037** — Verificar/ajustar `IPaymentConditionService` e `MockPaymentConditionService`:
- Garantir soma das parcelas = total_liquido com tolerância monetária
- Interface já existe e mock já funciona, revisar edge cases

**T038** — Ajustar mapeamento/persistência em `PedidosDbContext.cs`:
- Verificar se todos os mapeamentos de Pedido/Item/Parcela estão corretos para o fluxo completo
- Confirmar índices de `tenant_id` e `status_id`

**T039** — Ajustar endpoint e cache em `Program.cs` / `PedidosEndpoints.cs`:
- Confirmar que POST /pedidos funciona end-to-end com banco real
- Validar cache de pedidos e fallback

**T040** — Integrar frontend com API real:
- Atualizar `vendaApi.ts`, `api.ts`, `types/vendas.ts`
- Remover uso de mocks onde houver API real disponível

**T041** — Ajustar telas frontend:
- `ClientSearch.tsx`: busca de clientes com autocomplete via API
- `ItemModal.tsx`: seleção de produto e quantidades
- `OrderTable.tsx`: exibição do pedido montado e histórico

### 🟠 BLOCO 2: Implementar US3 — Integração ERP (5-7 dias)

**T045** — Criar contratos de mensageria em `src/backend/.../Infrastructure/Messaging/`:
- `PedidoEnviadoEvent.cs` — conforme schema `pedido.enviado` v1
- `PedidoResultadoEvent.cs` — conforme schema `pedido.resultado` v1

**T046** — Implementar `PedidoIntegrationPublisher.cs`:
- Publicar no RabbitMQ tópico `pedido.enviado.v1`
- Serializar payload conforme contrato JSON Schema

**T047** — Implementar trilha de idempotência:
- Lógica de deduplicação por `(tenantId, pedidoId, sourceEventId)` no `EventoIntegracaoPedidoEntity`
- Verificar antes de aplicar transição de status

**T048** — Criar migração EF Core para `eventos_integracao_pedidos` (se necessário)

**T049** — Integrar despacho no `CriarPedidoCommand.cs`:
- Após persistir pedido com `rascunho`, publicar evento e transicionar para `enviado`

**T050** — Implementar `PedidoResultadoConsumer.cs` em `src/worker/.../Consumers/`:
- Consumir do RabbitMQ tópico `pedido.resultado.v1`
- Aplicar transição: `aguardando_processamento → processado` ou `→ erro`
- Verificar idempotência antes de aplicar

**T051** — Ajustar `Worker.cs`:
- Registrar consumidor RabbitMQ
- Conectar ao broker
- Tratar erros com retry/dead-letter

**T052** — Implementar transições de status no domínio:
- Adicionar `AguardandoProcessamentoId = 5` ao `PedidoStatus.cs`
- Adicionar seed no `PedidosDbContext`
- Validar transições: `rascunho → enviado → aguardando_processamento → processado|erro`
- Rejeitar transições inválidas

**T053** — Exibir status no frontend:
- Atualizar `uiStore.ts` com estados de integração
- Ajustar `vendaApi.ts` com polling/refresh de status
- Badge de status na listagem de pedidos

**T042/T043/T044** — Testes: contrato JSON Schema, transição/idempotência, consumidor worker

### 🟡 BLOCO 3: Demo (2-3 dias)

**T054** — Criar `E2EDemoSmokeTests.cs` com teste ponta a ponta
**T055** — Completar checklist de requisitos em `specs/.../checklists/requirements.md`
**T056** — Consolidar roteiro de demo no `quickstart.md` (caminho feliz + erro ERP)
**T057** — Atualizar template de evidências para PR em `docs/ISSUE_PR_TEXTS_2026-03-25.md`
**T058** — Polir tela de login para demo em `src/frontend/app/src/app/(auth)/login/page.tsx`

### 🟢 BLOCO 4: Polish (3-4 dias)

**T059** — Estabilizar suite de testes: `dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/`
**T060** — Revisar healthcheck e telemetria em `Health/RedisHealthCheck.cs`
**T061** — Remover stubs `Class1.cs` de Application, Domain e Infrastructure
**T062** — Atualizar `README.md` e `spec.md` com status final
**T066/T067/T068** — Benchmarks de performance (catálogo p95 ≤ 1s, pedido p95 ≤ 5s, status ≤ 2min)

---

## DECISÕES DE DESIGN (ADRs)

1. **Login sem campo tenant**: O formulário de login recebe apenas `email` + `senha`. O `tenant_id` é resolvido internamente pelo backend via lookup do email cadastrado.
2. **Email único global**: O email é único em toda a plataforma (não por tenant). Isso simplifica login e elimina ambiguidade.
3. **Broker RabbitMQ**: Já configurado no docker-compose.yml. Tópicos: `pedido.enviado.v1` e `pedido.resultado.v1`.
4. **Idempotência**: Chave de deduplicação `(tenantId, pedidoId, sourceEventId)` persistida ANTES de aplicar transição de status.
5. **Monolito modular**: Backend API + Worker separado; sem microserviços adicionais.
6. **Observabilidade**: Logs estruturados com `tenantId`, `pedidoId`, `correlationId`, `sessionId`.
7. **Branches**: Padrão `feature/001-fluxo-e2e-usX-<slug>`, nunca commit direto em develop/main.

---

## REGRAS IMPORTANTES

1. **Isolamento multi-tenant**: TODO request protegido DEVE carregar contexto de tenant resolvido. Fail-closed sem tenant válido.
2. **Teste antes do código**: Para US2, os testes (T028-T031) JÁ EXISTEM. Use-os como guia (TDD).
3. **PRs pequenos**: Cada PR ≤ 5 arquivos de produção e ≤ 3 de teste. CI verde obrigatório.
4. **Program.cs fino**: Não adicionar lógica em `Program.cs` — usar extensões em arquivos dedicados.
5. **Contratos versionados**: Não alterar contratos REST/eventos sem bumpar versão.

---

## COMO RODAR O PROJETO

```powershell
# Subir dependências
docker compose up -d

# Backend API (http://localhost:5000, Swagger em /swagger)
dotnet run --project src/backend/Versatus.ForcaVendas.Api

# Worker
dotnet run --project src/worker/Versatus.ForcaVendas.Worker

# Frontend (http://localhost:3000)
cd src/frontend/app && npm install && npm run dev

# Testes
dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/
```

---

## INSTRUÇÃO PARA A IA

### Dois caminhos de trabalho disponíveis:

**Caminho A — Fluxo E2E de Vendas (MVP)**: Comece pela **tarefa T036** (Bloco 1 em `specs/001-fluxo-e2e-vendas/tasks.md`), que é o próximo passo do desenvolvimento do MVP. Foco: completar as Stories MVP-04 (Pedidos), MVP-05 (Integração RabbitMQ), MVP-06 (Frontend) e MVP-07 (Qualidade).

**Caminho B — Integração ERP via FTP/Google Drive + PWA**: Comece pela **Story INT-01** (em `specs/002-integracao-erp-ftp-gdrive/spec.md`). Foco: implementar os 3 providers de transporte (RabbitMQ, FTP, Google Drive), o Worker real, o adaptador ERP e o modo offline PWA. Total: 6 Stories de integração + 2 Stories PWA.

Pergunte ao usuário qual caminho seguir, ou se deseja trabalhar em ambos em paralelo.

### Regras obrigatórias para qualquer caminho:
1. Ser implementada em branch dedicada
2. Ter testes correspondentes (ou rodar os existentes)
3. Manter CI verde (`dotnet test`)
4. Seguir isolamento multi-tenant in todo endpoint
5. Respeitar os 5 princípios da constituição (`.specify/memory/constitution.md`)
6. Manter `Program.cs` fino — usar extensões por contexto
7. Contratos JSON versionados — não alterar sem bumpar versão

### Documentos que DEVE consultar antes de decisões de arquitetura:
- `.specify/memory/constitution.md` — Princípios obrigatórios
- `Analise/06-app-forca-venda-web.md` — Domínio completo extraído do ERP legado
- `Analise/07-conducao-projeto-mvp.md` — Regras de governança, sprints e PRs
- `docs/sdd/` — Software Design Document (arquitetura, dados, integração, segurança)
- `specs/001-fluxo-e2e-vendas/spec.md` — Especificação funcional com cenários de aceite
- `specs/001-fluxo-e2e-vendas/tasks.md` — Lista mestra de tarefas com status
- `specs/001-fluxo-e2e-vendas/contracts/` — Contratos REST e de eventos
- `specs/002-integracao-erp-ftp-gdrive/spec.md` — Integração ERP + PWA (6 Stories + 2 PWA Stories)

### Sobre PWA e Offline:
A app é **UMA aplicação só** (Next.js). PWA não é um projeto separado — é a mesma URL, mesmo deploy. Adiciona: Service Worker (cache offline), IndexedDB (dados locais via Dexie.js), manifest.json (instalar no celular).
*   **Fase P1**: Vendedor cria pedido sem internet → salva localmente → sincroniza automaticamente na volta da rede. (Implementado com sucesso)
*   **Fase P2**: Controle Híbrido de Estoque (configuração global `"Sales:StockControlMode"` com valor `Strict` / `Disabled` + propriedade `ControlaEstoque` individual por produto no catálogo) regulando as validações de quantidade vendida no front e faturamento no adaptador. (Planejado para execução posterior)

