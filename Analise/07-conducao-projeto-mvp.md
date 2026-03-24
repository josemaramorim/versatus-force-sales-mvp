# 07 — Conducao do Projeto (MVP-first)

## Objetivo

Conduzir o projeto de forma organizada, com foco em:

1. Entregar um MVP rapido para demonstracao ao cliente.
2. Evoluir por incrementos curtos ate cobrir toda a documentacao.
3. Manter a documentacao sempre atualizada como fonte oficial de verdade.

---

## 1. Forma de trabalho

### 1.1 Principios

- MVP primeiro, perfeicao depois.
- Entregas pequenas e frequentes.
- Decisao tecnica sempre registrada.
- Escopo congelado por sprint curta.
- Criticos de negocio sempre validados antes da implementacao completa.

### 1.2 Cadencia

- Sprint curta: 1 semana.
- Ritual fixo semanal:
  - Planejamento (inicio da semana)
  - Checkpoint tecnico (meio da semana)
  - Demo do incremento (fim da semana)
  - Retro + atualizacao da documentacao (fim da semana)

### 1.3 Definicoes de pronto

Definition of Ready (DoR) para iniciar item:

- Problema de negocio claro.
- Regra funcional definida.
- Critero de aceite objetivo.
- Dependencias identificadas.

Definition of Done (DoD) para concluir item:

- Funciona em ambiente de desenvolvimento.
- Testes minimos executados (manual ou automatizado).
- Logs e mensagens de erro adequados.
- Documentacao atualizada.
- Item demonstrado em demo interna.

---

## 2. Escopo do MVP de demostracao

## 2.1 Objetivo do MVP

Demonstrar para o cliente um fluxo ponta a ponta de pedido, do login ao status de processamento.

## 2.2 Escopo fechado do MVP

- Login com tenant e usuario.
- Controle de usuarios simultaneos contratado por tenant.
- Catalogo basico (clientes e produtos).
- Criacao de pedido (cabecalho, itens, parcelas).
- Envio de pedido para fila de integracao.
- Retorno de status: processado ou erro.
- Tela de historico de pedidos.

## 2.3 Fora do MVP (fase posterior)

- Offline completo PWA.
- Painel gerencial completo.
- Financeiro detalhado do cliente.
- Onboarding self-service completo.
- Motor avancado de relatorios.

---

## 3. Plano de execucao por fases

## Fase A - Preparacao (Semana 1)

- Confirmar escopo MVP com cliente.
- Fechar contrato de integracao com ERP legado.
- Congelar regras de licenciamento simultaneo.
- Criar backlog inicial priorizado.

Entregavel:

- Documento de escopo MVP aprovado.

## Fase B - Fundacao tecnica (Semana 2)

- Base API .NET 8 pronta.
- Banco PostgreSQL + Redis prontos.
- Autenticacao JWT com tenant.
- Estrutura de logs e observabilidade minima.

Entregavel:

- Login funcional e ambiente estavel.

## Fase C - Fluxo de pedido MVP (Semanas 3 e 4)

- Catalogo basico.
- Cadastro e listagem de pedidos.
- Calculo de totais e validacoes essenciais.
- Frontend dashboard com fluxo completo.

Entregavel:

- Pedido criado e visivel no historico.

## Fase D - Integracao com legado (Semana 5)

- Publicacao de evento de pedido enviado.
- Worker adaptador integrado ao ERP legado.
- Atualizacao de status processado/erro.

Entregavel:

- Pedido atraves do fluxo completo ate retorno do ERP.

## Fase E - Ajuste final e demo (Semana 6)

- Ajustes de UX e estabilidade.
- Teste assistido com tenant piloto.
- Material de apresentacao para cliente.

Entregavel:

- MVP pronto para demonstracao oficial.

---

## 4. Conducao apos o MVP

Apos a demo, o projeto continua por ondas quinzenais de evolucao:

1. Onda 1: offline parcial + robustez de sincronizacao.
2. Onda 2: painel gerencial e indicadores de vendas.
3. Onda 3: financeiro do cliente e politicas comerciais.
4. Onda 4: onboarding de novos tenants e automacoes SaaS.

Cada onda termina com:

- Demo funcional.
- Atualizacao da documentacao.
- Replanejamento de prioridades.

---

## 5. Governanca de documentacao (obrigatoria)

## 5.1 Regra geral

Nenhuma entrega e considerada concluida sem atualizar a documentacao.

## 5.2 Quando atualizar

Atualizar documentacao sempre que houver:

- Mudanca de escopo.
- Decisao de arquitetura.
- Alteracao de regra de negocio.
- Alteracao de contrato de API/evento.
- Alteracao de cronograma.

## 5.3 Onde atualizar

- [Analise/06-app-forca-venda-web.md](Analise/06-app-forca-venda-web.md): arquitetura e estrategia do produto.
- [Analise/07-conducao-projeto-mvp.md](Analise/07-conducao-projeto-mvp.md): modelo de conducao, fases e governanca.
- [Analise/README.md](Analise/README.md): indice e ordem de leitura.

## 5.4 Checklist de atualizacao

Antes de encerrar cada sprint:

1. Escopo da semana entregue.
2. Pendencias registradas.
3. Riscos atualizados.
4. Decisoes novas documentadas.
5. Proximo passo definido.

---

## 6. Papeis e responsabilidades

- Produto/Negocio: prioriza backlog e valida valor de negocio.
- Engenharia Backend: API, integracao e regras de dominio.
- Engenharia Frontend: experiencia do usuario e fluxo comercial.
- DevOps/Infra: ambientes, deploy, observabilidade e confiabilidade.
- QA funcional: roteiro de validacao do MVP e aceite.

---

## 7. Riscos de execucao e mitigacao

- Escopo inflar antes da demo.
  - Mitigacao: congelar escopo MVP por 6 semanas.
- Dependencia do ERP legado atrasar.
  - Mitigacao: simular adaptador com stub desde a semana 2.
- Licenciamento concorrente gerar falso bloqueio.
  - Mitigacao: heartbeat + TTL + painel de sessoes ativas.
- Falta de visibilidade do progresso.
  - Mitigacao: demo semanal obrigatoria.

---

## 8. Primeiros passos imediatos (proxima semana)

1. Realizar kick-off de 90 minutos com negocio e tecnologia.
2. Aprovar escopo fechado do MVP.
3. Definir tenant piloto e usuarios de teste.
4. Fechar contratos minimos de API/evento.
5. Iniciar implementacao da fundacao tecnica.

---

## 9. Critero de sucesso do MVP para cliente

- Usuario entra no sistema respeitando limite contratado simultaneo.
- Vendedor cria pedido em poucos minutos.
- Pedido vai para integracao e retorna status claro.
- Cliente enxerga valor e aprova continuidade.

Com isso, o projeto segue com evolucao incremental ate cobrir todo o escopo da documentacao.

---

## 10. Modelo GitHub (obrigatorio)

Este projeto sera conduzido no GitHub com rastreabilidade completa por etapa.

Regras obrigatorias:

1. Todo trabalho nasce como Issue.
2. Toda Issue deve estar no formato Historia + Tarefas tecnicas.
3. Toda etapa relevante usa branch propria.
4. Toda branch so entra na principal via Pull Request.
5. Toda entrega exige evidencias: testes, checklist e documentacao atualizada.

---

## 11. Formato padrao de Historia e Tarefas

### 11.1 Estrutura da Historia (Issue)

Titulo:

`[MVP][Modulo] Historia: <resultado de negocio>`

Template da historia:

- Contexto
- Objetivo de negocio
- Criterios de aceite
- Escopo (in)
- Fora de escopo (out)
- Dependencias
- Riscos

Exemplo de criterios de aceite:

1. Dado usuario autenticado, quando criar pedido valido, entao pedido e salvo.
2. Dado pedido enviado, quando integracao concluir, entao status e atualizado.
3. Dado tenant com limite 4, quando 5o usuario tentar login, entao acesso e negado.

### 11.2 Estrutura das Tarefas (sub-itens da Historia)

Titulo:

`[Task][Modulo] <acao tecnica objetiva>`

Template da tarefa:

- Objetivo tecnico
- Arquivos/componentes afetados
- Passos de implementacao
- Teste de validacao
- Critero de pronto

---

## 12. Estrategia de Branches e PRs

### 12.1 Estrutura de branches

- `main`: branch estavel de producao.
- `develop`: consolidacao da sprint atual.
- `feature/<issue-id>-<descricao-curta>`: historias e tarefas.
- `hotfix/<issue-id>-<descricao-curta>`: correcao urgente em producao.
- `release/<versao>`: preparacao de release.

Exemplos:

- `feature/123-login-tenant-jwt`
- `feature/148-pedido-criacao-basica`
- `feature/177-limite-usuarios-simultaneos`

### 12.2 Regras de Pull Request

Padrao de titulo:

`[ISSUE-123] <resumo objetivo da alteracao>`

Checklist obrigatorio no PR:

1. Escopo da issue atendido.
2. Testes locais executados.
3. Sem impacto colateral conhecido.
4. Documentacao atualizada.
5. Evidencias (print, log, video curto ou gif).

Politica minima de revisao:

- 1 aprovacao tecnica obrigatoria.
- PR pequeno: preferencialmente ate 400 linhas liquidas.
- Merge squash para manter historico limpo.

---

## 13. CI/CD no GitHub (blueprint)

Objetivo: evitar perda de controle em projeto grande, com validacao automatica a cada passo.

### 13.1 Pipelines recomendados

1. CI - Pull Request
  - Lint e format check
  - Build backend (.NET)
  - Build frontend (Next.js)
  - Testes unitarios
  - Verificacao de seguranca basica (dependencias)

2. CI - Merge em develop
  - Repetir validacoes
  - Publicar artefatos de homologacao

3. CD - Release
  - Trigger por tag (`v*`) ou merge em `main`
  - Deploy em ambiente alvo
  - Smoke test automatizado

### 13.2 Gates de qualidade

- Branch protection em `main` e `develop`
- Bloquear merge sem CI verde
- Bloquear merge sem review
- Bloquear merge sem issue vinculada

### 13.3 Ambientes

- `dev`: testes internos rapidos
- `hml`: homologacao com fluxo real
- `prod`: liberacao controlada

### 13.4 Estrategia de release

- Versao semantica: `vMAJOR.MINOR.PATCH`
- Release MVP inicial: `v0.1.0`
- Deploy incremental e reversivel (rollback definido)

Arquivos ja preparados no repositorio:

- [.github/workflows/ci.yml](.github/workflows/ci.yml)
- [.github/workflows/cd.yml](.github/workflows/cd.yml)
- [.github/ISSUE_TEMPLATE/story.yml](.github/ISSUE_TEMPLATE/story.yml)
- [.github/ISSUE_TEMPLATE/task.yml](.github/ISSUE_TEMPLATE/task.yml)
- [.github/PULL_REQUEST_TEMPLATE.md](.github/PULL_REQUEST_TEMPLATE.md)

Observacao:

- O CI/CD foi criado em modo inicial, com placeholders de deploy para serem
  conectados ao ambiente real (dev/hml/prod) na fase de infraestrutura.

---

## 14. Backlog inicial do MVP em Historias

Epic: MVP Forca de Vendas

Historia 1 - Autenticacao e tenant

- Tarefa 1.1: API de login JWT
- Tarefa 1.2: refresh token
- Tarefa 1.3: middleware tenant

Historia 2 - Controle de usuarios simultaneos

- Tarefa 2.1: tabela de assinatura por tenant
- Tarefa 2.2: controle de sessao em Redis
- Tarefa 2.3: bloqueio de login acima do limite
- Tarefa 2.4: endpoint de sessoes ativas (admin)

Historia 3 - Catalogo basico

- Tarefa 3.1: endpoint clientes
- Tarefa 3.2: endpoint produtos
- Tarefa 3.3: cache de catalogo

Historia 4 - Pedido MVP

- Tarefa 4.1: criar pedido
- Tarefa 4.2: adicionar itens e parcelas
- Tarefa 4.3: calculo de totais e validacoes
- Tarefa 4.4: listar e detalhar pedidos

Historia 5 - Integracao com legado

- Tarefa 5.1: publicar evento de pedido enviado
- Tarefa 5.2: worker adaptador
- Tarefa 5.3: retorno de status processado/erro

Historia 6 - Frontend MVP

- Tarefa 6.1: login
- Tarefa 6.2: dashboard de pedidos
- Tarefa 6.3: tela novo pedido
- Tarefa 6.4: historico com status

Historia 7 - Qualidade e demo

- Tarefa 7.1: roteiro de teste funcional MVP
- Tarefa 7.2: ajustes finais de UX
- Tarefa 7.3: script de demonstracao ao cliente

---

## 15. Regra de controle de escopo para nao perder o foco

Para manter entregas pequenas e seguras:

1. Toda historia deve caber em no maximo 3 dias uteis.
2. Se passar de 3 dias, quebrar em 2 ou mais historias.
3. Toda task deve caber em no maximo 1 dia.
4. Nenhuma branch fica aberta por mais de 2 dias sem PR.
5. Toda semana termina com demo e atualizacao de documentacao.
