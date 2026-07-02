# Integração com o Legado — O que o ERP Precisa para Funcionar com o Versatus Force Sales

Este documento descreve **o que o sistema legado (ERP Versatus em SQL Server) precisa fornecer e o que o adaptador de integração escreve de volta** para que a aplicação Force Sales funcione. Não é necessário modificar nada no código-fonte do ERP legado — apenas garantir que as tabelas, views e colunas descritas aqui existam e estejam acessíveis.

> [!IMPORTANT]
> O ERP Adapter **nunca altera tabelas de negócio do ERP** (não mexe em notas fiscais, estoque, financeiro, etc.). Ele apenas **lê dados de catálogo** (clientes, produtos, preços) para exportar ao aplicativo, e **grava pedidos de venda recebidos** na tabela intermediária `MOBVENDA`, que já existe no legado Versatus.

---

## 1. Visão Geral da Integração

```
┌─────────────────────────────────────────────────────────────────┐
│                  MÁQUINA DO SERVIDOR LEGADO                     │
│                                                                 │
│  SQL Server (banco "versatus")                                  │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │ LEITURA (a cada 5 min):                                 │   │
│  │   VWCLIENTE        → Dados dos clientes ativos          │   │
│  │   GLOCLIENTE       → Data de alteração dos clientes     │   │
│  │   GLOCLIENTEFILIAL → Área de venda e comissionado       │   │
│  │   VWRITEMESTOQUE   → Produtos ativos com saldo          │   │
│  │   ESTPRODUTO       → Data de alteração dos produtos     │   │
│  │   VENTABELAPRECOESTOQUE → Preços por produto e tabela   │   │
│  │   VENTABELAPRECO   → Descrição das tabelas de preço     │   │
│  │   GLOCONDICAOPAGAMENTO  → Condições de pagamento        │   │
│  │                                                         │   │
│  │ ESCRITA (a cada 10 seg):                                │   │
│  │   MOBVENDA         → Cabeçalho do pedido recebido       │   │
│  │   MOBVENDAITEM     → Itens do pedido                    │   │
│  │   MOBVENDAPARCELA  → Parcelas de pagamento              │   │
│  └─────────────────────────────────────────────────────────┘   │
│                          │   ▲                                  │
│                          │   │                                  │
│     ErpAdapter .NET ─────┘   │                                  │
│     (roda nesta máquina)     │                                  │
│                          │   │                                  │
│     FTP (porta 21) ──────────┘                                  │
│     (ou SFTP porta 22)                                          │
└─────────────────────────────────────────────────────────────────┘
                          │   ▲
               JSON via FTP/SFTP
                          ▼   │
┌─────────────────────────────────────────────────────────────────┐
│              SERVIDOR FORCE SALES (VPS)                         │
│   PostgreSQL, Redis, RabbitMQ, API .NET, Worker, Frontend       │
└─────────────────────────────────────────────────────────────────┘
```

---

## 2. Acesso ao Banco de Dados

### 2.1. Credenciais necessárias

O ERP Adapter se conecta ao SQL Server do legado com uma **string de conexão** configurada no arquivo `appsettings.json`. O banco de dados alvo é o banco principal do Versatus, tipicamente chamado `versatus`.

Exemplo de string de conexão:
```
Server=NOME_DO_SERVIDOR\SQLEXPRESS2008;Database=versatus;User Id=sa;Password=SENHA;TrustServerCertificate=True;
```

> [!IMPORTANT]
> O adaptador precisa de um usuário SQL Server com permissão de **`SELECT`** nas tabelas e views listadas abaixo, e permissão de **`INSERT` e `UPDATE`** nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA`.

### 2.2. Permissões mínimas recomendadas

Se você não quiser usar o `sa`, crie um usuário dedicado:

```sql
-- Criar usuário de integração
CREATE LOGIN fvs_integration WITH PASSWORD = 'SenhaForte@!456';
USE versatus;
CREATE USER fvs_integration FOR LOGIN fvs_integration;

-- Permissões de leitura nas views/tabelas de catálogo
GRANT SELECT ON VWCLIENTE            TO fvs_integration;
GRANT SELECT ON GLOCLIENTE           TO fvs_integration;
GRANT SELECT ON GLOCLIENTEFILIAL     TO fvs_integration;
GRANT SELECT ON VWRITEMESTOQUE       TO fvs_integration;
GRANT SELECT ON ESTPRODUTO           TO fvs_integration;
GRANT SELECT ON VENTABELAPRECOESTOQUE TO fvs_integration;
GRANT SELECT ON VENTABELAPRECO       TO fvs_integration;
GRANT SELECT ON GLOCONDICAOPAGAMENTO TO fvs_integration;

-- Permissões de escrita nas tabelas de importação de pedidos
GRANT SELECT, INSERT ON MOBVENDA        TO fvs_integration;
GRANT SELECT, INSERT ON MOBVENDAITEM    TO fvs_integration;
GRANT SELECT, INSERT ON MOBVENDAPARCELA TO fvs_integration;
GRANT UPDATE ON MOBVENDA SET (EXPORTADA) TO fvs_integration;
```

---

## 3. Views e Tabelas Lidas pelo Adaptador

### 3.1. `VWCLIENTE` — View de Clientes

Usada para exportar o catálogo de clientes ativos para o aplicativo.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDGLOCLIENTE` | INT | ✅ | ID único do cliente no ERP |
| `NOME` | VARCHAR | ✅ | Nome/Razão social do cliente |
| `CNPJ` | VARCHAR | ❌ | CNPJ (usado se CPF for vazio) |
| `CPF` | VARCHAR | ❌ | CPF (usado se CNPJ for vazio) |
| `ATIVO` | INT/BIT | ✅ | Apenas clientes com `ATIVO = 1` são exportados |
| `IDGLOFILIAL` | INT | ✅ | Filtra apenas os clientes da filial configurada |
| `ITEMFINANCEIROPADRAO` | INT | ❌ | ID da condição de pagamento padrão do cliente |

**Query executada (carga total):**
```sql
SELECT 
    c.IDGLOCLIENTE,
    c.NOME,
    COALESCE(NULLIF(c.CNPJ, ''), NULLIF(c.CPF, ''), '') AS DOCUMENTO,
    COALESCE(cf.IDGLOAREAVENDA, 1) AS IDGLOAREAVENDA,
    COALESCE(c.ITEMFINANCEIROPADRAO, 1) AS IDMOBCONDICAOPAGAMENTO,
    COALESCE(cf.IDGLOCOMISSIONADO, 1) AS IDGLOCOMISSIONADO
FROM VWCLIENTE c
LEFT JOIN GLOCLIENTEFILIAL cf ON c.IDGLOCLIENTE = cf.IDGLOCLIENTE 
    AND c.IDGLOFILIAL = cf.IDGLOFILIAL
WHERE c.ATIVO = 1 AND c.IDGLOFILIAL = @FilialId
```

---

### 3.2. `GLOCLIENTE` — Tabela de Clientes (para sincronização incremental)

Usada apenas na **carga incremental (delta)** para identificar clientes alterados desde a última sincronização.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDGLOCLIENTE` | INT | ✅ | ID do cliente (JOIN com VWCLIENTE) |
| `DATAALTERACAO` | DATETIME | ✅ | Data/hora da última alteração do cadastro |
| `DATAINCLUSAO` | DATETIME | ✅ | Data/hora de criação do cadastro |

**Query executada (carga incremental/delta):**
```sql
SELECT 
    c.IDGLOCLIENTE, c.NOME, ...
FROM VWCLIENTE c
INNER JOIN GLOCLIENTE gc ON c.IDGLOCLIENTE = gc.IDGLOCLIENTE
LEFT JOIN GLOCLIENTEFILIAL cf ON ...
WHERE c.ATIVO = 1 AND c.IDGLOFILIAL = @FilialId
  AND (gc.DATAALTERACAO > @UltimoSync OR gc.DATAINCLUSAO > @UltimoSync)
```

---

### 3.3. `GLOCLIENTEFILIAL` — Tabela de Clientes por Filial

Complementa os dados do cliente com informações específicas de cada filial.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDGLOCLIENTE` | INT | ✅ | Chave de relacionamento com VWCLIENTE |
| `IDGLOFILIAL` | INT | ✅ | Filial correspondente |
| `IDGLOAREAVENDA` | INT | ❌ | ID da área de venda do cliente |
| `IDGLOCOMISSIONADO` | INT | ❌ | ID do vendedor/comissionado responsável |

---

### 3.4. `VWRITEMESTOQUE` — View de Itens de Estoque (Produtos)

Usada para exportar o catálogo de produtos ativos com saldo.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDESTESTOQUE` | INT | ✅ | ID único do item de estoque no ERP |
| `DESCRICAO` | VARCHAR | ✅ | Descrição/nome do produto |
| `SIGLAUNIDADEVENDA` | VARCHAR | ❌ | Sigla da unidade (ex: UN, KG, CX) |
| `SALDOATUALESTOQUE` | DECIMAL | ❌ | Saldo atual em estoque |
| `DESCRICAOMARCA` | VARCHAR | ❌ | Nome da marca |
| `DESCRICAOFABRICANTE` | VARCHAR | ❌ | Nome do fabricante |
| `Ativo` | INT/BIT | ✅ | Apenas produtos com `Ativo = 1` são exportados |
| `IDGLOFILIAL` | INT | ✅ | Filtra apenas os produtos da filial configurada |

**Query executada (carga total):**
```sql
SELECT 
    IDESTESTOQUE,
    DESCRICAO,
    COALESCE(SIGLAUNIDADEVENDA, 'UN') AS SIGLAUNIDADEVENDA,
    COALESCE(SALDOATUALESTOQUE, 0) AS SALDOATUALESTOQUE,
    COALESCE(DESCRICAOMARCA, '') AS DESCRICAOMARCA,
    COALESCE(DESCRICAOFABRICANTE, '') AS DESCRICAOFABRICANTE
FROM VWRITEMESTOQUE
WHERE Ativo = 1 AND IDGLOFILIAL = @FilialId
```

---

### 3.5. `ESTPRODUTO` — Tabela de Produtos (para sincronização incremental)

Usada apenas na **carga incremental (delta)** para identificar produtos alterados.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDESTPRODUTO` | INT | ✅ | ID do produto (JOIN com VWRITEMESTOQUE via IDESTPRODUTO) |
| `DATAALTERACAO` | DATETIME | ✅ | Data/hora da última alteração |
| `DATAINCLUSAO` | DATETIME | ✅ | Data/hora de criação do produto |

> [!NOTE]
> O JOIN entre `VWRITEMESTOQUE` e `ESTPRODUTO` é feito pelo campo `IDESTPRODUTO` presente na view.

---

### 3.6. `VENTABELAPRECOESTOQUE` — Tabela de Preços por Item

Usada para exportar os preços de cada produto por tabela de preços.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDVENTABELAPRECOESTOQUE` | INT | ✅ | ID único do registro de preço |
| `IDESTESTOQUE` | INT | ✅ | Referência ao ID do produto |
| `IDVENTABELAPRECO` | INT | ✅ | Referência à tabela de preços |
| `PRECO` | DECIMAL | ✅ | Preço unitário de venda |
| `PERCENTUALDESCONTOMAXIMO` | DECIMAL | ❌ | % máximo de desconto permitido |
| `DESCONTOMAXIMODIFERENTE` | INT | ❌ | Flag se controla desconto máximo diferente |
| `ATIVO` | INT/BIT | ✅ | Apenas preços com `ATIVO = 1` |
| `IDGLOFILIAL` | INT | ✅ | Filtra pela filial configurada |
| `DATAALTERACAO` | DATETIME | ❌ | Usado na carga incremental |
| `DATAINCLUSAO` | DATETIME | ❌ | Usado na carga incremental |

**Query executada (carga total):**
```sql
SELECT 
    t.IDVENTABELAPRECOESTOQUE,
    t.IDESTESTOQUE,
    t.IDVENTABELAPRECO,
    t.PRECO,
    COALESCE(t.PERCENTUALDESCONTOMAXIMO, 0) AS PERCENTUALDESCONTOMAXIMO,
    COALESCE(t.DESCONTOMAXIMODIFERENTE, 0) AS DESCONTOMAXIMODIFERENTE,
    COALESCE(tp.DESCRICAO, '') AS DESCRICAO
FROM VENTABELAPRECOESTOQUE t
LEFT JOIN VENTABELAPRECO tp ON t.IDVENTABELAPRECO = tp.IDVENTABELAPRECO
WHERE t.ATIVO = 1 AND t.IDGLOFILIAL = @FilialId
```

---

### 3.7. `VENTABELAPRECO` — Cadastro de Tabelas de Preço

Usada para obter o nome/descrição de cada tabela de preços.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDVENTABELAPRECO` | INT | ✅ | ID da tabela de preços |
| `DESCRICAO` | VARCHAR | ✅ | Nome da tabela (ex: "Tabela Padrão Varejo") |

---

### 3.8. `GLOCONDICAOPAGAMENTO` — Condições de Pagamento

Usada para exportar as formas de pagamento disponíveis no sistema.

**Colunas utilizadas:**

| Coluna | Tipo | Obrigatório | Descrição |
|---|---|---|---|
| `IDGLOCONDICAOPAGAMENTO` | INT | ✅ | ID único da condição de pagamento |
| `DESCRICAO` | VARCHAR | ✅ | Descrição (ex: "Boleto 30 dias") |
| `QUANTIDADEPARCELA` | INT | ✅ | Número de parcelas |
| `DIASPARCELAMENTO` | INT | ✅ | Dias entre cada parcela |
| `ACRESCIMO` | DECIMAL | ❌ | Percentual de acréscimo |
| `DESCONTO` | DECIMAL | ❌ | Percentual de desconto |
| `IDGLOFORMACOBRANCA` | INT | ❌ | ID da forma de cobrança (boleto, pix, etc.) |
| `USARMESCOMERCIAL` | INT/BIT | ❌ | Se usa mês comercial para cálculo |
| `ATIVO` | INT/BIT | ✅ | Apenas condições com `ATIVO = 1` |
| `DATAALTERACAO` | DATETIME | ❌ | Usado na carga incremental |
| `DATAINCLUSAO` | DATETIME | ❌ | Usado na carga incremental |

**Query executada (carga total):**
```sql
SELECT 
    IDGLOCONDICAOPAGAMENTO,
    DESCRICAO,
    QUANTIDADEPARCELA,
    DIASPARCELAMENTO,
    COALESCE(ACRESCIMO, 0) AS ACRESCIMO,
    COALESCE(DESCONTO, 0) AS DESCONTO,
    COALESCE(IDGLOFORMACOBRANCA, 1) AS IDGLOFORMACOBRANCA,
    COALESCE(USARMESCOMERCIAL, 0) AS USARMESCOMERCIAL
FROM GLOCONDICAOPAGAMENTO
WHERE ATIVO = 1
```

---

## 4. Tabelas Escritas pelo Adaptador (Pedidos Recebidos)

Quando um vendedor finaliza um pedido no aplicativo Force Sales, o ERP Adapter grava automaticamente nas tabelas abaixo. **Essas tabelas já existem no banco Versatus** — não é necessário criá-las.

### 4.1. `MOBVENDA` — Cabeçalho do Pedido

O adaptador verifica antes de gravar se já existe um registro com o mesmo `CODIGOINTEGRACAO` (deduplicação — se o pedido já foi importado, não importa novamente).

**Colunas gravadas pelo adaptador:**

| Coluna | Valor Gravado | Descrição |
|---|---|---|
| `IDMOBVENDA` | `MAX(IDMOBVENDA) + 1` | Gerado pelo adaptador (sequencial) |
| `IDGLOFILIAL` | Configuração do tenant | ID da filial da empresa |
| `IDMOBCLIENTE` | ID do cliente selecionado | Referência ao cliente no ERP |
| `NOMEPRECLIENTE` | Nome buscado do ERP | Nome do cliente (máx. 100 caracteres) |
| `IDMOBCONDICAOPAGAMENTO` | Selecionado pelo vendedor | Condição de pagamento escolhida |
| `DATAEMISSAO` | Data/hora do pedido | Quando o pedido foi criado |
| `VALORTOTAL` | Calculado | Valor final do pedido |
| `DESCONTO` | Calculado | Desconto total do pedido |
| `ACRESCIMO` | Calculado | Acréscimo total do pedido |
| `NOMEUSUARIO` | `"ForcaVendas"` | Identificador do sistema |
| `CHAVEDISPOSITIVO` | `"Web"` | Identificador de dispositivo |
| `ORCAMENTO` | 0 ou 1 | Se é orçamento ou pedido firme |
| `OBSERVACAO` | Texto do vendedor | Observações do pedido (nullable) |
| `EXPORTADA` | `0` | Ainda não exportado para o ERP (aguardando faturamento) |
| `PROCESSADA` | `0` | Ainda não processado |
| `IDGLOCOMISSIONADO` | `1` (padrão) | Comissionado responsável |
| `VALORFRETE` | Frete do pedido | Valor do frete |
| `IDTIPOPLATAFORMA` | `1` | Identificador da plataforma web |
| `CODIGOINTEGRACAO` | UUID do pedido | Chave única de integração para deduplicação |

**Query de verificação de duplicidade:**
```sql
SELECT COUNT(*) FROM MOBVENDA 
WHERE CODIGOINTEGRACAO = @CodigoIntegracao AND IDGLOFILIAL = @FilialId
```

**Query de inserção:**
```sql
INSERT INTO MOBVENDA (
    IDMOBVENDA, IDGLOFILIAL, IDMOBCLIENTE, NOMEPRECLIENTE, IDMOBCONDICAOPAGAMENTO, 
    DATAEMISSAO, VALORTOTAL, DESCONTO, ACRESCIMO, NOMEUSUARIO, CHAVEDISPOSITIVO, 
    ORCAMENTO, OBSERVACAO, EXPORTADA, PROCESSADA, IDGLOCOMISSIONADO, IDVENDOCUMENTO, 
    IDMOBVENDAIMPORTACAO, OBSERVACAOGERACAOVENDA, NOVOCLIENTE, VALORFRETE, 
    IDTIPOPLATAFORMA, CODIGOINTEGRACAO
) VALUES (
    @IDMOBVENDA, @IDGLOFILIAL, @IDMOBCLIENTE, @NOMEPRECLIENTE, @IDMOBCONDICAOPAGAMENTO, 
    @DATAEMISSAO, @VALORTOTAL, @DESCONTO, @ACRESCIMO, @NOMEUSUARIO, @CHAVEDISPOSITIVO, 
    @ORCAMENTO, @OBSERVACAO, @EXPORTADA, @PROCESSADA, @IDGLOCOMISSIONADO, NULL, 
    NULL, NULL, 0, @VALORFRETE, 1, @CODIGOINTEGRACAO
)
```

---

### 4.2. `MOBVENDAITEM` — Itens do Pedido

Um registro para cada produto incluído no pedido.

**Colunas gravadas pelo adaptador:**

| Coluna | Valor Gravado | Descrição |
|---|---|---|
| `IDMOBVENDAITEM` | `MAX(IDMOBVENDAITEM) + 1` | Gerado pelo adaptador |
| `IDMOBVENDA` | ID da venda mãe | Referência ao cabeçalho |
| `IDGLOFILIAL` | Filial configurada | ID da filial |
| `IDMOBESTOQUE` | ID do produto no ERP | Referência ao `IDESTESTOQUE` |
| `IDMOBTABELAPRECOESTOQUE` | ID do preço utilizado | Referência ao `IDVENTABELAPRECOESTOQUE` |
| `QUANTIDADE` | Quantidade pedida | Quantidade do produto |
| `VALORUNITARIO` | Preço praticado | Preço unitário final |
| `DESCONTO` | Desconto do item | Valor de desconto por item |
| `ACRESCIMO` | Acréscimo do item | Valor de acréscimo por item |
| `VALORTOTAL` | Total do item | Valor total (qty × preço - desconto + acréscimo) |
| `SIGLAUNIDADE` | Sigla da unidade | Ex: UN, KG, CX |

**Query de inserção:**
```sql
INSERT INTO MOBVENDAITEM (
    IDMOBVENDAITEM, IDMOBVENDA, IDGLOFILIAL, IDMOBESTOQUE, IDMOBTABELAPRECOESTOQUE, 
    QUANTIDADE, VALORUNITARIO, DESCONTO, ACRESCIMO, VALORTOTAL, SIGLAUNIDADE, 
    OBSERVACAOGERACAOVENDA
) VALUES (
    @IDMOBVENDAITEM, @IDMOBVENDA, @IDGLOFILIAL, @IDMOBESTOQUE, @IDMOBTABELAPRECOESTOQUE, 
    @QUANTIDADE, @VALORUNITARIO, @DESCONTO, @ACRESCIMO, @VALORTOTAL, @SIGLAUNIDADE, 
    NULL
)
```

---

### 4.3. `MOBVENDAPARCELA` — Parcelas do Pedido

Um registro para cada parcela de pagamento do pedido.

**Colunas gravadas pelo adaptador:**

| Coluna | Valor Gravado | Descrição |
|---|---|---|
| `IDMOBVENDAPARCELA` | `MAX(IDMOBVENDAPARCELA) + 1` | Gerado pelo adaptador |
| `IDMOBVENDA` | ID da venda mãe | Referência ao cabeçalho |
| `IDGLOFILIAL` | Filial configurada | ID da filial |
| `NUMEROPARCELA` | Número da parcela | 1, 2, 3... |
| `IDMOBFORMACOBRANCA` | Forma de cobrança | Referência a `IDGLOFORMACOBRANCA` |
| `VALOR` | Valor da parcela | Valor a pagar nesta parcela |
| `DATAVENCIMENTO` | Data de vencimento | Data calculada da parcela |

**Query de inserção:**
```sql
INSERT INTO MOBVENDAPARCELA (
    IDMOBVENDAPARCELA, IDMOBVENDA, IDGLOFILIAL, NUMEROPARCELA, 
    IDMOBFORMACOBRANCA, VALOR, DATAVENCIMENTO
) VALUES (
    @IDMOBVENDAPARCELA, @IDMOBVENDA, @IDGLOFILIAL, @NUMEROPARCELA, 
    @IDMOBFORMACOBRANCA, @VALOR, @DATAVENCIMENTO
)
```

---

## 5. Coluna `CODIGOINTEGRACAO` na Tabela `MOBVENDA`

> [!IMPORTANT]
> Esta é a única coluna que **pode precisar ser criada** no banco legado caso não exista.

O adaptador usa a coluna `CODIGOINTEGRACAO` (VARCHAR) na tabela `MOBVENDA` para:
1. **Deduplicação**: Evitar importar o mesmo pedido duas vezes.
2. **Rastreamento**: Ligar o pedido do Force Sales ao registro no ERP.
3. **Retorno de resultado**: Após o pedido ser faturado no ERP (campo `PROCESSADA = 1` e `IDVENDOCUMENTO` preenchido), o adaptador lê essa coluna para saber qual pedido do Force Sales deve ser atualizado.

### Como verificar se a coluna já existe:
```sql
SELECT COLUMN_NAME 
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'MOBVENDA' AND COLUMN_NAME = 'CODIGOINTEGRACAO';
```

### Se não existir, criar:
```sql
ALTER TABLE MOBVENDA ADD CODIGOINTEGRACAO VARCHAR(50) NULL;
```

### Criar índice para melhorar a performance da busca de duplicatas:
```sql
CREATE INDEX IX_MOBVENDA_CODIGOINTEGRACAO 
ON MOBVENDA (CODIGOINTEGRACAO, IDGLOFILIAL) 
WHERE CODIGOINTEGRACAO IS NOT NULL;
```

---

## 6. Fluxo de Retorno do Faturamento (ERP → Aplicativo)

Após o ERP processar a venda (faturar, gerar NF, etc.), o adaptador detecta automaticamente e envia o resultado de volta ao aplicativo. O adaptador monitora pedidos na `MOBVENDA` com a seguinte query a cada 10 segundos:

```sql
SELECT IDMOBVENDA, IDVENDOCUMENTO, CODIGOINTEGRACAO, IDGLOFILIAL 
FROM MOBVENDA 
WHERE PROCESSADA = 1 AND EXPORTADA = 0 AND IDVENDOCUMENTO IS NOT NULL
```

Ou seja, o ERP precisa:
- Setar `PROCESSADA = 1` quando o pedido for faturado/processado.
- Preencher `IDVENDOCUMENTO` com o ID do documento gerado (NF/pedido).

O adaptador então:
1. Exporta um arquivo JSON de resultado para o servidor via FTP.
2. Marca `EXPORTADA = 1` para não processar novamente:
   ```sql
   UPDATE MOBVENDA SET EXPORTADA = 1 WHERE IDMOBVENDA = @IdMobVenda
   ```

---

## 7. Configuração do ERP Adapter

O arquivo `appsettings.json` do ERP Adapter precisa ser configurado com os dados da instalação. Ele fica em:
```
src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/appsettings.json
```

### Exemplo de configuração completo:
```json
{
  "ConnectionStrings": {
    "ErpDatabase": "Server=NOME_SERVIDOR\\SQLEXPRESS2008;Database=versatus;User Id=sa;Password=SENHA;TrustServerCertificate=True;"
  },
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "IP_OU_NOME_DO_SERVIDOR_FTP",
      "Port": 21,
      "UseSftp": false,
      "Username": "test",
      "Password": "test",
      "BasePath": "/integration-sync",
      "CatalogPollIntervalSeconds": 300,
      "ResultPollIntervalSeconds": 30
    }
  },
  "Auth": {
    "Tenants": [
      "00000000-0000-0000-0000-000000000001"
    ]
  },
  "ErpAdapter": {
    "CatalogExportIntervalSeconds": 300,
    "OrderImportIntervalSeconds": 10,
    "Tenants": {
      "00000000-0000-0000-0000-000000000001": {
        "FilialId": 1,
        "FullSyncHour": 3,
        "TabelaPrecoIdDefault": 1,
        "PermiteAlterarTabelaPreco": true
      }
    }
  }
}
```

### Parâmetros importantes:

| Parâmetro | Descrição |
|---|---|
| `ConnectionStrings.ErpDatabase` | String de conexão com o SQL Server do legado |
| `Integration.Ftp.Host` | IP ou hostname do servidor que tem o FTP (onde está o Force Sales) |
| `Integration.Ftp.Port` | Porta FTP (padrão 21) ou SFTP (padrão 22) |
| `Integration.Ftp.UseSftp` | `true` para SFTP seguro, `false` para FTP simples |
| `Auth.Tenants` | Lista de UUIDs dos tenants/empresas configurados no sistema |
| `ErpAdapter.CatalogExportIntervalSeconds` | Frequência de exportação do catálogo (segundos). Padrão: 300 (5 min) |
| `ErpAdapter.OrderImportIntervalSeconds` | Frequência de verificação de pedidos novos (segundos). Padrão: 10 |
| `ErpAdapter.Tenants.<UUID>.FilialId` | ID da filial (`IDGLOFILIAL`) no SQL Server para este tenant |
| `ErpAdapter.Tenants.<UUID>.FullSyncHour` | Hora do dia (0-23) para rodar a carga total diária. Ex: `3` = 3h da manhã |
| `ErpAdapter.Tenants.<UUID>.TabelaPrecoIdDefault` | ID da tabela de preço padrão do tenant (Ex: `1`) |
| `ErpAdapter.Tenants.<UUID>.PermiteAlterarTabelaPreco` | `true` para permitir que o vendedor selecione outras tabelas no app, `false` para bloquear |

---

## 8. Estrutura de Pastas no FTP

O adaptador organiza os arquivos de integração na seguinte estrutura de diretórios no servidor FTP:

```
/integration-sync/
└── {TenantId}/
    ├── catalogo/
    │   ├── clientes.json          ← Catálogo de clientes exportado do ERP
    │   ├── produtos.json          ← Catálogo de produtos exportado do ERP
    │   ├── tabelas-preco.json     ← Tabelas de preço exportadas do ERP
    │   └── condicoes-pagamento.json ← Condições de pagamento exportadas
    ├── pedidos/
    │   ├── pendentes/             ← Pedidos novos aguardando importação
    │   ├── processando/           ← Pedido em processo de importação (atômico)
    │   └── concluidos/            ← Pedidos já importados no ERP
    └── resultados/
        └── pendentes/             ← Resultados de faturamento aguardando leitura
```

---

## 9. Checklist Rápido para o Responsável Pelo Legado

Antes de conectar o ERP Adapter ao banco de dados legado, verifique:

- [ ] **Usuário SQL Server** com permissão de SELECT nas views e tabelas de catálogo criado
- [ ] **Usuário SQL Server** com permissão de INSERT/SELECT nas tabelas MOB criado
- [ ] **Coluna `CODIGOINTEGRACAO`** existe na tabela `MOBVENDA` (VARCHAR, nullable)
- [ ] **Índice** criado na coluna `CODIGOINTEGRACAO` para performance
- [ ] **String de conexão** configurada corretamente no `appsettings.json` do ERP Adapter
- [ ] **Firewall** da máquina do legado permite conexão de entrada na porta `1433` (SQL Server) a partir do IP da máquina onde o ERP Adapter roda
- [ ] **Porta FTP (21)** do servidor Force Sales está acessível a partir da máquina do legado
- [ ] Views `VWCLIENTE` e `VWRITEMESTOQUE` existem e retornam dados com a filial correta
- [ ] Tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA` existem no banco

---

## 10. Verificações Rápidas via SQL (Para Validação)

Use as queries abaixo para validar se os dados estão disponíveis corretamente antes de ligar o adaptador:

```sql
-- Teste 1: Quantos clientes ativos existem para a FilialId = 1?
SELECT COUNT(*) AS TotalClientes FROM VWCLIENTE WHERE ATIVO = 1 AND IDGLOFILIAL = 1;

-- Teste 2: Quantos produtos ativos com saldo existem para a FilialId = 1?
SELECT COUNT(*) AS TotalProdutos FROM VWRITEMESTOQUE WHERE Ativo = 1 AND IDGLOFILIAL = 1;

-- Teste 3: Quantas tabelas de preço ativas existem para a FilialId = 1?
SELECT COUNT(*) AS TotalPrecos FROM VENTABELAPRECOESTOQUE WHERE ATIVO = 1 AND IDGLOFILIAL = 1;

-- Teste 4: Quantas condições de pagamento ativas existem?
SELECT COUNT(*) AS TotalCondicoes FROM GLOCONDICAOPAGAMENTO WHERE ATIVO = 1;

-- Teste 5: Verificar se CODIGOINTEGRACAO existe na MOBVENDA
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'MOBVENDA' AND COLUMN_NAME = 'CODIGOINTEGRACAO';

-- Teste 6: Exemplo de cliente para validar campos retornados
SELECT TOP 5
    c.IDGLOCLIENTE, c.NOME,
    COALESCE(NULLIF(c.CNPJ, ''), NULLIF(c.CPF, ''), '') AS DOCUMENTO,
    c.ATIVO, c.IDGLOFILIAL
FROM VWCLIENTE c
WHERE c.ATIVO = 1 AND c.IDGLOFILIAL = 1;
```
