# Data Model - Fluxo E2E de Forca de Vendas MVP

## Visao geral
Modelo centrado em isolamento por tenant, controle de sessao/licenca e ciclo de vida de pedido com integracao assincrona.

## Entidades principais

### TenantSubscription (infra.assinaturas)
- Campos: `tenant_id (Guid, PK)`, `nome_empresa (string)`, `max_usuarios_simultaneos (int)`, `ativo (bool)`.
- Regras:
  - `max_usuarios_simultaneos > 0`.
  - login somente se `ativo = true`.
- Relacoes:
  - 1:N com Usuario.
  - 1:N logico com SessaoAtiva (Redis).

### Usuario (infra.usuarios)
- Campos: `id (Guid, PK)`, `tenant_id (Guid)`, `email (string)`, `password_hash (string)`, `role (string)`, `ativo (bool)`, `criado_em (datetimeoffset)`.
- Regras:
  - `email` e o identificador de login — indice **unico global** em `(email)`, independente de tenant.
  - `email` deve ser formato valido (RFC 5321) e armazenado em lowercase.
  - autenticacao exige `ativo = true` e `tenant.ativo = true`.
  - o formulario de login recebe apenas `email` + `senha`; o `tenant_id` e resolvido internamente a partir do cadastro do usuario.
  - nao e permitido dois usuarios com o mesmo email, mesmo que em tenants diferentes (unicidade global).

### SessaoAtiva (Redis)
- Campos logicos: `sessionId`, `tenantId`, `userId`, `loginAt`, `lastHeartbeatAt`, `expiresAt`.
- Regras:
  - heartbeat renova TTL.
  - sessao expirada libera seat automaticamente.
  - logout remove sessao e libera seat.
- Relacoes:
  - N:1 com TenantSubscription.
  - N:1 com Usuario.

### SessionAuditEvent (infra.audit_events)
- Campos: `id (string, PK)`, `user_id`, `tenant_id`, `event_type (login|heartbeat|logout|evict)`, `timestamp`, `ip_address`, `user_agent`.
- Regras:
  - registrar eventos criticos de sessao para rastreabilidade.

### Pedido (pedidos)
- Campos: `id (Guid, PK)`, `tenant_id (string)`, `cliente_id (string)`, `status_id (int FK)`, `criado_em`, `total_bruto`, `total_desconto`, `total_liquido`, `observacao`.
- Regras:
  - pedido deve ter pelo menos 1 item.
  - `total_liquido = total_bruto - total_desconto`.
  - somente acesso com `tenant_id` do contexto autenticado.
- Relacoes:
  - 1:N com PedidoItem.
  - 1:N com PedidoParcela.
  - N:1 com PedidoStatus.

### PedidoItem (pedido_itens)
- Campos: `id (Guid, PK)`, `pedido_id (Guid FK)`, `produto_id`, `sku`, `nome`, `quantidade`, `preco_unitario`, `desconto`, `total`.
- Regras:
  - `quantidade > 0`.
  - `preco_unitario >= 0`.
  - `desconto >= 0` e dentro da politica do tenant.
  - `total = quantidade * preco_unitario - desconto`.

### PedidoParcela (pedido_parcelas)
- Campos: `id (Guid, PK)`, `pedido_id (Guid FK)`, `numero`, `data_vencimento`, `valor`, `forma_pagamento`.
- Regras:
  - `numero >= 1`.
  - soma de parcelas deve corresponder a `total_liquido` dentro de tolerancia monetaria.

### PedidoStatus (pedido_status)
- Campos: `id (int, PK)`, `codigo`, `descricao`.
- Valores MVP: `rascunho`, `enviado`, `aguardando_processamento`, `processado`, `erro`.

### EventoIntegracaoPedido (novo - persistencia de rastreio/idempotencia)
- Campos propostos: `id (Guid, PK)`, `tenant_id`, `pedido_id`, `tipo_evento (enviado|processado|erro)`, `source_event_id`, `correlation_id`, `payload_json`, `ocorrido_em`, `processado_em`, `resultado_aplicacao`.
- Regras:
  - unicidade em (`tenant_id`, `pedido_id`, `source_event_id`).
  - evento duplicado deve ser marcado e ignorado sem alterar estado final valido.

## Transicoes de estado
- `rascunho -> enviado`: apos persistencia do pedido e publicacao do evento de despacho.
- `enviado -> aguardando_processamento`: confirmacao de aceite no canal assincrono para rastrear backlog de processamento.
- `aguardando_processamento -> processado`: retorno ERP com sucesso e `documentoVendaId`.
- `aguardando_processamento -> erro`: retorno ERP com rejeicao e motivo.
- `processado/erro -> processado/erro` (mesmo evento): no-op idempotente.
- transicoes fora de ordem devem ser registradas como inconsistencia controlada, sem corromper estado final.
