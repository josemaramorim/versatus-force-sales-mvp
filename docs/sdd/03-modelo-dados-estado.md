# 3. Modelo de Dados e Estado

## 3.1 Abordagem SaaS (Multi-Tenant)

O sistema de Force Sales precisa estar preparado para suportar dezenas de clientes da Versatus rodando suas vendas remotamente na cloud. Devido a isolamento e segurança, a arquitetura multitenant aprovada é **Database-per-schema**.

* O banco de dados do Core Backend será o **PostgreSQL 16**.
* Existirá um esquema root `infra` com os tenants e cadastros de assinaturas.
* Para cada novo cliente integrando-se, o sistema provisiona o esquema isolado (`tenant_001`, `tenant_002`).

## 3.2 Diagrama Conceitual das Entidades Core

As entidades fundamentais para a criação modularizada de Vendas são:

### Pedido (Header)
Tabela transacional isolada onde são mantidas as sub-relacionadas (`Itens` e `Parcelas`).
Campos relevantes previstos: `id (UUID)`, `tenant_id`, `situacao` (Rascunho, Enviado, Processado, Erro), `cliente_id` (ref. ERP), `origem` (Web), `subtotal`, `descontos`, `valor_final`, e as flags de integração.

### Configurações Atuais
Cálculos e definições limitantes (exemplo: `desconto_maximo`) que antes vinham da base instalada desktop, se alinham como politicas armazenadas no root tenant configuration por usuário/perfil.

## 3.3 Cache de Catálogo Distribuído (Redis)

O catálogo de produtos, tabela de preços e relação de clientes são sincronizados do ERP-Legado constantemente. Para uma listagem responsiva e com menos "load" sobre o ERP local e para entregar experiência fluída web:
* **Camada Redis 7**: Armazena via Hashes e Sets a listagem do Catálogo atual pro Tenant em questão.
* **Trabalho em Background**: Workers/Hangfire podem reaquecer o Redis por chamadas agendadas puxando do banco origem.

## 3.4 Controle de Sessão Concorrente Ativa

Uma premissa de negócio estrita do Versatus (para SaaS) é o licenciamento baseado em `Seats`.
O limite ativo (`max_usuarios_simultaneos`) fica gravado no cadastro da licença do Tenant.
A validação de "Heartbeats" da sessão ocorre em uma chave específica do Redis, de formato (Set com expiração).
O fluxo do estado de auth garante rentabilidade mantendo logado as `N` sessões, interceptando o token JWT e barrando com HTTP 403 / 429 caso novas tentem superar o Teto.
