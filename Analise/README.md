# Modernizacao do ERP Versatus

Arquivos desta analise:

- `01-diagnostico-legado.md`: diagnostico tecnico do estado atual e bloqueadores da migracao.
- `02-arquitetura-alvo-net8.md`: recomendacao de arquitetura e stack alvo.
- `03-priorizacao-dominios.md`: proposta de ordem de estrangulamento dos dominios.
- `04-roadmap-execucao.md`: plano faseado para sair de .NET Framework para .NET 8.
- `05-plano-piloto-acesso-global.md`: plano tecnico executavel para o primeiro modulo migrado.
- `06-app-forca-venda-web.md`: analise do modulo Small (forca de venda) e proposta de aplicacao web desacoplada com stack, banco e consideracoes SaaS.
- `07-conducao-projeto-mvp.md`: plano de conducao do projeto de ponta a ponta, com foco em MVP rapido e governanca de documentacao.
- `08-backlog-mvp-historias-tarefas.md`: backlog inicial no formato de historias e tarefas para criacao de issues no GitHub.

Padroes GitHub ja configurados no repositorio:

- `.github/ISSUE_TEMPLATE/story.yml`
- `.github/ISSUE_TEMPLATE/task.yml`
- `.github/PULL_REQUEST_TEMPLATE.md`
- `.github/workflows/ci.yml`
- `.github/workflows/cd.yml`

Leitura sugerida:

1. Diagnostico tecnico.
2. Arquitetura alvo.
3. Priorizacao dos dominios.
4. Roadmap de execucao.
5. Plano do piloto acesso.global.
6. App web forca de venda (Small).
7. Conducao do projeto (MVP-first).
8. Backlog MVP (historias e tarefas).