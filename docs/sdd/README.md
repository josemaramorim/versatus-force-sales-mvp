# Documento de Design de Software (SDD) - Versatus Force Sales MVP

Este diretório contém o Software Design Document corporativo para o projeto **Versatus Force Sales MVP** (migração do módulo "Small" WinForms). O SDD é mantido como código e documenta toda a arquitetura, dados, integrações e diretrizes de desenvolvimento desta aplicação.

## Estrutura do SDD

1. **[Visão Geral e Escopo](01-visao-geral.md)**: Objetivos, contexto atual (legado) e o escopo funcional da nova aplicação web.
2. **[Arquitetura](02-arquitetura.md)**: Visão estrutural, diagrama de componentes (Next.js, ASP.NET Core, Worker), banco de dados e infraestrutura.
3. **[Modelo de Dados](03-modelo-dados-estado.md)**: Modelagem no PostgreSQL, estratégia multi-tenant (SaaS) e cache (Redis).
4. **[Interfaces e Integração](04-interfaces-integracao.md)**: Estratégias de API, webhooks e mensageria assíncrona orientada a eventos para integração com o ERP.
5. **[Segurança e NFRs](05-seguranca-requisitos-nfs.md)**: Requisitos não-funcionais (Performance, Offline), mecanismos de segurança (Autenticação JWT) e licenciamento por uso simultâneo.

---

> Esse design document é vivo (Living Document). Todos os desenvolvedores e arquitetos que modifiquem a arquitetura ou as regras de domínio fundamentais do Force Sales MVP devem aplicar as devidas atualizações aos arquivos deste SDD.
