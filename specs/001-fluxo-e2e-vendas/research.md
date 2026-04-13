# Research - Fluxo E2E de Forca de Vendas MVP

## Decisao 1: Broker padrao do MVP para integracao assincrona
- Decision: usar RabbitMQ como broker principal para o MVP, com topicos/filas para `pedido.enviado.v1` e `pedido.resultado.v1`.
- Rationale: ja existe `docker-compose.yml` com RabbitMQ de desenvolvimento e a arquitetura alvo em `docs/sdd/04-interfaces-integracao.md` prioriza mensageria para desacoplamento do ERP.
- Alternatives considered: SQS (adiado para fase cloud posterior), chamada HTTP sincrona para ERP (rejeitada por acoplamento e indisponibilidade).

## Decisao 2: Estrategia de idempotencia no retorno de status
- Decision: persistir chave de deduplicacao por evento de retorno (`tenantId + pedidoId + sourceEventId`) antes de aplicar transicao de status no pedido.
- Rationale: requisito IC-004 exige robustez para eventos duplicados/fora de ordem; deduplicacao protege historico e evita regressao de estado.
- Alternatives considered: apenas comparar status atual (insuficiente contra repeticao do mesmo evento), lock distribuido sem persistencia (fragil a restart).

## Decisao 3: Regras de transicao de status de pedido
- Decision: transicoes validas no MVP: `rascunho -> enviado -> processado|erro` e atualizacoes repetidas no mesmo estado sao no-op.
- Rationale: ja existem seeds de status em `PedidosDbContext` (`rascunho`, `enviado`, `processado`, `erro`) e o spec exige rastreabilidade e consistencia.
- Alternatives considered: permitir transicao livre por codigo (rejeitada por risco de inconsistencias), remover estado `enviado` (rejeitada por perda de visibilidade operacional).

## Decisao 4: Observabilidade ponta a ponta
- Decision: padronizar logs estruturados com `tenantId`, `pedidoId`, `sessionId`, `correlationId`, `eventType`; expor metricas de login negado por limite, latencia de criacao de pedido e atraso de processamento assincrono.
- Rationale: atende OB-001..OB-003 e Principle V da constituicao.
- Alternatives considered: logs apenas textuais sem correlacao (rejeitado por baixa diagnostabilidade), observabilidade somente no backend API (rejeitada por nao cobrir worker).

## Decisao 5: Escopo MVP e independencia de historias
- Decision: manter US1, US2 e US3 implementaveis e testaveis de forma independente, com US4 restrita a roteiro e evidencia de demo.
- Rationale: Principle I exige fatias de valor demonstraveis; reduz risco de bloqueio entre times frontend/backend/worker.
- Alternatives considered: entrega unica com tudo junto (rejeitada por alto risco de regressao e baixa previsibilidade).
