# 06 — Aplicação Web de Força de Venda (Small → Web)

## Sumário

Este documento extrai a lógica do módulo Small (força de venda móvel offline) e
propõe uma arquitetura moderna para uma **aplicação web desacoplada e independente**,
capaz de integrar-se de forma assíncrona aos módulos `faturamento` e `Base.Distribuicao`
do ERP legado, com caminho para oferta SaaS.

---

## 1. Diagnóstico do módulo Small legado

### 1.1 O que ele faz

O módulo Small é o canal de pedidos de campo do ERP. Funciona como uma aplicação
WinForms instalada no dispositivo do vendedor (notebook/tablet Windows), operando
**offline-first**: o representante comercial cria pedidos sem conexão com o servidor,
e periodicamente sincroniza os dados via FTP ou pasta local compartilhada.

### 1.2 Entidades principais

| Tabela legada     | Domínio                         | Campos relevantes                                                                                     |
|-------------------|---------------------------------|-------------------------------------------------------------------------------------------------------|
| `MobVenda`        | Cabeçalho do pedido             | `idVendaSmall`, `idClienteSmall`, `idComissionado`, `idDocumentoVenda` (FK p/ faturamento pós-sync), `nomePreCliente`, `orcamento`, `exportada`, `processada`, `valorTotal`, `valorFinal`, `valorTotalDesconto`, `valorTotalAcrescimo`, `dataEmissao`, `valorFrete`, `idTipoPlataforma`, `chaveDispositivo`, `codigoIntegracao` |
| `MobVendaItem`    | Itens do pedido                 | `idEstoqueSmall`, `idTabelaPrecoEstoqueSmall`, `quantidade`, `valorUnitario`, `valorDesconto`, `valorAcrescimo`, `totalItem` |
| `MobVendaParcela` | Parcelas do pedido              | `numeroParcela`, `idFormaCobrancaSmall`, `valor`, `vencimento`                                        |
| `MobConfiguracao` | Config. do dispositivo/usuário  | `chaveDispositivo`, `nomeAcesso`, `ftpEndereco/Porta/Usuario/Senha`, `descontoMaximo`, `importarSomenteClientesAreaVenda`, `idAreaVenda` |
| `MobFinanceiro`   | Posição financeira do cliente   | `numeroDocumento`, `situacao`, `cheque`, `valorPendente`, `dataVencimento`                            |
| `MobCliente`      | Clientes cached (offline)       | Cópia local do cadastro para uso sem servidor                                                         |
| `MobEstoque`      | Produtos/saldo cached           | `saldo` (decrementado localmente na inclusão de item)                                                  |
| `MobTabelaPreco`  | Tabela de preços cached         | Cópia local da tabela de preço ativa                                                                  |

### 1.3 Fluxo de sincronização legado

```
Dispositivo (WinForms)                   Servidor
      │                                      │
      │── Download: clientes, estoque,        │
      │   tabela preço, condições pagto ─────►│ (ClasseTipo: 1..4, 21)
      │                                      │
      │ [Vendedor cria pedidos offline]       │
      │                                      │
      │── Upload: MobVenda + itens +          │
      │   parcelas (exportada=true) ─────────►│ (ClasseTipo: 5)
      │                                      │
      │                                      │── GerarDocumentoVendaVersatus()
      │                                      │   (processada=true,
      │                                      │    idDocumentoVenda preenchido)
      │                                      │
      │◄── Download: VendaHistorico ─────────│ (ClasseTipo: 19)
```

**Flags de controle no `MobVenda`:**
- `exportada = false` → pedido ainda no dispositivo
- `exportada = true, processada = false` → recebido pelo servidor, aguardando geração de documento
- `exportada = true, processada = true` → `DocumentoVenda` gerado; `idDocumentoVenda` preenchido

### 1.4 Integração com faturamento — método chave

O método `VendaBase.GerarDocumentoVendaVersatus()` é o ponto zero de acoplamento
legado. Ele:

1. Valida o pedido Small (`ValidarVendaVersatus`)
2. Instancia `IDocumentoVenda` (objeto de negócio de `faturamento`)
3. Cria itens via `VendaSmallItem.AddItemVendaVersatus()`
4. Chama `OnGerarDocumentoVendaVersatus()` (hook abstrato, com aplicação de parcelas/pagamentos)
5. Persiste `DocumentoVenda` (salva na tabela `VenDocumento` de `Base.Distribuicao`)
6. Atualiza `MobVenda` (`processada = true`, `idDocumentoVenda = dv.IdDistribuicao`)

O `DocumentoVenda` herda de `Distribuicao` (tabela `VenDocumento`), abrangendo
tributos, formas de pagamento, comissionados e integração fiscal (NF-e).

### 1.5 Limitações atuais

| Limitação                          | Impacto                                          |
|------------------------------------|--------------------------------------------------|
| WinForms — Windows only            | Não funciona em Mac/Linux/mobile nativo          |
| Sync via FTP                       | Frágil, sem rastreabilidade, sem criptografia    |
| Offline-first com cópia de dados   | Conflitos de estoque não são detectados em tempo real |
| Acoplamento direto ao servidor .NET Remoting | Impossível desacoplar sem refatorar |
| Instalação por dispositivo         | Custo alto de distribuição e atualização         |
| Sem multi-tenant nativo            | Um banco por cliente, impede SaaS                |

---

## 2. Domínio — Lógica a extrair

A seguir está o contrato de negócio que a nova aplicação deve implementar,
independentemente do ERP legado.

### 2.1 Pedido (Order)

```
Pedido
├── id                (UUID)
├── numeroPedido      (gerado pelo sistema)
├── clienteId         (ref. ao cadastro de clientes)
├── nomePreCliente    (para pre-cadastro em campo)
├── vendedorId        (comissionado/representante)
├── dataEmissao
├── situacao          (Rascunho | Enviado | Processado | Cancelado | Erro)
├── orcamento         (bool)
├── origem            (Small | Ecommerce | API | ...)
├── codigoIntegracao  (ref. no ERP legado)
├── observacao
├── valorFrete
├── totais
│   ├── subtotal
│   ├── descontoTotal
│   ├── acrescimoTotal
│   └── valorFinal
├── itens             [ PedidoItem ]
└── parcelas          [ PedidoParcela ]
```

```
PedidoItem
├── id
├── produtoId         (referência ao catálogo)
├── descricao
├── unidade
├── quantidade
├── valorUnitario     (da tabela de preço no momento da venda)
├── percentualDesconto / valorDesconto
├── percentualAcrescimo / valorAcrescimo
├── totalItem
└── tabelaPrecoId     (tabela de preço usada)
```

```
PedidoParcela
├── numero
├── formaCobrancaId
├── valor
└── vencimento
```

### 2.2 Cálculo de preço e desconto

Regras extraídas de `VendaSmallItem.CalcularItem()`:

1. `valorUnitario` vem da tabela de preço; pode ser sobrescrito por usuário com permissão
2. `valorDesconto = valorUnitario * (percentualDesconto / 100)`
3. `valorAcrescimo = valorUnitario * (percentualAcrescimo / 100)`
4. `precoFinal = valorUnitario - valorDesconto + valorAcrescimo`
5. `totalItem = precoFinal * quantidade`
6. Desconto máximo é controlado por `ConfiguracaoSmall.descontoMaximo` → deve virar política por usuário/papel

### 2.3 Catálogo de referência (dados mestre)

Entidades que a app web precisa consumir (somente leitura na nova app):

- **Clientes** — `ClienteSmall` / `ICliente` legado
- **Produtos/Estoque** — `EstoqueSmall` / `IEstoque` legado
- **Tabela de preços** — `TabelaPrecoEstoqueSmall` / `ITabelaPrecoEstoque` legado
- **Condições de pagamento** — `CondicaoPagtoSmall` / `ICondicaoPagamento` legado
- **Formas de cobrança** — `FormaCobrancaSmall` / `IFormaCobranca` legado

### 2.4 Integração com faturamento

O ponto de integração é a conversão `MobVenda → DocumentoVenda` (tabela `VenDocumento`).
Na nova arquitetura esse acoplamento é quebrado via **evento de domínio**:

```
Pedido.SituacaoAlterada(Enviado)
  → Publica: PedidoRecebidoEvent { pedidoId, dadosCompletos }
  → ERP Legado consome: GerarDocumentoVendaVersatus()
  → ERP Legado publica: PedidoProcessadoEvent { pedidoId, documentoVendaId }
  → App Web consome: atualiza Pedido.situacao = Processado, salva codigoERP
```

### 2.5 Integração com Base.Distribuicao

`Base.Distribuicao` (classe `Distribuicao`) é o motor de fulfillment — calcula tributos,
forma de pagamento, despacho, saldo de estoque real. A nova app **não precisa conhecer**
esse módulo diretamente; ele é acionado internamente pelo ERP ao processar o `DocumentoVenda`.

Para a nova app, o contrato é: "enviei um pedido com estrutura correta → ERP converte → me notifica do resultado".

---

## 3. Arquitetura proposta

### 3.1 Visão geral

```
┌─────────────────────────────────────────────────────────────────┐
│                     Aplicação Web (nova)                        │
│                                                                 │
│  ┌────────────────┐    ┌──────────────────────────────────────┐ │
│  │  Frontend SPA  │    │          Backend API (.NET 8)         │ │
│  │  (React/Next)  │◄──►│  Pedidos | Catálogo | Sync | Auth    │ │
│  └────────────────┘    └──────────┬────────────────┬──────────┘ │
│                                   │                │            │
│                         ┌─────────▼──┐    ┌────────▼────────┐  │
│                         │  PostgreSQL │    │  Message Broker │  │
│                         │  (app db)  │    │  (RabbitMQ/SQS) │  │
│                         └────────────┘    └────────┬────────┘  │
└──────────────────────────────────────────────────────┼──────────┘
                                                        │ eventos
                                             ┌──────────▼──────────┐
                                             │    Adaptador ERP     │
                                             │  (Worker .NET 8)     │
                                             │  consome evento →    │
                                             │  chama ERP legado    │
                                             └──────────┬───────────┘
                                                        │ .NET Remoting /
                                                        │ SQL direto (fase 1)
                                             ┌──────────▼───────────┐
                                             │   ERP Legado          │
                                             │  (.NET Framework 4.7) │
                                             │  faturamento +        │
                                             │  Base.Distribuicao    │
                                             └──────────────────────┘
```

### 3.2 Componentes

#### Backend — ASP.NET Core .NET 8 (Modular Monolith)

Módulos organizados por domínio:

| Módulo           | Responsabilidade                                                               |
|------------------|--------------------------------------------------------------------------------|
| `Pedidos`        | CRUD de pedidos, cálculo de totais, workflow de situação, upload de arquivos   |
| `Catalogo`       | Leitura de produtos, preços, clientes, condições de pagamento                  |
| `Usuarios`       | Autenticação, perfis, controle de desconto máximo por usuário                  |
| `Integracao`     | Publicação de eventos, webhook handler, status de processamento no ERP         |
| `Tenants`        | Gestão de tenants (SaaS)                                                       |

Tecnologias do backend:

```
Runtime           : .NET 8 LTS
Web framework     : ASP.NET Core Minimal APIs + MediatR (CQRS leve)
ORM / DB Access   : EF Core 8 (Code-First) + Dapper para queries complexas
Validação         : FluentValidation
Autenticação      : JWT Bearer + refresh token (ASP.NET Core Identity)
Documentação API  : Swashbuckle / Scalar
Logging           : Serilog → Seq (desenvolvimento) / CloudWatch ou OTLP (produção)
Observabilidade   : OpenTelemetry (traces, métricas)
Background jobs   : Hangfire (licença LGPL, adequada) ou Worker Services nativos
Message broker    : RabbitMQ (on-premise) / Amazon SQS (cloud)
Cache             : Redis (listas de catálogo + sessões)
Testes            : xUnit, Bogus (dados fake), Testcontainers (integ.)
```

#### Frontend — React + Next.js

```
Framework         : Next.js 14+ (App Router)
UI components     : shadcn/ui + Tailwind CSS
State management  : Zustand (client state) + TanStack Query (server state)
Offline / PWA     : next-pwa + IndexedDB (Dexie.js) para pedidos offline
Mobile            : Responsivo por padrão; PWA instalável em Android/iOS
```

> **Alternativa Blazor WebAssembly:** viável se o time for 100% .NET, mas carregamento
> inicial maior e ecossistema de componentes mais limitado. Recomenda-se React/Next.js
> para alcance máximo de desenvolvedores.

#### Template recomendado para frontend

Recomendação principal:

- Basear o frontend em um template de dashboard Next.js (App Router), com layout
  administrativo pronto (menu lateral, tabela, filtros, paginação e formulários)
- Combinar com `shadcn/ui` + Tailwind para manter consistência visual e acelerar entregas

Diretriz importante:

- Preferir template de UI (frontend) e evitar templates SaaS fullstack acoplados em
  autenticação/banco próprios (ex.: NextAuth + Prisma acoplados por padrão), para não
  conflitar com a API .NET 8 e as regras de licenciamento por usuários simultâneos

Estrutura mínima esperada no template escolhido:

1. Tela de login
2. Dashboard de pedidos
3. Tela de novo pedido
4. Catálogo (clientes/produtos)
5. Sessões ativas e limite contratado (admin)
6. Configurações do tenant

Checklist de customização inicial do template:

1. Remover autenticação nativa do template (se houver)
2. Integrar autenticação com API .NET 8 (JWT + refresh token)
3. Configurar camada de API client com interceptors para token e retry
4. Adicionar TanStack Query para cache e invalidação de dados
5. Preparar PWA (`next-pwa`) e storage offline com Dexie (fase incremental)
6. Criar design tokens (cores, tipografia, espaçamento) do produto

#### Adaptador ERP (Worker Service .NET 8)

Componente pequeno e descartável (será removido quando o ERP for migrado). Responsabilidade única:

1. Consome `PedidoEnviadoEvent` do broker
2. Converte para chamada ao ERP legado (`GerarDocumentoVendaVersatus` via SQL direto ou API ERP)
3. Publica `PedidoProcessadoEvent` com resultado

### 3.3 Banco de dados

#### PostgreSQL 16 (recomendado principal)

**Por quê PostgreSQL e não SQL Server:**

| Critério                  | PostgreSQL 16               | SQL Server Express/SE       |
|---------------------------|-----------------------------|-----------------------------|
| Custo licença             | Gratuito (open source)      | Gratuito apenas Express (limitado) |
| SaaS / multi-tenant       | Row-level security nativo   | Requer esquemas separados  |
| JSON / JSONB              | Nativo, com índice          | Suporte básico              |
| Extensibilidade           | pgvector, TimescaleDB, etc. | Limitado                    |
| Compatibilidade cloud     | RDS, Aurora, Supabase, Neon | Azure SQL, RDS SQL Server   |
| EF Core                   | Npgsql — 1ª classe          | Suporte completo            |

> Se a empresa **já paga SQL Server** e prefere manter uma tecnologia só, SQL Server 2019+
> é aceitável. Ajuste apenas o provider do EF Core (`UseSqlServer`).

#### Redis 7

- Cache de catálogo (clientes, produtos, preços) com TTL configurável por tenant
- Sessões de usuário
- Rate limiting

#### Estrutura de schemas no PostgreSQL (multi-tenant)

```sql
-- Schema compartilhado (infraestrutura)
CREATE SCHEMA infra;  -- tenants, planos, billing

-- Schema por tenant (isolamento de dados)
CREATE SCHEMA tenant_001;
CREATE SCHEMA tenant_002;
-- Alternativa: row-level security no schema public (mais simples, menos isolado)
```

### 3.4 Modelo de dados da nova app

```sql
-- Pedido
CREATE TABLE pedidos (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    numero          SERIAL,
    cliente_id      UUID,
    nome_pre_cliente VARCHAR(200),
    vendedor_id     UUID NOT NULL,
    situacao        VARCHAR(20) NOT NULL DEFAULT 'rascunho',
    orcamento       BOOLEAN NOT NULL DEFAULT FALSE,
    origem          VARCHAR(20) NOT NULL DEFAULT 'web',
    codigo_erp      VARCHAR(50),          -- idDocumentoVenda após sync
    observacao      TEXT,
    valor_frete     NUMERIC(15,4) DEFAULT 0,
    subtotal        NUMERIC(15,4) DEFAULT 0,
    desconto_total  NUMERIC(15,4) DEFAULT 0,
    acrescimo_total NUMERIC(15,4) DEFAULT 0,
    valor_final     NUMERIC(15,4) DEFAULT 0,
    data_emissao    DATE NOT NULL,
    criado_em       TIMESTAMPTZ DEFAULT now(),
    alterado_em     TIMESTAMPTZ DEFAULT now()
);

CREATE TABLE pedido_itens (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pedido_id           UUID NOT NULL REFERENCES pedidos(id),
    produto_id          VARCHAR(50),
    descricao           VARCHAR(300) NOT NULL,
    unidade             VARCHAR(10),
    quantidade          NUMERIC(15,4) NOT NULL,
    valor_unitario      NUMERIC(15,4) NOT NULL,
    perc_desconto       NUMERIC(8,4) DEFAULT 0,
    valor_desconto      NUMERIC(15,4) DEFAULT 0,
    perc_acrescimo      NUMERIC(8,4) DEFAULT 0,
    valor_acrescimo     NUMERIC(15,4) DEFAULT 0,
    total_item          NUMERIC(15,4) NOT NULL,
    tabela_preco_id     VARCHAR(50)
);

CREATE TABLE pedido_parcelas (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    pedido_id       UUID NOT NULL REFERENCES pedidos(id),
    numero          SMALLINT NOT NULL,
    forma_cobranca  VARCHAR(50),
    valor           NUMERIC(15,4) NOT NULL,
    vencimento      DATE NOT NULL
);

CREATE TABLE integracao_eventos (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID NOT NULL,
    pedido_id       UUID NOT NULL,
    tipo            VARCHAR(50) NOT NULL,  -- 'enviado' | 'processado' | 'erro'
    payload         JSONB,
    criado_em       TIMESTAMPTZ DEFAULT now(),
    processado_em   TIMESTAMPTZ
);
```

---

## 4. Estratégia de integração desacoplada

### 4.1 Contrato de eventos

```jsonc
// PedidoEnviadoEvent  (nova app → broker → Adaptador ERP)
{
  "eventId":      "uuid",
  "eventoTipo":   "pedido.enviado",
  "tenantId":     "uuid",
  "pedidoId":     "uuid",
  "timestamp":    "2025-06-01T10:30:00Z",
  "payload": {
    "clienteCodigoERP":        "12345",
    "vendedorCodigoERP":       "67",
    "tipoDocumentoVendaId":    3,
    "dataEmissao":             "2025-06-01",
    "condicaoPagamentoId":     2,
    "observacao":              "...",
    "itens": [
      { "estoqueId": 101, "tabelaPrecoId": 5, "quantidade": 2.0, "valorUnitario": 150.00 }
    ],
    "parcelas": [
      { "numero": 1, "formaCobrancaId": 1, "valor": 300.00, "vencimento": "2025-07-01" }
    ]
  }
}

// PedidoProcessadoEvent  (Adaptador ERP → broker → nova app)
{
  "eventId":      "uuid",
  "eventoTipo":   "pedido.processado",
  "pedidoId":     "uuid",
  "documentoVendaId": 9999,
  "numeroDocumento":  "000123",
  "timestamp":    "2025-06-01T10:30:05Z"
}

// PedidoErroEvent
{
  "eventId":      "uuid",
  "eventoTipo":   "pedido.erro",
  "pedidoId":     "uuid",
  "mensagem":     "Cliente não encontrado no ERP",
  "timestamp":    "2025-06-01T10:30:05Z"
}
```

### 4.2 Mapeamento de campos (nova app ↔ Small legado)

| Nova app               | MobVenda legado          | Observação                               |
|------------------------|--------------------------|------------------------------------------|
| `pedido.id`            | —                        | UUID novo; `codigo_erp` guarda o de volta |
| `pedido.codigo_erp`    | `idDocumentoVenda`       | Preenchido após processamento            |
| `pedido.cliente_id`    | `idClienteSmall`         | Internamente mapeado por código ERP      |
| `pedido.vendedor_id`   | `idComissionado`         | —                                        |
| `pedido.situacao`      | `exportada` + `processada` flags | Unificado em enum                |
| `pedido.orcamento`     | `orcamento`              | —                                        |
| `pedido.origem`        | `idTipoPlataforma`       | Small → `PlataformaTipo.Small`           |

### 4.3 Dados mestre — estratégia cache

Na fase 1, o catálogo (clientes, produtos, preços) vem do ERP legado via **sincronização agendada**:

```
Worker (a cada N minutos)
  → SELECT * FROM MobCliente WHERE filial = ?     (via SQL direto)
  → UPDATE Redis hash: catalogo:{tenantId}:clientes
  → Replica também para PostgreSQL (tabela local de referência)
```

Na fase 2 (quando o ERP legado tiver API), substituir SQL direto por chamada REST.

### 4.4 Tratamento de conflitos de estoque

O Small legado decrementava o saldo local (`EstoqueSmall.Saldo`) de forma otimista.
A nova app **não controla estoque em tempo real** — essa responsabilidade permanece no ERP.
O adaptador ERP deve retornar erro `pedido.erro` com mensagem de saldo insuficiente,
e a nova app exibe isso ao vendedor.

---

## 5. Funcionalidades da aplicação web

### 5.1 MVP (mínimo viável)

| Feature                         | Prioridade | Descrição                                                         |
|---------------------------------|------------|-------------------------------------------------------------------|
| Login / autenticação JWT        | P0         | Login com CPF/email + senha; perfil de vendedor                   |
| Catálogo de produtos            | P0         | Listagem com busca; preço por tabela do usuário                   |
| Busca de clientes               | P0         | Pesquisa por nome/CNPJ; pré-cadastro para novos clientes          |
| Criação de pedido               | P0         | Header + itens + parcelas; cálculo automático de totais           |
| Envio para ERP                  | P0         | Publicar evento e aguardar confirmação (polling ou WebSocket)     |
| Listagem de pedidos             | P0         | Histórico de pedidos com status                                   |
| Detalhe do pedido               | P0         | Itens, parcelas, status de integração, número ERP                 |
| Modo offline (PWA)              | P1         | Criar pedido sem internet; sincronizar quando conectar            |
| Orçamento                       | P1         | Pedido marcado como orçamento, sem envio automático               |
| Controle de desconto máximo     | P1         | Configurável por usuário/perfil via admin                         |
| Histórico de pedidos processados| P1         | Integração via VendaHistorico / ExportarVendaHistoricoSmallConsulta|
| Posição financeira do cliente   | P2         | FinanceiroSmall → títulos abertos                                 |
| Impressão / PDF de pedido       | P2         | Relatório básico do pedido                                        |
| Painel gerencial (supervisor)   | P2         | Pedidos por vendedor, funil diário, ranqueamento                  |

### 5.2 Fluxo de criação de pedido (telas)

```
1. Selecionar cliente (busca / pré-cadastro)
2. Escolher tabela de preço (se mais de uma)
3. Adicionar itens (busca produto → qty → desconto)
4. Revisar totais e definir parcelamento
5. Confirmar e enviar
6. Aguardar status de processamento (Spinner / WebSocket)
7. Exibir número do documento ERP gerado
```

---

## 6. Considerações SaaS

### 6.1 Modelo de tenant

**Recomendação: Database-per-schema no PostgreSQL (1 schema por tenant)**

```
infra.tenants        → cadastro de tenants, planos, configurações
tenant_{id}.pedidos  → dados isolados por tenant
tenant_{id}.catalogo_cache → cópia local do catálogo ERP do tenant
```

Alternativamente, **row-level security (RLS)** no PostgreSQL é mais simples de
operar mas tem risco de vazamento por bug de política. Para dados comerciais
sensíveis, schema separado é mais seguro.

### 6.2 Configuração por tenant

Cada tenant tem:

```
{
  "erp": {
    "tipo": "VersatusLegado" | "VersatusAPI" | "NenhUm",
    "connectionString": "...",       // para SQL direto na fase 1
    "apiBaseUrl": "...",             // para fase 2
    "filialId": 1
  },
  "vendas": {
    "descontoMaximoPadrao": 10,
    "usaOrcamento": true,
    "tabelaPrecoDefault": 2
  },
  "broker": {
    "fila": "pedidos.tenant_001"
  }
}
```

### 6.3 Planos SaaS sugeridos

| Plano       | Limite pedidos/mês | Vendedores | Integrações ERP | Preço sugerido       |
|-------------|-------------------|------------|-----------------|----------------------|
| **Starter** | 500               | 3          | Não             | R$ 149/mês           |
| **Profissional** | 5.000        | 20         | Sim (adaptador) | R$ 499/mês           |
| **Enterprise** | ilimitado      | ilimitado  | Sim + suporte   | Sob consulta         |

### 6.4 Roadmap SaaS

```
Fase 1 — On-premise / single tenant
  → Deploy em servidor do cliente (Docker Compose)
  → Banco PostgreSQL local
  → Integração via SQL direto no ERP legado

Fase 2 — SaaS básico
  → Deploy em cloud (AWS / Azure / GCP)
  → Schema por tenant
  → Self-service de cadastro e pagamento (Stripe)
  → Adaptador ERP como serviço gerenciado

Fase 3 — SaaS maduro
  → API pública para integrar ERPs de terceiros
  → Mobile app nativo (React Native / Expo) a partir do mesmo código React
  → Marketplace de integrações
```

### 6.5 Controle de cobrança por usuários simultâneos

Para cobrança justa e previsível, o plano deve ser por usuários simultâneos
(seats concorrentes), e não por quantidade total de usuários cadastrados.

Exemplo de regra:

- Tenant contratou 4 usuários simultâneos
- Pode cadastrar 20, 50 ou 100 usuários
- Somente 4 sessões ativas podem permanecer logadas ao mesmo tempo
- O 5º login recebe bloqueio de plano (com opção de upgrade)

Modelo de dados recomendado:

```sql
CREATE TABLE infra.assinaturas (
    tenant_id UUID PRIMARY KEY,
    plano VARCHAR(40) NOT NULL,
    max_usuarios_simultaneos INTEGER NOT NULL,
    status VARCHAR(20) NOT NULL, -- ativa, suspensa, cancelada
    inicio_vigencia TIMESTAMPTZ NOT NULL,
    fim_vigencia TIMESTAMPTZ
);
```

Controle de sessão ativa (Redis):

```
Key: tenant:{tenantId}:active_sessions
Type: Set
Value: sessionId

Key: session:{sessionId}
Type: Hash
Fields: userId, tenantId, loginAt, lastHeartbeatAt
TTL: 120 segundos (renovado por heartbeat)
```

Fluxo de login:

1. Usuário autentica (JWT)
2. API consulta `max_usuarios_simultaneos` do tenant
3. API lê quantidade de sessões ativas no Redis
4. Se ativo < limite: cria sessão e permite entrada
5. Se ativo >= limite: bloqueia login e retorna erro de plano

Fluxo de logout e expiração:

- Logout explícito: remove sessão do Set e Hash
- Queda de internet/fechamento abrupto: sessão expira por TTL se não houver heartbeat
- Heartbeat recomendado: a cada 30s, renovando TTL para evitar sessão fantasma

Regras adicionais importantes:

- Múltiplas abas do mesmo navegador não devem consumir vagas extras
- Mesmo usuário em outro dispositivo pode:
  - derrubar sessão anterior (modo recomendado), ou
  - consumir nova vaga (modo estrito)
- Painel do admin deve exibir em tempo real:
  - limite contratado
  - sessões ativas
  - usuários conectados

Exemplo solicitado (4 contratados + 2 novos):

- Com 4 sessões ativas, os próximos 2 logins são negados
- Ao fazer upgrade para 6 simultâneos, os 2 logins passam imediatamente

Mensagem padrão de bloqueio:

> Limite de acessos simultâneos do plano foi atingido. Faça upgrade ou encerre uma sessão ativa para continuar.

---

## 7. Stack tecnológica completa (resumo)

### Backend

```
ASP.NET Core 8 (Minimal APIs)
EF Core 8 + Npgsql
Dapper (queries de catálogo e relatórios)
MediatR (CQRS — Commands/Queries)
FluentValidation
ASP.NET Core Identity + JWT Bearer
Redis (StackExchange.Redis)
RabbitMQ (MassTransit) ou Amazon SQS
Hangfire (jobs de sincronização de catálogo)
Serilog + OpenTelemetry
Docker + docker-compose
```

### Frontend

```
Next.js 14 (App Router, React 18)
TypeScript
Tailwind CSS + shadcn/ui
TanStack Query (React Query)
Zustand (estado global)
React Hook Form + Zod
Dexie.js (IndexedDB — offline)
next-pwa (Service Worker)
Vitest + React Testing Library
```

### Infraestrutura

```
PostgreSQL 16
Redis 7
RabbitMQ 3.x (ou Amazon SQS)
Docker / Kubernetes (fase 2+)
Nginx (reverse proxy)
GitHub Actions (CI/CD)
```

---

## 8. Migração do Small legado — Strangler Fig

### 8.1 Fases

```
Fase 0 — Preparação (2 semanas)
  [ ] Mapear todos os campos MobVenda/MobVendaItem/MobVendaParcela
  [ ] Criar parser do XML de sync legado (para importar pedidos históricos)
  [ ] Deploy da infraestrutura base (PostgreSQL + Redis + broker)

Fase 1 — Novo canal, mesmo processamento (4-6 semanas)
  [ ] App web MVP rodando em paralelo
  [ ] Adaptador ERP consome eventos e chama GerarDocumentoVendaVersatus()
  [ ] WinForms Small continua operando normalmente
  [ ] Validar que pedidos da nova app chegam no ERP igual aos do Small

Fase 2 — Migração de vendedores (rolling)
  [ ] Treinamento e rollout por equipe comercial
  [ ] Monitoramento de pedidos duplicados / conflitos
  [ ] Desativar upload FTP após todos migrados

Fase 3 — Descomissionar Small legado
  [ ] Arquivar MobVenda, MobVendaItem, MobVendaParcela (backup histórico)
  [ ] Remover Gestao.Small da solução do ERP
  [ ] Manter VendaHistorico para consulta retroativa
```

### 8.2 Riscos e mitigações

| Risco                                      | Mitigação                                                   |
|--------------------------------------------|-------------------------------------------------------------|
| ERP não processa evento por indisponibilidade | Dead-letter queue com retry exponencial + alerta           |
| Mapeamento de cliente incorreto             | Validação dupla: por código ERP + CNPJ/CPF antes de enviar  |
| Saldo de estoque inconsistente              | ERP retorna erro; app exibe mensagem clara ao vendedor       |
| Resistência do time ao novo sistema         | PWA instalável; UX melhor que WinForms; treinamento pago    |
| Multi-tenant com vazamento de dados         | RLS + testes de isolamento no CI; schema separado na Fase 2 |

---

## 9. Próximos passos concretos

1. **Validar contratos de evento** com responsável pelo ERP legado
2. **Criar projeto base** (`dotnet new webapi -n ForceOfSales.Api`)
3. **Configurar EF Core + PostgreSQL** com migrations iniciais (`pedidos`, `pedido_itens`, `pedido_parcelas`)
4. **Implementar endpoint de catálogo** lendo do cache Redis (populado com dados de exemplo)
5. **Implementar endpoint de criação de pedido** com cálculo de totais e publicação de evento
6. **Criar worker adaptador** com MassTransit consumindo `PedidoEnviadoEvent` e chamando SQL do ERP legado
7. **Scaffoldar frontend Next.js** com tela de login, catálogo, criação de pedido
8. **Testar fluxo completo** end-to-end em ambiente de desenvolvimento

---

## 10. Plano inicial de execução (primeiros 60 dias)

Objetivo do plano inicial: colocar a aplicação em produção para um tenant piloto,
com criação de pedidos, integração com ERP legado e controle de acesso simultâneo.

### Semana 1-2 — Fundação técnica

- Definir arquitetura final (API, worker, frontend, Redis, broker)
- Provisionar ambientes Dev/HML com Docker Compose
- Criar repositórios e pipelines CI/CD
- Implementar módulo de tenant e assinatura (incluindo `max_usuarios_simultaneos`)
- Implementar autenticação base (login + refresh token)

Entregáveis:

- API online com health-check
- Banco PostgreSQL e Redis provisionados
- Primeiro login funcional

### Semana 3-4 — Núcleo de pedidos

- Implementar entidades e migrations (`pedidos`, `pedido_itens`, `pedido_parcelas`)
- Implementar regras de cálculo de item e totais
- Implementar endpoints de pedido (criar, listar, detalhar)
- Implementar frontend MVP (login, catálogo, montagem de pedido)
- Implementar controle de desconto máximo por perfil

Entregáveis:

- Pedido completo criado e salvo na nova base
- Fluxo de revisão e confirmação no frontend

### Semana 5-6 — Integração e licenciamento concorrente

- Implementar publicação `PedidoEnviadoEvent`
- Implementar worker adaptador para ERP legado
- Implementar retorno `PedidoProcessadoEvent` e `PedidoErroEvent`
- Implementar controle de usuários simultâneos em Redis (bloqueio no login)
- Criar painel admin de sessões ativas por tenant

Entregáveis:

- Pedido criado na nova app e processado no ERP
- Bloqueio funcional de login acima do limite contratado

### Semana 7-8 — Piloto assistido

- Habilitar tenant piloto com dados reais
- Treinar equipe comercial piloto
- Monitorar erros de integração e performance
- Ajustar UX, mensagens de erro e observabilidade
- Preparar material de rollout para novos tenants

Entregáveis:

- Go-live piloto com operação diária
- Métricas mínimas de sucesso validadas

### Critérios de sucesso do plano inicial

- 95%+ dos pedidos piloto integrados sem intervenção manual
- Tempo médio de criação de pedido <= 2 minutos
- Falha de login por concorrência tratada com mensagem clara e rastreável
- Zero vazamento entre tenants (validação de isolamento)

### Backlog imediato pós-60 dias

1. Modo offline PWA completo com sincronização automática
2. Financeiro do cliente na tela de pedido
3. Painel gerencial (vendas por vendedor, funil, conversão)
4. Onboarding self-service para novos tenants

## 11. Guia de condução do projeto

Para condução operacional do projeto (cadencia, governanca, fases de entrega,
responsabilidades e politica de atualizacao continua da documentacao), utilizar
como referencia principal:

- [Analise/07-conducao-projeto-mvp.md](Analise/07-conducao-projeto-mvp.md)

---

*Gerado como parte da análise de modernização do ERP Versatus — ver também `05-plano-piloto-acesso-global.md` e `04-roadmap-execucao.md`.*
