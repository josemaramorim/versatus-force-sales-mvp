# Tasks: Fluxo E2E de Forca de Vendas MVP

**Input**: Documentos de design em `specs/001-fluxo-e2e-vendas/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

**Tests**: Incluidos por historia (unitario/integracao/contrato) no projeto existente `src/backend/Versatus.ForcaVendas.Api.Tests/`.

**Regra de granularidade**: cada tarefa deve caber em <= 1 dia; cada historia deve ser entregue em <= 3 dias com merge incremental.

## Formato: `[ID] [P?] [Story] Descricao com caminho`

- `[P]` = tarefa paralelizavel (arquivos diferentes, sem dependencia direta)
- `[USx]` = mapeamento para historia do `spec.md`

## Phase 1: Setup (Infraestrutura Compartilhada)

**Objetivo**: alinhar configuracao de ambiente, contratos e documentacao base para execucao do fluxo.

- [x] T001 Atualizar `specs/001-fluxo-e2e-vendas/plan.md` com referencia explicita ao roadmap de execucao por PR pequeno (US1-US4)
- [x] T002 [P] Revisar `specs/001-fluxo-e2e-vendas/quickstart.md` com comandos unificados para API, Worker, Frontend e testes backend
- [x] T003 [P] Revisar `specs/001-fluxo-e2e-vendas/contracts/rest-e2e-vendas.openapi.yaml` para manter aderencia aos endpoints implementados
- [x] T004 [P] Revisar `specs/001-fluxo-e2e-vendas/contracts/eventos-integracao-pedidos.schema.json` com exemplos de payload de sucesso/erro/idempotencia
- [x] T005 [P] Atualizar `docs/WORKFLOWS.md` com estrategia de PRs merge-friendly (tarefas <= 1 dia)
- [x] T063 [P] Definir estrategia de compatibilidade de contrato de login (versao/janela de transicao) em `specs/001-fluxo-e2e-vendas/contracts/rest-e2e-vendas.openapi.yaml` e `specs/001-fluxo-e2e-vendas/quickstart.md`

---

## Phase 2: Foundational (Bloqueadores Comuns)

**Objetivo**: completar base tecnica que bloqueia todas as historias.

**⚠️ Critico**: nenhuma US comeca antes da conclusao desta fase.

- [x] T006 Ajustar composicao de dependencias em `src/backend/Versatus.ForcaVendas.Api/Program.cs` (Auth, TenantContext, Redis, DbContext, mensageria)
- [x] T007 [P] Consolidar registro de servicos em `src/backend/Versatus.ForcaVendas.Api/Program.Partial.cs` para isolamento multi-tenant fail-closed
- [x] T008 [P] Endurecer resolucao de tenant em `src/backend/Versatus.ForcaVendas.Api/Middleware/TenantContextMiddleware.cs`
- [x] T009 [P] Ajustar contrato de contexto em `src/backend/Versatus.ForcaVendas.Api/Middleware/TenantContext.cs` para uso consistente em auth/catalogo/pedidos
- [x] T010 Implementar/ajustar persistencia base em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs` para suportar trilha de eventos de integracao
- [x] T011 [P] Criar migracao de base compartilhada em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/Migrations/` para colunas/indices de rastreio e idempotencia
- [x] T012 [P] Ajustar configuracao de conexoes em `src/backend/Versatus.ForcaVendas.Api/appsettings.Development.json` (PostgreSQL, Redis, RabbitMQ)
- [x] T013 [P] Adicionar configuracao de observabilidade minima em `src/backend/Versatus.ForcaVendas.Api/appsettings.json` (logs estruturados e categorias)
- [x] T064 Criar migracao para unicidade global de email em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/Migrations/` e ajustar mapeamento em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs`
- [x] T065 [P] Criar testes de integracao/concorrencia para email duplicado global em `src/backend/Versatus.ForcaVendas.Api.Tests/AuthTests.cs`

**Checkpoint**: base comum pronta para implementar historias em paralelo controlado.

---

## Phase 3: User Story 1 - Acesso com controle de licenca por tenant (Prioridade: P1) 🎯 MVP

**Goal**: autenticar por email+senha com tenant resolvido internamente, controle de seats em Redis, heartbeat e logout auditavel.

**Teste independente**: login com email/senha valido e invalido, resolucao automatica de tenant, disputa de ultimo seat, heartbeat e logout sem depender de catalogo/pedidos.

### Tests US1

- [ ] T014 [P] [US1] Criar testes de integracao de login (email/senha valido e invalido, tenant resolvido internamente), heartbeat e logout em `src/backend/Versatus.ForcaVendas.Api.Tests/AuthTests.cs`
- [ ] T015 [P] [US1] Criar testes de concorrencia de seat em `src/backend/Versatus.ForcaVendas.Api.Tests/AuthTests.cs`
- [ ] T016 [P] [US1] Criar testes de contrato de auth conforme OpenAPI em `src/backend/Versatus.ForcaVendas.Api.Tests/AuthContractTests.cs`
- [ ] T017 [P] [US1] Ajustar dublers de sessao para expiracao/eviccao em `src/backend/Versatus.ForcaVendas.Api.Tests/Stubs/InMemorySessionStore.cs`
- [ ] T018 [P] [US1] Ajustar dublers de licenca por tenant em `src/backend/Versatus.ForcaVendas.Api.Tests/Stubs/InMemoryTenantSubscriptionRepository.cs`

### Implementacao US1

- [ ] T019 [P] [US1] Ajustar modelos de auth em `src/backend/Versatus.ForcaVendas.Api/Auth/AuthModels.cs` para receber `email` + `senha` no login (sem campo tenant); incluir claims `sub=userId`, `email`, `tenantId` e `role` no JWT
- [ ] T020 [P] [US1] Ajustar opcoes JWT em `src/backend/Versatus.ForcaVendas.Api/Auth/AuthOptions.cs` para expiracao e renovacao coerentes
- [ ] T021 [US1] Implementar reforco de emissao de token em `src/backend/Versatus.ForcaVendas.Api/Auth/JwtTokenService.cs`
- [ ] T022 [US1] Implementar controle de refresh token e revogacao em `src/backend/Versatus.ForcaVendas.Api/Auth/InMemoryRefreshTokenStore.cs`
- [ ] T023 [US1] Ajustar fluxo de logout em `src/backend/Versatus.ForcaVendas.Api/Auth/LogoutRequest.cs`
- [ ] T024 [US1] Ajustar fluxo de eviccao administrativa em `src/backend/Versatus.ForcaVendas.Api/Auth/EvictRequest.cs`
- [ ] T025 [P] [US1] Consolidar contratos de sessao em `src/backend/Versatus.ForcaVendas.Application/Sessao/ISessionStore.cs` e `src/backend/Versatus.ForcaVendas.Application/Sessao/SessionInfo.cs`
- [ ] T026 [P] [US1] Ajustar contrato de assinatura/licenca em `src/backend/Versatus.ForcaVendas.Application/Licenca/ITenantSubscriptionRepository.cs` e `src/backend/Versatus.ForcaVendas.Application/Licenca/TenantSubscription.cs`
- [ ] T027 [US1] Persistir auditoria de sessao em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/SessionAuditEventEntity.cs` e expor consulta em `src/backend/Versatus.ForcaVendas.Api/Controllers/AuditController.cs`

**Checkpoint**: US1 funcional e validada isoladamente.

---

## Phase 4: User Story 2 - Consulta de catalogo e criacao de pedido (Prioridade: P2)

**Goal**: consultar catalogo por tenant e criar/listar/detalhar pedidos com validacoes e totais.

**Teste independente**: consultar clientes/produtos, criar pedido valido/invalido e consultar historico sem depender do worker.

### Tests US2

- [ ] T028 [P] [US2] Expandir cenarios de catalogo por tenant em `src/backend/Versatus.ForcaVendas.Api.Tests/CatalogTests.cs`
- [ ] T029 [P] [US2] Expandir cenarios de criacao/listagem/detalhe em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosTests.cs`
- [ ] T030 [P] [US2] Criar testes de contrato de pedidos (OpenAPI) em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosContractTests.cs`
- [ ] T031 [P] [US2] Criar testes unitarios de regra de totais/desconto em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidoDomainRulesTests.cs`

### Implementacao US2

- [ ] T032 [P] [US2] Ajustar contratos de catalogo em `src/backend/Versatus.ForcaVendas.Application/Catalogo/IClientCatalogRepository.cs`, `src/backend/Versatus.ForcaVendas.Application/Catalogo/IProductCatalogRepository.cs`, `src/backend/Versatus.ForcaVendas.Application/Catalogo/ClientSummary.cs` e `src/backend/Versatus.ForcaVendas.Application/Catalogo/ProductSummary.cs`
- [ ] T033 [P] [US2] Endurecer validacoes de pedido em `src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoRequestValidator.cs`
- [ ] T034 [P] [US2] Ajustar DTOs e comando de criacao em `src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoRequest.cs` e `src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoCommand.cs`
- [ ] T035 [P] [US2] Ajustar resposta de pedido em `src/backend/Versatus.ForcaVendas.Api/Pedidos/PedidoResponse.cs`
- [ ] T036 [US2] Atualizar agregados de dominio em `src/backend/Versatus.ForcaVendas.Domain/Pedidos/Pedido.cs`, `src/backend/Versatus.ForcaVendas.Domain/Pedidos/PedidoItem.cs` e `src/backend/Versatus.ForcaVendas.Domain/Pedidos/PedidoParcela.cs`
- [ ] T037 [US2] Atualizar servico de regra de pagamento em `src/backend/Versatus.ForcaVendas.Domain/Pedidos/Services/IPaymentConditionService.cs` e `src/backend/Versatus.ForcaVendas.Infrastructure/Data/Services/MockPaymentConditionService.cs`
- [ ] T038 [US2] Ajustar mapeamento/persistencia de pedido em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs`
- [ ] T039 [US2] Ajustar endpoint e cache de pedidos em `src/backend/Versatus.ForcaVendas.Api/Program.cs`, `src/backend/Versatus.ForcaVendas.Api/Pedidos/IPedidoCache.cs` e `src/backend/Versatus.ForcaVendas.Api/Pedidos/InMemoryPedidoCache.cs`
- [ ] T040 [P] [US2] Integrar jornada de catalogo/pedido no frontend em `src/frontend/app/src/lib/vendaApi.ts`, `src/frontend/app/src/lib/api.ts` e `src/frontend/app/src/types/vendas.ts`
- [ ] T041 [P] [US2] Ajustar telas de busca e montagem de pedido em `src/frontend/app/src/components/vendas/ClientSearch.tsx`, `src/frontend/app/src/components/vendas/ItemModal.tsx` e `src/frontend/app/src/components/vendas/OrderTable.tsx`

**Checkpoint**: US2 funcional e testavel isoladamente com JWT/tenant.

---

## Phase 5: User Story 3 - Despacho assincrono para ERP e retorno de status (Prioridade: P3)

**Goal**: despachar pedido para broker e atualizar historico por retorno processado/erro com idempotencia.

**Teste independente**: pedido ja criado recebe eventos de retorno sucesso/erro/duplicado/fora de ordem e preserva consistencia.

### Tests US3

- [ ] T042 [P] [US3] Criar testes de contrato JSON Schema para eventos em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidoEventosContractTests.cs`
- [ ] T043 [P] [US3] Criar testes de integracao de transicao de status/idempotencia (incluindo `enviado -> aguardando_processamento`) em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosIntegracaoStatusTests.cs`
- [ ] T044 [P] [US3] Criar testes de consumidor worker para sucesso/erro em `src/backend/Versatus.ForcaVendas.Api.Tests/WorkerPedidosConsumerTests.cs`

### Implementacao US3

- [ ] T045 [P] [US3] Criar contratos de mensageria no backend em `src/backend/Versatus.ForcaVendas.Infrastructure/Messaging/PedidoEnviadoEvent.cs` e `src/backend/Versatus.ForcaVendas.Infrastructure/Messaging/PedidoResultadoEvent.cs`
- [ ] T046 [P] [US3] Implementar publisher de despacho em `src/backend/Versatus.ForcaVendas.Infrastructure/Messaging/PedidoIntegrationPublisher.cs`
- [ ] T047 [US3] Implementar trilha de idempotencia no banco em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/EventoIntegracaoPedidoEntity.cs` e `src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs`
- [ ] T048 [US3] Criar migracao de evento de integracao em `src/backend/Versatus.ForcaVendas.Infrastructure/Data/Migrations/`
- [ ] T049 [US3] Integrar despacho no fluxo de pedido em `src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoCommand.cs` e `src/backend/Versatus.ForcaVendas.Api/Program.cs`
- [ ] T050 [US3] Implementar consumidor ERP no worker em `src/worker/Versatus.ForcaVendas.Worker/Consumers/PedidoResultadoConsumer.cs` e registrar em `src/worker/Versatus.ForcaVendas.Worker/Program.cs`
- [ ] T051 [US3] Ajustar processamento em background no worker em `src/worker/Versatus.ForcaVendas.Worker/Worker.cs`
- [ ] T052 [US3] Atualizar status/historico no dominio em `src/backend/Versatus.ForcaVendas.Domain/Pedidos/PedidoStatus.cs` e `src/backend/Versatus.ForcaVendas.Domain/Pedidos/Pedido.cs`
- [ ] T053 [P] [US3] Exibir status de integracao no frontend em `src/frontend/app/src/store/uiStore.ts`, `src/frontend/app/src/lib/vendaApi.ts` e `src/frontend/app/src/app/page.tsx`

**Checkpoint**: US3 valida fluxo assincrono E2E com idempotencia.

---

## Phase 6: User Story 4 - Demonstracao guiada do fluxo completo (Prioridade: P4)

**Goal**: disponibilizar roteiro de demo reproduzivel cobrindo P1-P3 e cenarios de erro controlado.

**Teste independente**: executar roteiro de demo com evidencias (logs, status e respostas API) sem ajustes manuais de codigo.

### Tests e Evidencias US4

- [ ] T054 [P] [US4] Criar teste de fumaca E2E no backend em `src/backend/Versatus.ForcaVendas.Api.Tests/E2EDemoSmokeTests.cs`
- [ ] T055 [P] [US4] Atualizar checklist de requisitos da feature em `specs/001-fluxo-e2e-vendas/checklists/requirements.md` com criterio de aceite por historia

### Implementacao US4

- [ ] T056 [US4] Consolidar roteiro de demonstracao em `specs/001-fluxo-e2e-vendas/quickstart.md` (caminho feliz + erro ERP)
- [ ] T057 [US4] Atualizar orientacao operacional em `docs/ISSUE_PR_TEXTS_2026-03-25.md` com template de evidencias para PR de demo
- [ ] T058 [US4] Ajustar pagina de login de demo em `src/frontend/app/src/app/(auth)/login/page.tsx` e `src/frontend/app/src/components/auth/LoginForm.tsx`

**Checkpoint**: US4 permite demonstracao guiada do fluxo completo para stakeholders.

---

## Final Phase: Polish & Cross-Cutting

**Objetivo**: fechar qualidade, observabilidade e documentacao transversal.

- [ ] T059 [P] Executar e estabilizar suite backend `dotnet test` ajustando `src/backend/Versatus.ForcaVendas.Api.Tests/AuthTests.cs`, `src/backend/Versatus.ForcaVendas.Api.Tests/CatalogTests.cs` e `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosTests.cs` quando necessario
- [ ] T060 [P] Revisar telemetria e healthcheck em `src/backend/Versatus.ForcaVendas.Api/Health/RedisHealthCheck.cs` e `src/backend/Versatus.ForcaVendas.Api/Program.cs`
- [ ] T061 [P] Remover divida tecnica obvia de stubs em `src/backend/Versatus.ForcaVendas.Application/Class1.cs`, `src/backend/Versatus.ForcaVendas.Domain/Class1.cs` e `src/backend/Versatus.ForcaVendas.Infrastructure/Class1.cs`
- [ ] T062 Atualizar documentacao principal em `README.md` e `specs/001-fluxo-e2e-vendas/spec.md` com status final da entrega
- [ ] T066 [P] Criar benchmark de catalogo (SC-003) e publicar evidencias de p95 em `src/backend/Versatus.ForcaVendas.Api.Tests/CatalogPerformanceTests.cs` e `specs/001-fluxo-e2e-vendas/quickstart.md`
- [ ] T067 [P] Criar benchmark de criacao/historico de pedido (SC-004) e publicar evidencias de p95 em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosPerformanceTests.cs` e `specs/001-fluxo-e2e-vendas/quickstart.md`
- [ ] T068 [P] Criar teste de latencia de atualizacao de status (SC-005) com evidencia em `src/backend/Versatus.ForcaVendas.Api.Tests/PedidosIntegracaoStatusTests.cs` e `specs/001-fluxo-e2e-vendas/quickstart.md`

---

## Dependencies & Execution Order

### Dependencias de fase

- Phase 1 -> Phase 2
- Phase 2 bloqueia o inicio de todas as historias
- Phase 3 (US1) e obrigatoria para autenticacao/base de tenant das demais
- Phase 4 (US2) depende de US1 para contexto autenticado
- Phase 5 (US3) depende de US2 para existencia de pedido e historico
- Phase 6 (US4) depende de US1+US2+US3 concluidas
- Final Phase depende das fases anteriores selecionadas para release

### Dependencias entre historias

- US1 (P1): sem dependencia de outras historias
- US2 (P2): depende de US1
- US3 (P3): depende de US2 (e implicitamente de US1)
- US4 (P4): depende de US1, US2 e US3

### Ordem critica sugerida

ok podemos continuar.- T006 -> T010 -> T011 -> T064 -> T014 -> T021 -> T028 -> T036 -> T039 -> T045 -> T047 -> T049 -> T050 -> T052 -> T054 -> T059 -> T066 -> T067 -> T068 -> T062

---

## Parallel Opportunities

### Paralelismo na fundacao

- T007, T008, T009, T012 e T013 podem rodar em paralelo apos T006
- T011 pode ocorrer em paralelo com T012/T013 apos definicao de modelo base (T010)

### Paralelismo por historia

- US1: T014-T018 em paralelo; T019-T020-T025-T026 em paralelo antes da consolidacao T021-T024-T027
- US2: T028-T031 em paralelo; T032-T035 e T040-T041 em paralelo antes de T036-T039
- US3: T042-T044 em paralelo; T045-T046 e T053 em paralelo antes de T047-T052
- US4: T054 e T055 em paralelo antes de T056-T058

---

## Implementation Strategy

### MVP primeiro (entrega minima)

1. Concluir Phase 1 e Phase 2
2. Concluir Phase 3 (US1)
3. Validar US1 isoladamente e abrir demo tecnica inicial

### Entrega incremental

1. Adicionar US2 e validar fluxo de pedido
2. Adicionar US3 e validar assincronia/idempotencia
3. Fechar US4 com roteiro de negocio para stakeholders
4. Executar Final Phase antes de merge final

### Estrategia de time paralelo

1. Dev A: backend auth/licenca (US1)
2. Dev B: catalogo/pedidos + frontend vendas (US2)
3. Dev C: mensageria + worker + status (US3)
4. Reunir em PRs pequenos por fase/historia, sempre com suite de testes verde
