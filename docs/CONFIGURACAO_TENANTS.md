# Configuração de Tenants (Multitenancy)

Este documento descreve como configurar os Tenants (empresas/filiais) no ecossistema da aplicação **Versatus Force Sales**, explicando o papel das seções de configuração e parâmetros nos arquivos `appsettings.json` da **API** e do **ErpAdapter**.

---

## 1. API: `Versatus.ForcaVendas.Api`

Na API do backend, a validação de Tenants ativos e autorizados é realizada de forma **100% dinâmica**.

### Como funciona?
Quando um vendedor tenta fazer login na aplicação móvel/web:
1. A API busca o usuário no banco de dados.
2. Recupera o `TenantId` associado àquele usuário.
3. Consulta o banco PostgreSQL (tabela `assinaturas`) para verificar se existe um registro para o `TenantId` e se o campo `ativo` é verdadeiro (`true`).
4. Se a assinatura estiver desativada ou não encontrada, a API bloqueia a autenticação imediatamente.

### Como cadastrar um novo Tenant em produção?
Como a validação é totalmente baseada em banco de dados, **não é necessário configurar chaves ou variáveis de ambiente na API, nem reiniciar os serviços (zero downtime)**. Basta inserir o novo Tenant na tabela `assinaturas` do PostgreSQL:

```sql
INSERT INTO assinaturas (tenant_id, nome_empresa, max_usuarios_simultaneos, ativo)
VALUES ('00000000-0000-0000-0000-000000000003', 'Nome da Nova Empresa', 10, true);
```

---

## 2. ERP Adapter: `Versatus.ForcaVendas.ErpAdapter`

No arquivo `appsettings.json` do ERP Adapter, temos duas seções principais ligadas a tenants:

```json
"Auth": {
  "Tenants": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
},
"ErpAdapter": {
  "Tenants": {
    "00000000-0000-0000-0000-000000000001": {
      "FilialId": 1,
      "FullSyncHour": 11
    },
    "00000000-0000-0000-0000-000000000002": {
      "FilialId": 2
    }
  }
}
```

### 2.1. `Auth:Tenants` (Lista/Array)
* **Para que serve:** Define a lista de tenants para os quais os processos automáticos do ERP Adapter (exportação de catálogos e importação de pedidos) devem rodar. O serviço em background lê essa lista e executa as tarefas cíclicas de integração de forma independente para cada tenant.
* **Como configurar:** Adicione os UUIDs/GUIDs dos tenants que você deseja integrar ativamente. Geralmente, esta lista deve ser idêntica à configurada na API.

---

### 2.2. `ErpAdapter:Tenants` (Mapeamento/Objeto)
* **Para que serve:** Mapeia cada `TenantId` da aplicação para as regras e parâmetros específicos do banco de dados do ERP Versatus (como qual filial aquele tenant representa).
* **Parâmetros de configuração por Tenant:**

#### A) Chave do Objeto (Ex: `"00000000-0000-0000-0000-000000000001"`)
É o identificador GUID único do tenant. Deve corresponder exatamente ao UUID cadastrado no array `Auth:Tenants` e na API.

#### B) `FilialId` (inteiro)
* **Para que serve:** Especifica o código identificador da **Filial** correspondente no banco de dados do ERP (SQL Server). 
* **Impacto no Sistema:**
  * **Exportação do Catálogo (CatalogExporter):** As consultas SQL de produtos, tabelas de preço, saldos de estoque e clientes são filtradas por este código de filial, garantindo que o aplicativo de força de vendas de um tenant receba apenas os produtos e preços autorizados e disponíveis para a sua respectiva filial.
  * **Importação de Resultados de Faturamento (OrderImporter):** Quando o ERP fatura um pedido e grava o resultado no banco, o adaptador utiliza o `FilialId` do registro de faturamento para traduzi-lo de volta ao `TenantId` correto e enviar a mensagem de notificação de sincronização para a API.
* **Valor Padrão:** Se omitido, assume o valor `1`.

#### C) `FullSyncHour` (inteiro, opcional)
* **Para que serve:** Define a hora do dia (no formato 24h, de `0` a `23`) em que o adaptador deve realizar uma **sincronização completa (Full Sync)** do catálogo no Redis. 
* **Impacto no Sistema:** Fora dessa hora, a sincronização periódica ocorre de forma incremental/delta (apenas dados alterados recentemente). Ao atingir o horário definido, o adaptador limpa o histórico local e força uma carga completa de todos os dados do catálogo para garantir consistência.
* **Valor Padrão:** Se omitido, assume a hora `3` (3:00 AM).

---

## 3. Sobrescrita com Variáveis de Ambiente (Produção / Painel ICP)

Em ambientes de produção (como o **Painel ICP**, contêineres Docker ou serviços de nuvem), o padrão de configuração do .NET Core permite sobrescrever qualquer propriedade do `appsettings.json` utilizando variáveis de ambiente.

A convenção do .NET para mapear a hierarquia JSON em chaves lineares de variáveis de ambiente é a utilização de **dois sublinhados (`__`) como separador**.

### 3.1. Configurando a Lista de Tenants no ERP Adapter (`Auth:Tenants`)

Para configurar a lista de tenants que o ERP Adapter deve integrar de forma ativa via variáveis de ambiente, utilize o índice numérico (começando em `0`) como a chave final:

| Variável | Valor |
|---|---|
| `Auth__Tenants__0` | `00000000-0000-0000-0000-000000000001` |
| `Auth__Tenants__1` | `00000000-0000-0000-0000-000000000002` |

> [!NOTE]
> Essa configuração é **necessária apenas no ERP Adapter** (executado localmente no cliente), para que o processo em background saiba quais catálogos exportar e pedidos importar. A **API (nuvem) não necessita de nenhuma variável de tenants**, pois valida logins diretamente no PostgreSQL.

### 3.2. Configurando o Mapeamento no ERP Adapter (`ErpAdapter:Tenants`)

Para o mapeamento de dicionários (chave/valor) por `TenantId` no adaptador, o próprio GUID do tenant funciona como a chave da estrutura:

| Variável | Valor |
|---|---|
| `ErpAdapter__Tenants__00000000-0000-0000-0000-000000000001__FilialId` | `1` |
| `ErpAdapter__Tenants__00000000-0000-0000-0000-000000000001__FullSyncHour` | `11` |
| `ErpAdapter__Tenants__00000000-0000-0000-0000-000000000002__FilialId` | `2` |

