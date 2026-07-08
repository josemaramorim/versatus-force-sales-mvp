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

## Documentacao de Operacao

- [docs/ARQUITETURA_DEPLOY.md](docs/ARQUITETURA_DEPLOY.md) — O que fica na VPS e o que fica na máquina do cliente (visão geral da arquitetura de deploy)
- [docs/DEPLOY_VPS.md](docs/DEPLOY_VPS.md) — Guia completo de deploy em VPS (produção) com Docker, Nginx e SSL
- [docs/DEPLOY_ICP_API.md](docs/DEPLOY_ICP_API.md) — Deploy da API (.NET) no Painel ICP da icontainer via integração com GitHub
- [docs/INTEGRACAO_LEGADO_ERP.md](docs/INTEGRACAO_LEGADO_ERP.md) — O que o ERP legado precisa para funcionar com a app (tabelas, views, colunas, permissões)
- [docs/GERENCIAMENTO_SERVICOS.md](docs/GERENCIAMENTO_SERVICOS.md) — Gerenciamento dos serviços locais e guia Docker para desenvolvimento
- [docs/CONFIGURACAO_TENANTS.md](docs/CONFIGURACAO_TENANTS.md) — Configuração de tenants e empresas
- [docs/CICLO_VIDA_PEDIDOS.md](docs/CICLO_VIDA_PEDIDOS.md) — Fluxo e ciclo de vida dos pedidos
- [docs/PLANO_VERSAO_APLICACOES.md](docs/PLANO_VERSAO_APLICACOES.md) — Plano técnico e prompt de IA para implementação de controle automático de versões

## Governanca GitHub

- Issues no formato Historia + Tarefas
- Branch por etapa (`feature/<issue-id>-<descricao>`)
- Pull Request obrigatorio com review
- CI/CD via GitHub Actions em `.github/workflows`
