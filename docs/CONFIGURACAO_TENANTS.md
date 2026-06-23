# Configuração de Tenants (Multitenancy)

Este documento descreve como configurar os Tenants (empresas/filiais) no ecossistema da aplicação **Versatus Force Sales**, explicando o papel das seções de configuração e parâmetros nos arquivos `appsettings.json` da **API** e do **ErpAdapter**.

---

## 1. API: `Versatus.ForcaVendas.Api`

No arquivo `appsettings.json` da API, temos a seguinte seção:

```json
"Auth": {
  "Tenants": [
    "00000000-0000-0000-0000-000000000001",
    "00000000-0000-0000-0000-000000000002"
  ]
}
```

### Para que serve?
Esta lista atua como uma **lista de permissões (whitelist)** de segurança e licenciamento em nível de servidor. 
Quando um vendedor tenta fazer login na aplicação móvel/web:
1. A API busca o usuário no banco de dados.
2. Recupera o `TenantId` associado àquele usuário.
3. Verifica se o `TenantId` está presente nesta lista `Auth:Tenants`.
4. Se o ID **não** estiver na lista, a API bloqueia a autenticação imediatamente com `401 Unauthorized` (mesmo se o usuário e senha estiverem corretos).

### Como configurar?
Basta adicionar ou remover os identificadores únicos (UUIDs/GUIDs) dos tenants autorizados a acessar o sistema. Cada tenant deve ser inserido como uma string no array JSON.

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
