# Especificacao de Funcionalidade: Fluxo E2E de Forca de Vendas MVP

**Feature Branch**: `[001-fluxo-e2e-vendas]`  
**Criado em**: 2026-04-12  
**Status**: Draft  
**Entrada**: "Force Sales MVP end-to-end demonstrable flow: email/password login with internal tenant resolution, concurrent session/license control, catalog lookup, order creation, async ERP integration dispatch, and status feedback to history."

## User Scenarios & Testing

### User Story 1 - Acesso com controle de licenca por tenant (Prioridade: P1)

Como representante comercial, quero entrar no sistema informando apenas meu email e senha para que o sistema identifique automaticamente meu tenant e me aceite somente quando houver vaga de licenca disponivel, garantindo acesso seguro e aderente ao plano contratado.

**Decisao de design (ADR-001)**: O login recebe `email` + `senha`. O email e o identificador unico do usuario em toda a aplicacao (indice unico global na tabela `usuarios`, nao restrito por tenant). O `tenant_id` e resolvido internamente a partir do cadastro do usuario — o formulario de login nao expoe campo de tenant. Isso simplifica UX, evita erros de digitacao de codigo de tenant e elimina uma categoria de ataque de enumeracao de tenants.

**Por que esta prioridade**: Sem autenticacao e sem controle de concorrencia de sessao/licenca, o restante do fluxo nao pode ser executado de forma segura nem demonstrado ao cliente.

**Teste independente**: Pode ser testada isoladamente com tentativas de login com email/senha valido e invalido, disputa pelo ultimo seat, heartbeat e logout, sem depender de catalogo ou pedidos.

**Cenarios de Aceitacao**:

1. **Dado** um usuario com email cadastrado, tenant ativo e vagas de licenca disponiveis, **Quando** ele realiza login informando email e senha corretos, **Entao** recebe token JWT com claims de identidade e contexto de tenant resolvido internamente, com acesso aos endpoints protegidos.
2. **Dado** um tenant com limite de sessoes simultaneas atingido, **Quando** um novo login e tentado pelo mesmo tenant, **Entao** o acesso e negado com mensagem de limite de plano sem criar sessao adicional.
3. **Dado** uma sessao autenticada, **Quando** o usuario envia heartbeat e depois logout, **Entao** a sessao permanece valida durante uso e e liberada ao encerrar, permitindo novo acesso dentro do limite.

---

### User Story 2 - Consulta de catalogo e criacao de pedido (Prioridade: P2)

Como representante comercial autenticado, quero consultar clientes e produtos do meu tenant e registrar um pedido com itens e condicao de pagamento, para concluir uma venda no campo.

**Por que esta prioridade**: Essa historia entrega o nucleo transacional do MVP para demonstracao comercial, independente da resposta final do ERP.

**Teste independente**: Pode ser testada com massa de catalogo por tenant, criacao de pedido com validacao de totais e consulta do proprio pedido, sem depender do worker de integracao.

**Cenarios de Aceitacao**:

1. **Dado** um usuario autenticado em um tenant, **Quando** ele pesquisa clientes e produtos, **Entao** visualiza apenas dados daquele tenant com tempo de resposta adequado para demo.
2. **Dado** um pedido valido com cabecalho, itens e parcelas, **Quando** o usuario confirma o envio do pedido no sistema, **Entao** o pedido e persistido com identificador unico, totais calculados e status inicial de acompanhamento.
3. **Dado** um pedido recem-criado, **Quando** o usuario abre o historico de pedidos, **Entao** encontra o registro com status atual e dados resumidos para acompanhamento.

---

### User Story 3 - Despacho assincrono para ERP e retorno de status (Prioridade: P3)

Como representante comercial, quero que o pedido criado seja despachado para processamento no ERP de forma assincrona e que o resultado apareca no historico, para acompanhar conclusao ou rejeicao sem travar minha operacao.

**Por que esta prioridade**: Fecha o fluxo ponta a ponta demonstravel do MVP, comprovando desacoplamento com ERP e rastreabilidade de status.

**Teste independente**: Pode ser testada usando pedido ja existente no status inicial e simulando retorno de processamento/erro, validando transicoes de status e exibicao no historico.

**Cenarios de Aceitacao**:

1. **Dado** um pedido elegivel para integracao, **Quando** ele e despachado para o canal assincrono, **Entao** o sistema registra o evento de envio e altera o pedido para status `aguardando_processamento`.
2. **Dado** um retorno de processamento bem-sucedido do ERP, **Quando** o sistema recebe a confirmacao, **Entao** o pedido e atualizado para status processado com identificador de rastreio do documento.
3. **Dado** um retorno de falha do ERP, **Quando** o sistema recebe o motivo de rejeicao, **Entao** o pedido e atualizado para status de erro com mensagem compreensivel no historico.

---

### User Story 4 - Demonstracao guiada do fluxo completo (Prioridade: P4)

Como lider tecnico/comercial, quero executar um roteiro unico de demonstracao cobrindo login, pedido e retorno de status, para comprovar viabilidade do MVP para stakeholders.

**Por que esta prioridade**: Gera valor de validacao de negocio e de venda do projeto, mas depende das historias anteriores.

**Teste independente**: Pode ser testada como roteiro funcional de ponta a ponta em ambiente de demo com dados controlados, sem exigir cobertura de todos os cenarios de borda da operacao real.

**Cenarios de Aceitacao**:

1. **Dado** ambiente de demo preparado, **Quando** o operador segue o roteiro completo, **Entao** todas as etapas P1-P3 sao exibidas sem bloqueios funcionais criticos.
2. **Dado** um erro esperado de negocio no retorno do ERP, **Quando** ele ocorre durante a demo, **Entao** o sistema apresenta feedback claro e permite continuar navegacao no historico.

### Edge Cases

- Login com email desconhecido ou senha incorreta deve retornar 401 com mensagem generica ("credenciais invalidas"), sem revelar qual campo falhou nem se o email existe.
- Tenant inativo deve bloquear o acesso apos resolucao bem-sucedida do usuario, com mensagem de conta suspensa.
- Competicao de login simultaneo no ultimo seat disponivel deve resultar em apenas uma sessao aceita.
- Heartbeat nao enviado dentro da janela definida deve expirar a sessao e liberar seat automaticamente.
- Requisicao autenticada sem contexto de tenant valido deve ser rejeitada para evitar vazamento entre tenants.
- Busca de catalogo sem resultados deve retornar lista vazia consistente, sem erro de processamento.
- Pedido com item invalido (quantidade/preco/desconto fora da politica) deve ser recusado com mensagem de validacao.
- Retorno duplicado de status de integracao para o mesmo pedido nao deve gerar regressao de estado nem duplicidade de historico.
- Falha temporaria no canal assincrono deve manter o pedido rastreavel para reprocessamento sem perda de referencia.

## Requirements

### Functional Requirements

- **FR-001**: O sistema DEVE autenticar usuario por `email` + `senha` (sem campo tenant no formulario) e estabelecer contexto explicito de tenant resolvido internamente para toda sessao autenticada.
- **FR-002**: O sistema DEVE emitir credenciais de acesso e renovacao de sessao apos autenticacao bem-sucedida.
- **FR-003**: O sistema DEVE rejeitar autenticacao quando email/senha estiverem invalidos, usuario estiver inativo, tenant resolvido estiver inativo ou sem permissao de uso.
- **FR-004**: O sistema DEVE aplicar limite de usuarios simultaneos por tenant usando controle de sessoes ativas em Redis.
- **FR-005**: O sistema DEVE registrar heartbeat de sessao e liberar automaticamente seats expirados por inatividade.
- **FR-006**: O sistema DEVE liberar seat no logout e manter trilha de auditoria de login, heartbeat e logout.
- **FR-007**: O sistema DEVE impedir leitura e escrita entre tenants em todos os endpoints protegidos.
- **FR-008**: O sistema DEVE disponibilizar consulta de clientes por tenant com suporte a busca para o fluxo de venda.
- **FR-009**: O sistema DEVE disponibilizar consulta de produtos por tenant com informacoes suficientes para montar pedido.
- **FR-010**: O sistema DEVE permitir criacao de pedido contendo cabecalho, itens e condicao de pagamento.
- **FR-011**: O sistema DEVE validar regras minimas de consistencia do pedido (itens obrigatorios, totais e limites de desconto).
- **FR-012**: O sistema DEVE persistir pedido com identificador unico e status inicial rastreavel no historico.
- **FR-013**: O sistema DEVE disponibilizar consulta de historico e detalhe de pedidos para o tenant autenticado.
- **FR-014**: O sistema DEVE despachar pedido criado para processamento ERP por mecanismo assincrono desacoplado.
- **FR-015**: O sistema DEVE registrar ciclo de status de integracao com transicoes validas `rascunho -> enviado -> aguardando_processamento -> processado|erro`.
- **FR-016**: O sistema DEVE atualizar historico de pedidos quando receber retorno de processamento bem-sucedido, incluindo identificador de documento.
- **FR-017**: O sistema DEVE atualizar historico de pedidos quando receber retorno de erro, incluindo motivo de rejeicao orientado ao usuario.
- **FR-018**: O sistema DEVE garantir idempotencia no processamento de retornos de integracao para evitar duplicidade de atualizacao de status.

### Security and Tenant Isolation Requirements

- **ST-001**: Todo request protegido DEVE carregar contexto de tenant resolvido antes de acessar dados de negocio.
- **ST-002**: O sistema DEVE validar isolamento de tenant em operacoes de autenticacao, catalogo, pedidos e historico.
- **ST-003**: O controle de licenca por sessoes em Redis DEVE negar excesso de concorrencia e permitir recuperacao automatica por expurgo de sessao inativa.
- **ST-004**: Eventos de auditoria de sessao DEVE registrar tenant, usuario, tipo de evento e instante para rastreabilidade operacional.

### Integration Contract Requirements

- **IC-001**: O contrato de envio para integracao DEVE incluir identificador do pedido, tenant, dados comerciais essenciais e referencia temporal de envio.
- **IC-002**: O contrato de retorno de integracao DEVE aceitar dois resultados: processado com referencia de documento ou erro com motivo de rejeicao.
- **IC-003**: O sistema DEVE aplicar transicoes de status apenas para mudancas validas do ciclo de vida do pedido.
- **IC-004**: O sistema DEVE tratar eventos de retorno fora de ordem sem quebrar consistencia do status final.

### Observability Requirements

- **OB-001**: O fluxo DEVE gerar logs estruturados por tenant, pedido e correlacao de processamento ponta a ponta.
- **OB-002**: O sistema DEVE expor sinais operacionais minimos para autenticao, saturacao de licenca, criacao de pedido e latencia de atualizacao de status.
- **OB-003**: O sistema DEVE registrar falhas de integracao assincrona com informacoes suficientes para diagnostico e reprocessamento.

### Key Entities

- **Tenant**: Empresa contratante com isolamento de dados e parametros de licenca.
- **Usuario**: Identidade autenticada por email unico global, vinculada a um unico tenant para operar o fluxo de vendas.
- **SessaoAtiva**: Registro logico de concorrencia de login com expiracao e renovacao por heartbeat.
- **LicencaTenant**: Limite de usuarios simultaneos e estado de assinatura que controla permissao de acesso.
- **ClienteCatalogo**: Cliente disponivel para selecao no pedido dentro do tenant.
- **ProdutoCatalogo**: Produto comercial com atributos de busca e preco para montagem de pedido.
- **Pedido**: Entidade transacional contendo cabecalho, itens, parcelas e status de integracao.
- **PedidoItem**: Item comercial do pedido com quantidade, preco, desconto e total calculado.
- **PedidoParcela**: Condicao financeira associada ao pedido para pagamento.
- **EventoIntegracaoPedido**: Registro de envio/retorno assicrono para atualizar status do pedido no historico.

## Success Criteria

### Measurable Outcomes

- **SC-001**: 100% dos usuarios de demo conseguem autenticar com email/senha validos (tenant resolvido internamente) em ate 30 segundos na primeira tentativa.
- **SC-002**: Em teste de concorrencia de licenca, 100% das tentativas acima do limite contratado sao bloqueadas e 100% dos seats sao liberados apos logout ou expiracao.
- **SC-003**: Pelo menos 95% das consultas de catalogo (clientes/produtos) retornam em ate 1 segundo no ambiente de demonstracao.
- **SC-004**: Pelo menos 95% dos pedidos validos sao criados e aparecem no historico em ate 5 segundos.
- **SC-005**: Pelo menos 95% dos pedidos despachados recebem atualizacao de status no historico (processado ou erro) em ate 2 minutos em condicoes normais da demo.
- **SC-006**: 100% das validacoes de isolamento entre tenants bloqueiam acesso cruzado em testes funcionais de seguranca.
- **SC-007**: Ao final do roteiro de demo, pelo menos 90% dos observadores confirmam que o fluxo ponta a ponta foi compreendido sem intervencao tecnica adicional.

### Quality Gate Evidence

- **QG-001**: Devem existir testes automatizados de unidade e integracao cobrindo autenticacao, controle de licenca, catalogo, criacao de pedido e transicoes de status.
- **QG-002**: Devem existir testes de contrato para envio e retorno da integracao assincrona, incluindo cenarios de sucesso, erro, duplicidade e fora de ordem.
- **QG-003**: O pipeline de validacao deve aprovar execucao de testes e verificacoes de qualidade sem falhas criticas antes de demonstracao.

## Assumptions

- O MVP foca demonstracao controlada do fluxo principal e nao cobre toda a complexidade fiscal do ERP legado.
- O usuario conhece seu email e senha no momento do login; o tenant e resolvido internamente a partir do cadastro ativo de usuario e assinatura/licenca para ambiente de demo.
- O catalogo necessario para demonstracao ja esta previamente sincronizado para consulta por tenant.
- O historico de pedidos precisa refletir estados de negocio compreensiveis para vendedor, sem detalhamento contabil profundo.
- O mecanismo assincrono de integracao pode sofrer indisponibilidade temporaria, mas sem perda definitiva de referencia do pedido.
- Fluxos de offline parcial e PWA permanecem fora do escopo estrito deste corte de especificacao de demonstracao E2E.
