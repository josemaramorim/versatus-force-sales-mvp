# 2. Arquitetura

O Versatus Force Sales MVP utiliza uma abordagem de Monolito Modular Cloud-Native, ideal para lidar com a complexidade de transição controlada (*Strangler Fig Pattern*) provendo robustez do ecossistema .NET com modernidade de frontends web em React e Next.js.

## 2.1 Visão Geral do Ecossistema

O ecossistema divide-se basicamente em 3 grandes frentes:

1. **Frontend App (Next.js Application)**: A aplicação SPA/PWA focada na interface de captura, painel de vendedores e interface responsiva do catálogo.
2. **Core API / Monolito Modular (.NET 8)**: O cérebro central SaaS do produto. Ele cuida do armazenamento isolado, das regras de validação superficial (descontos máximos, acesso, etc) e orquestra eventos.
3. **Adaptador ERP (Worker Service .NET 8)**: Um micro-serviço (daemon) responsável exclusivo por escutar os eventos do Core API e realizar a inserção/comunicação direta nos bancos legados e acionar a distribuição/faturamento legados (via Integração Local Legacy).

## 2.2 Estrutura do Frontend (React + Next.js 14+)

* **Next.js App Router**: Arquitetura padrão para entrega de aplicação estruturada em server/client boundaries.
* **UI/UX System**: Interface moderna, premium ("Airy aesthetic", glass interactions), suportada por bibliotecas padrão como Tailwind CSS + shadcn/ui.
* **State Management**:
  * *Global Client State*: `Zustand` para fluxos voláteis do usuário e preferências em campo.
  * *Server State/Cache*: `TanStack Query` otimizando chamadas HTTP para o catálogo.

## 2.3 Estrutura do Backend (.NET 8)

Adotamos a *Clean Architecture* leve acoplada a *Modular Monolith*. Os módulos respeitam domínios internos:
* **`Versatus.Sales.Api`**: Gateway HTTP, autenticação, injeção de dependências e roteamento.
* **`Versatus.Sales.Application`**: Commands/Queries e contratos DTOs (`MediatR` para controle de fluxo).
* **`Versatus.Sales.Domain`**: Domínio Rico (Pedidos, Itens de Pedido, Situações).
* **`Versatus.Sales.Infrastructure`**: DB Context (EF Core), Caches (Redis), Integração Message Broker (MassTransit/RabbitMQ).

## 2.4 Padrão de Integração Asíncrona

A quebra do `.NET Remoting` para este módulo é efetuada por **Eventos de Domínio**:
1. Frontend salva o Pedido.
2. API transaciona no Postgre do SaaS e lança `PedidoEnviadoEvent` no Message Broker.
3. API responde HTTP 201 (Accepted/Created) para o Frontend.
4. O Worker ERP capta o evento assincronamente e aciona o módulo local (`VendaBase.GerarDocumentoVendaVersatus()`).
5. O Worker joga a resposta (`PedidoProcessadoEvent` ou `PedidoErroEvent`) no Broker que o Core App reage e reflete o `Situacao` final.
