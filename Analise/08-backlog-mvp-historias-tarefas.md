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

## Regras de divisao para manter controle

1. Story maximo 3 dias uteis.
2. Task maximo 1 dia util.
3. PR pequeno e objetivo.
4. Merge somente com CI verde e review.
