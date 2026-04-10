# 4. Interfaces e Integração

## 4.1 Separação de Módulos (Decoupling)

Um dos maiores ganhos arquiteturais do novo Versatus Force Sales MVP é a remoção de dependências diretas de bibliotecas Desktop, chamadas orientadas ao método (.NET Remoting) sobre protocolos custom, adotando HTTP e Mensageria (Events).

Isso nos permite que aplicações SPA Next.js, integrações SaaS externas ou qualquer dispositivo Mobile consuma a aplicação. 

## 4.2 Application Programming Interfaces (Web API .NET 8)

As requisições operacionais de leitura de catálogo e salvamento de Orçamentos ocorrem pelos seguintes contratos definidos em Endpoints REST sob o Swagger Specification (OpenAPI):

### Grupo Auth e Sessão (`/api/v1/auth`)
* `POST /login`: Efetua autenticação via tenantId e emite os JWTs validando o limite de usuários.
* `POST /refresh`: Renovação do token temporário expirado.

### Grupo Catálogo e Mestre (`/api/v1/catalogo`)
* `GET /clientes`: Busca otimizada com autocomplete de Razão Social via Redis.
* `GET /produtos`: Lista produtos por tabela de preço vinculada.

### Grupo de Pedidos (`/api/v1/pedidos`)
* `POST /`: Submete o contrato fechado transacional: Itens, Totais de sub-totais, Acréscimos/Descontos e Parcelamento previsto.
* `GET /{id}`: Recebe o status processado originado do Backend legados.

## 4.3 Integração Orientada a Eventos com ERP

A aplicação usa o Padrão "Event-Driven" para evitar chamadas síncronas contra uma base corporativa local na filial do Cliente que pode cair.

O Broker utilizado é **RabbitMQ** (On-Premise migração) ou **Amazon SQS** (SaaS Cloud Phase).

Haverão três (03) contratos essenciais fluindo em Filas distintas:
1. `PedidoEnviadoEvent`: Contém o Payload completo transacionado do banco cloud. Transportado pelo Broker, chega ao **Worker ERP Adapter** instalado localmente na infra do cliente. 
2. O Adapter efetua a ponte (Legacy Bridge), engatando no método clássico existente: `GerarDocumentoVendaVersatus()`.
3. Em resposta o Adapter produz o retorno da integração:
   * `PedidoProcessadoEvent`: Pedido importado perfeitamente pro fluxo fiscal da Versatus. Fornece de volta o Ticket de Rastreio (`documentoVendaId`).
   * `PedidoErroEvent`: O servidor ERP refutou por regras contábeis, estoques insuficientes no galpão, restrição de crédito financeira atual, etc. O MVP UI exibirá isso em alerta explícito ao Representante de Vendas.
