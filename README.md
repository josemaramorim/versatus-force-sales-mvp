# Versatus Force Sales MVP

Projeto MVP de forca de vendas web, com integracao desacoplada ao ERP legado Versatus.

## Objetivo

Entregar rapidamente um fluxo ponta a ponta para demonstracao ao cliente:

1. Login por tenant
2. Controle de usuarios simultaneos
3. Criacao de pedido
4. Envio para integracao
5. Retorno de status do processamento

## Documentacao principal

- [Analise/README.md](Analise/README.md)
- [Analise/06-app-forca-venda-web.md](Analise/06-app-forca-venda-web.md)
- [Analise/07-conducao-projeto-mvp.md](Analise/07-conducao-projeto-mvp.md)
- [Analise/08-backlog-mvp-historias-tarefas.md](Analise/08-backlog-mvp-historias-tarefas.md)

## Governanca GitHub

- Issues no formato Historia + Tarefas
- Branch por etapa (`feature/<issue-id>-<descricao>`)
- Pull Request obrigatorio com review
- CI/CD via GitHub Actions em `.github/workflows`
