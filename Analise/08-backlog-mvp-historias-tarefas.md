# 08 — Backlog MVP em Historias e Tarefas (GitHub)

## Objetivo

Servir como base de criacao de Issues no GitHub para conduzir o MVP em incrementos pequenos,
controlados e rastreaveis.

---

## Convencoes

- Label de epic: `epic:mvp`
- Label de tipo: `type:story`, `type:task`, `type:bug`
- Label de modulo: `mod:auth`, `mod:pedidos`, `mod:catalogo`, `mod:integracao`, `mod:frontend`, `mod:devops`
- Label de prioridade: `prio:p0`, `prio:p1`, `prio:p2`

---

## Epic MVP

Titulo:

`[MVP] Epic: Forca de vendas web - demonstracao cliente`

Meta:

Demonstrar fluxo completo login -> criacao pedido -> envio -> retorno de status.

---

## Stories e Tasks

## Story MVP-01 - Autenticacao por tenant

Titulo:

`[MVP][Auth] Historia: login com tenant e JWT`

Criterios de aceite:

1. Usuario autentica com tenant valido.
2. Token JWT e refresh token retornados.
3. Usuario sem tenant valido recebe erro controlado.

Tasks:

- `[Task][Auth] Criar endpoint de login`
- `[Task][Auth] Implementar refresh token`
- `[Task][Auth] Adicionar middleware de tenant`

---

## Story MVP-02 - Controle de usuarios simultaneos

Titulo:

`[MVP][Licenca] Historia: bloquear acessos acima do limite contratado`

Criterios de aceite:

1. Tenant com limite 4 permite ate 4 logins ativos.
2. 5o login e bloqueado com mensagem de plano.
3. Logout e heartbeat liberam sessoes corretamente.

Tasks:

- `[Task][Licenca] Persistir limite por tenant`
- `[Task][Licenca] Controlar sessoes ativas no Redis`
- `[Task][Licenca] Bloquear login acima do limite`
- `[Task][Licenca] Criar endpoint admin de sessoes ativas`

---

## Story MVP-03 - Catalogo basico

Titulo:

`[MVP][Catalogo] Historia: listar clientes e produtos para pedido`

Criterios de aceite:

1. Lista de clientes disponivel na tela de pedido.
2. Lista de produtos disponivel com busca simples.
3. Tempo de resposta aceitavel em ambiente de demo.

Tasks:

- `[Task][Catalogo] Endpoint de clientes`
- `[Task][Catalogo] Endpoint de produtos`
- `[Task][Catalogo] Cache de leitura`

---

## Story MVP-04 - Criacao de pedido

Titulo:

`[MVP][Pedidos] Historia: criar pedido com itens e parcelas`

Criterios de aceite:

1. Pedido salvo com cabecalho, itens e parcelas.
2. Totais calculados corretamente.
3. Pedido aparece no historico.

Tasks:

- `[Task][Pedidos] Modelagem pedido/item/parcela`
- `[Task][Pedidos] Endpoint criar pedido`
- `[Task][Pedidos] Regras de calculo e validacao`
- `[Task][Pedidos] Endpoint listar e detalhar`

---

## Story MVP-05 - Integracao com ERP legado

Titulo:

`[MVP][Integracao] Historia: enviar pedido e receber status de processamento`

Criterios de aceite:

1. Pedido enviado gera evento.
2. Adaptador processa e retorna status.
3. Status processado/erro visivel no historico.

Tasks:

- `[Task][Integracao] Publicar evento pedido.enviado`
- `[Task][Integracao] Worker adaptador ERP`
- `[Task][Integracao] Consumir evento de retorno`

---

## Story MVP-06 - Frontend de demonstracao

Titulo:

`[MVP][Frontend] Historia: navegar no fluxo completo de vendas`

Criterios de aceite:

1. Usuario loga.
2. Usuario cria pedido.
3. Usuario acompanha status no historico.

Tasks:

- `[Task][Frontend] Tela login`
- `[Task][Frontend] Dashboard pedidos`
- `[Task][Frontend] Tela novo pedido`
- `[Task][Frontend] Tela historico`

---

## Story MVP-07 - Qualidade e demo

Titulo:

`[MVP][Qualidade] Historia: garantir estabilidade minima para apresentar ao cliente`

Criterios de aceite:

1. Fluxo principal validado por roteiro de teste.
2. Logs e erros legiveis para suporte.
3. Roteiro de demo pronto.

Tasks:

- `[Task][Qualidade] Criar roteiro de teste MVP`
- `[Task][Qualidade] Ajustar mensagens de erro`
- `[Task][Qualidade] Criar script de apresentacao`

---

## Sincronizacao atual com GitHub

Data de referencia: 2026-03-25

Resumo atual:

- Issues abertas: 23
- Issues fechadas: 8
- Historias com evidencias de implementacao no codigo: MVP-01, MVP-02
- Modulos ainda sem implementacao identificada no backend: Catalogo, Pedidos, Integracao
- Frontend MVP ainda pendente de execucao

Historias e tarefas sincronizadas:

- `MVP-01 Auth` -> issue `#1` aberta
	- `#8 Criar endpoint de login` -> fechada
	- `#9 Implementar refresh token` -> fechada
	- `#10 Adicionar middleware de tenant` -> fechada
	- Observacao: historia candidata a fechamento apos conferencia final de aceite

- `MVP-02 Licenca` -> issue `#2` aberta
	- `#11 Persistir limite por tenant` -> fechada
	- `#12 Controlar sessoes ativas no Redis` -> fechada
	- `#13 Bloquear login acima do limite` -> fechada
	- `#14 Criar endpoint admin de sessoes ativas` -> fechada
	- Observacao: historia candidata a fechamento apos conferencia final de aceite

- `MVP-03 Catalogo` -> issue `#3` aberta
	- `#15 Endpoint de clientes` -> fechada
	- `#16 Endpoint de produtos` -> implementada no codigo, candidata a fechamento
	- `#17 Cache de leitura no Redis` -> implementada no codigo, candidata a fechamento

- `MVP-04 Pedidos` -> issue `#4` aberta
	- `#18 Modelagem pedido/item/parcela` -> aberta
	- `#19 Endpoint criar pedido` -> aberta
	- `#20 Regras de calculo e validacao` -> aberta
	- `#21 Endpoint listar e detalhar pedido` -> aberta

- `MVP-05 Integracao` -> issue `#5` aberta
	- `#22 Publicar evento pedido.enviado` -> aberta
	- `#23 Worker adaptador ERP` -> aberta
	- `#24 Consumir evento de retorno` -> aberta

- `MVP-06 Frontend` -> issue `#6` aberta
	- `#25 Tela de login` -> aberta
	- `#26 Dashboard de pedidos` -> aberta
	- `#27 Tela novo pedido` -> aberta
	- `#28 Tela historico com status ERP` -> aberta

- `MVP-07 Qualidade` -> issue `#7` aberta
	- `#29 Criar roteiro de teste MVP` -> aberta
	- `#30 Ajustar mensagens de erro para usuario` -> aberta
	- `#31 Criar script de apresentacao` -> aberta

Gaps identificados:

- Auditoria de login/logout foi iniciada no codigo, mas ainda nao aparece formalizada neste backlog MVP nem como issue dedicada no GitHub.
- Recomendada a criacao de uma task tecnica separada para rastrear persistencia, consulta e testes da auditoria.

Proximo bloco recomendado:

1. Encerrar as historias `#1` e `#2` apos validacao final dos criterios de aceite.
2. Fechar `#16` e `#17` para concluir a historia de catalogo.
3. Em seguida abrir execucao concentrada de `MVP-04 Pedidos`, que e o maior bloqueador funcional do MVP.

---

## Regras de divisao para manter controle

1. Story maximo 3 dias uteis.
2. Task maximo 1 dia util.
3. PR pequeno e objetivo.
4. Merge somente com CI verde e review.
