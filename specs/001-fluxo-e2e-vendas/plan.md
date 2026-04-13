# Implementation Plan: Fluxo E2E de Forca de Vendas MVP

**Branch**: `fix/swagger-bearer-auth` | **Date**: 2026-04-12 | **Spec**: `specs/001-fluxo-e2e-vendas/spec.md`
**Input**: Especificacao de funcionalidade em `specs/001-fluxo-e2e-vendas/spec.md`

## Summary

Entregar um fluxo E2E demonstravel e independente por historia: autenticacao por email+senha com resolucao interna de tenant e limite de sessoes via Redis, consulta de catalogo por tenant, criacao e historico de pedidos no PostgreSQL, despacho assincrono para ERP via broker e retorno idempotente de status. A estrategia usa o monolito modular .NET 8 existente para API e dominio, Next.js para UX de demonstracao, e Worker .NET 8 para adaptacao de integracao legado.

## Technical Context

**Language/Version**: C# 12/.NET 8 (API, Application, Domain, Infrastructure, Worker), TypeScript 5 com React 19 e Next.js 16 (frontend)  
**Primary Dependencies**: ASP.NET Core Minimal APIs, EF Core 8, Npgsql, StackExchange.Redis, MediatR, FluentValidation, JWT Bearer, Prometheus.AspNetCore, xUnit/FluentAssertions; frontend com TanStack Query, Zustand, Zod, React Hook Form, Axios  
**Storage**: PostgreSQL (pedidos, status, usuarios, assinaturas, auditoria), Redis (controle de sessao/seats e heartbeat), RabbitMQ (mensageria assincrona de integracao)  
**Testing**: `dotnet test src/backend/Versatus.ForcaVendas.Api.Tests/` (unitario + integracao de endpoints), contratos por schema/OpenAPI e testes de consumidor/produtor de eventos no backend/worker  
**Target Platform**: Linux/Windows containers para backend e worker; navegador moderno para frontend Next.js  
**Project Type**: Aplicacao web SaaS multi-tenant com API + frontend + worker assincorno  
**Performance Goals**: catalogo p95 <= 1s, criacao/listagem de pedido p95 <= 5s, atualizacao de status de integracao <= 2 minutos em cenario de demo  
**Constraints**: escopo MVP-first, independencia de historias P1/P2/P3, isolamento estrito por tenant, fail-closed sem tenant valido, idempotencia em retornos de integracao, login com email+senha sem campo tenant no formulario, compatibilidade de contrato de login documentada por versao/janela de transicao  
**Scale/Scope**: piloto de demonstracao com tenants controlados, baixo volume inicial (centenas de pedidos/dia), foco em rastreabilidade ponta a ponta ao inves de otimizar throughput maximo

## Constitution Check (Pre-Phase 0)

*GATE: deve passar antes da pesquisa e ser revalidado apos design.*

- [x] **PASS - MVP slice explicito**: historias P1/P2/P3 sao entregues em fatias demonstraveis e independentes; P4 e somente roteiro de demonstracao sem bloquear as demais.
- [x] **PASS - Isolamento de tenant e licenciamento**: `TenantContextMiddleware`, `ITenantContext`, filtro por tenant em consultas de pedido e `ISessionStore` via Redis definem fronteiras testaveis para ST-001..ST-004.
- [x] **PASS - Impacto em contratos de integracao**: contratos REST e eventos assincronos foram listados em `contracts/`, incluindo versao e regras de compatibilidade para transicoes de status.
- [x] **PASS - Quality gates definidos**: suite existente de auth/catalogo/pedidos sera expandida com cenarios de despacho, retorno de status, duplicidade e fora de ordem; CI deve manter verde.
- [x] **PASS - Observabilidade definida**: logs estruturados com `tenantId`, `pedidoId`, `correlationId`; metricas de login negado por seat, latencia de criacao e lag de processamento assincrono.
- [x] **PASS - Traceabilidade de workflow**: plano explicita decomposicao por historia e estrategia de PRs pequenos por modulo (API, Infrastructure, Worker, Frontend).

## Project Structure

### Documentacao da feature

```text
specs/001-fluxo-e2e-vendas/
|-- plan.md
|-- research.md
|-- data-model.md
|-- quickstart.md
|-- contracts/
|   |-- rest-e2e-vendas.openapi.yaml
|   `-- eventos-integracao-pedidos.schema.json
`-- checklists/
```

### Codigo-fonte real (repositorio)

```text
src/
|-- backend/
|   |-- Versatus.ForcaVendas.Api/
|   |   |-- Program.cs
|   |   |-- Middleware/
|   |   `-- Pedidos/
|   |-- Versatus.ForcaVendas.Application/
|   |   |-- Catalogo/
|   |   |-- Licenca/
|   |   |-- Pedidos/
|   |   `-- Sessao/
|   |-- Versatus.ForcaVendas.Domain/
|   |   |-- Auditoria/
|   |   |-- Catalogo/
|   |   |-- Licenca/
|   |   `-- Pedidos/
|   |-- Versatus.ForcaVendas.Infrastructure/
|   |   |-- Data/
|   |   `-- Redis/
|   `-- Versatus.ForcaVendas.Api.Tests/
|-- frontend/app/
|   `-- src/
|       |-- app/
|       |-- components/
|       |-- lib/
|       |-- store/
|       `-- types/
`-- worker/Versatus.ForcaVendas.Worker/
```

**Structure Decision**: manter monolito modular no backend com worker separado para integracao ERP assincrona e frontend desacoplado em Next.js, sem criar novos servicos alem dos ja existentes no repositorio.

## Phase 0 - Pesquisa e Decisoes

Saida: `specs/001-fluxo-e2e-vendas/research.md`

1. Confirmar estrategia de broker para despacho/retorno: RabbitMQ como padrao MVP (ja presente em `docker-compose.yml`) com contratos versionados.
2. Definir idempotencia de retorno do ERP por chave de deduplicacao (`tenantId + pedidoId + sourceEventId`).
3. Consolidar estrategia de observabilidade para correlacao API -> broker -> worker -> API.

## Phase 1 - Design, Dados e Contratos

Saidas: `data-model.md`, `quickstart.md`, `contracts/`

1. Modelar entidades e transicoes para sessao/licenca/pedido/evento de integracao.
2. Definir contratos REST necessarios para P1/P2/P3 e contratos de evento para despacho e retorno assincrono.
3. Atualizar quickstart com fluxo local completo: dependencias, API, frontend, worker e roteiro E2E.
4. Atualizar contexto do agente com tecnologias realmente usadas no plano.

## Phase 2 - Planejamento de Implementacao (MVP-first)

1. **US1 (P1) - Login/licenca com tenant resolvido internamente**
   - Consolidar endpoints de login/refresh/heartbeat/logout e auditoria com payload de login em email+senha (sem tenant no input).
   - Fortalecer regras de expiracao de sessao e liberacao de seat.
   - Testes: concorrencia no ultimo seat, expiracao por heartbeat ausente, isolamento de tenant.
2. **US2 (P2) - Catalogo e pedidos**
   - Garantir consulta por tenant em clientes/produtos com limites e validacao.
   - Criar/listar/detalhar pedidos com totais e status inicial rastreavel.
   - Testes: validacao de pedido invalido, tenant sem vazamento, consulta de historico.
3. **US3 (P3) - Integracao assincrona ERP**
   - Publicar evento de pedido enviado apos persistencia.
   - Implementar consumidor no worker e retorno de `processado`/`erro` com idempotencia.
   - Atualizar historico mantendo transicoes validas (`rascunho -> enviado -> aguardando_processamento -> processado|erro`).
   - Testes: sucesso, rejeicao, duplicidade e fora de ordem.
4. **US4 (P4) - Roteiro de demo**
   - Script de validacao ponta a ponta com dados seed e evidencias de logs/metricas.

## Constitution Check (Post-Phase 1)

- [x] **PASS - Principle I (MVP Value Slice First)**: planejamento preserva independencia de historias P1-P3 e reduz risco de big-bang.
- [x] **PASS - Principle II (Tenant Isolation and Session Licensing)**: fronteiras de tenant, regras de seat e fail-closed permanecem explicitas no design.
- [x] **PASS - Principle III (Contract-Driven Integration and Status Flow)**: contratos REST/eventos versionados e regras de transicao/idempotencia documentados em `contracts/`.
- [x] **PASS - Principle IV (Test and CI Quality Gates)**: matriz de testes por historia e por contrato definida; merge condicionado a CI verde.
- [x] **PASS - Principle V (Observability and Operational Traceability)**: correlacao e sinais operacionais minimos definidos para diagnostico.

## Complexity Tracking

Sem violacoes de constituicao que exijam excecao formal.

---

## Roadmap de Execucao por PR Pequeno (US1-US4)

**Objetivo**: cada PR deve ser entregavel em <= 1 dia de trabalho, com CI verde e impacto isolado por historia.  
**Regra de granularidade**: cada tarefa individual cabe em <= 1 dia; cada historia e entregue em <= 3 dias com merge incremental.

### PR-US1: Autenticacao e licenciamento (P1 - Prioritario)

**Escopo**: endpoints de login/heartbeat/logout/eviction, controle de seats em Redis, auditoria de sessao.  
**Detalhe de login**: payload aceita `email` + `senha` sem campo tenant; tenant resolvido internamente por lookup de email.  
**Arquivos principais**: `Api/Auth/`, `Application/Sessao/`, `Application/Licenca/`, `Infrastructure/Redis/`  
**Testes de aceite**: T014, T015, T016, T017, T018 passando com CI verde.  
**Criterio de merge**: login por email+senha funcional, tenant resolvido internamente, sem vazamento entre tenants.

### PR-US2: Catalogo e pedidos (P2)

**Escopo**: endpoints de clientes/produtos e CRUD de pedidos com totais e status inicial rastreavel.  
**Dependencia**: requer PR-US1 mergeado (contexto autenticado com tenantId no JWT).  
**Arquivos principais**: `Api/Pedidos/`, `Application/Catalogo/`, `Domain/Pedidos/`, `Infrastructure/Data/`  
**Testes de aceite**: T028, T029, T030, T031 passando com CI verde.  
**Criterio de merge**: catalogo filtrado estritamente por tenant, pedido com totais calculados e status inicial `rascunho`.

### PR-US3: Integracao assincrona ERP (P3)

**Escopo**: publicacao de evento no broker, consumidor no worker, retorno idempotente com transicoes de status.  
**Dependencia**: requer PR-US2 mergeado (pedido existente e historico de status).  
**Arquivos principais**: `Infrastructure/Messaging/`, `Worker/Consumers/`, `Infrastructure/Data/EventoIntegracaoPedidoEntity.cs`  
**Testes de aceite**: T042, T043, T044 passando com CI verde.  
**Criterio de merge**: idempotencia confirmada em retorno duplicado e fora de ordem; transicoes validas preservadas.

### PR-US4: Roteiro de demo guiada (P4)

**Escopo**: quickstart atualizado com roteiro guiado, teste de fumaca E2E e checklist de aceite por historia.  
**Dependencia**: requer PR-US1 + PR-US2 + PR-US3 mergeados.  
**Arquivos principais**: `specs/001-fluxo-e2e-vendas/quickstart.md`, `Api.Tests/E2EDemoSmokeTests.cs`  
**Testes de aceite**: T054, T055 passando com CI verde.  
**Criterio de merge**: roteiro executavel sem modificacao de codigo, evidencias de logs/metricas capturadas.

### Regras transversais de merge

- Cada PR contem alteracoes em <= 5 arquivos de producao e <= 3 arquivos de teste.
- Toda mudanca de dominio ou infraestrutura e acompanhada de teste correspondente no mesmo PR.
- Contratos REST e de eventos nao sao alterados sem bumpar versao ou adicionar janela de transicao documentada.
- Branches de historia sao criadas a partir de `feature/001-fluxo-e2e`; merge via PR para `develop` com CI obrigatorio.
