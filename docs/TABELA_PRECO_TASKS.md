# Tarefas — Tabelas de Preço Dinâmicas (Multi-Tabela + Promocional)

> [!IMPORTANT]
> **Plano de referência**: [TABELA_PRECO_IMPLEMENTATION_PLAN.md](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/docs/TABELA_PRECO_IMPLEMENTATION_PLAN.md)
> 
> **Regra de branching**: Toda implementação deve ser feita na branch `feature/tabela-preco-dinamica` criada a partir de `develop`. **Nunca commitar diretamente em `develop` ou `main`.**

---

## Fase 0 — Setup da Branch

- [x] Fazer checkout da branch `develop` e garantir que está atualizada (`git pull`)
- [x] Criar branch `feature/tabela-preco-dinamica` a partir de `develop`
- [x] Confirmar que está na branch correta antes de iniciar qualquer alteração

```bash
git checkout develop
git pull origin develop
git checkout -b feature/tabela-preco-dinamica
```

---

## Fase 1 — Modelo de Dados (DTOs)

**Arquivo**: [CatalogSnapshot.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/CatalogSnapshot.cs)

- [x] **1.1** Adicionar 3 campos ao `TabelaPrecoCatalogDto`:
  - `bool IsPromocional` (default `false`)
  - `DateTime? VigenciaInicio`
  - `DateTime? VigenciaFim`

- [x] **1.2** Criar novo DTO `TabelaPrecoMetadataDto` (metadados da tabela-mãe `VENTABELAPRECO`):
  - `int TabelaPrecoIdERP`
  - `string Descricao`
  - `bool IsPromocional`
  - `bool Ativa` (default `true`)
  - `DateTime? VigenciaInicio`
  - `DateTime? VigenciaFim`

- [x] **1.3** Criar novo DTO `TenantParametersDto`:
  - `int TabelaPrecoIdDefault` (default `1`)
  - `bool PermiteAlterarTabelaPreco` (default `true`)

- [x] **1.4** Adicionar ao `CatalogSnapshot`:
  - `IReadOnlyList<TabelaPrecoMetadataDto> TabelasPrecoMetadata`
  - `TenantParametersDto TenantParameters`

**Critério**: O projeto deve compilar sem erros após esta fase.

---

## Fase 2 — ERP Adapter (Exportação SQL + Limpeza)

**Arquivo**: [CatalogExporter.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/CatalogExporter.cs)

### Limpeza (remover dados simulados)

- [x] **2.1** Deletar o método `GenerateSimulatedCatalog` (linhas ~472-501)
- [x] **2.2** Remover o bloco `if (useSimulated) { ... }` (linhas ~116-128)
- [x] **2.3** Alterar o bloco `catch` (linhas ~141-153) para apenas logar o erro e retornar sem exportar:
  ```csharp
  catch (Exception ex)
  {
      _logger.LogError(ex, "Falha ao consultar SQL Server do ERP para tenant {TenantId}. Catálogo NÃO atualizado.", tenantId);
      return;
  }
  ```
- [x] **2.4** Remover o parâmetro `useSimulated` de `ExportCatalogForTenantAsync` e a variável `useSimulatedCatalog` que lê `UseSimulatedCatalog` do `IConfiguration`
- [x] **2.5** Remover `UseSimulatedCatalog` de:
  - `appsettings.json` (erp-adapter)
  - `appsettings.Production.json` (erp-adapter)

### Novas queries SQL

- [x] **2.6** Na query de `VENTABELAPRECOESTOQUE` (full e delta), adicionar 3 campos ao SELECT via JOIN com `VENTABELAPRECO`:
  - `COALESCE(tp.PROMOCAO, 0) AS PROMOCAO`
  - `tp.VIGENCIAINICIO`
  - `tp.VIGENCIAFIM`
  
  E mapear no `new TabelaPrecoCatalogDto { ... }`:
  - `IsPromocional = ReadInt32Safe(reader, 7) != 0`
  - `VigenciaInicio = reader.IsDBNull(8) ? null : reader.GetDateTime(8)`
  - `VigenciaFim = reader.IsDBNull(9) ? null : reader.GetDateTime(9)`

- [x] **2.7** Criar nova query SQL para buscar metadados da tabela-mãe `VENTABELAPRECO`:
  ```sql
  SELECT IDVENTABELAPRECO, DESCRICAO, COALESCE(PROMOCAO, 0) AS PROMOCAO, VIGENCIAINICIO, VIGENCIAFIM
  FROM VENTABELAPRECO WHERE ATIVO = 1
  ```
  E popular `snapshot.TabelasPrecoMetadata` com os resultados.

- [x] **2.8** Ler parâmetros do tenant do `IConfiguration` e popular `snapshot.TenantParameters`:
  ```csharp
  snapshot.TenantParameters = new TenantParametersDto
  {
      TabelaPrecoIdDefault = _config.GetValue<int>($"ErpAdapter:Tenants:{tenantId}:TabelaPrecoIdDefault", 1),
      PermiteAlterarTabelaPreco = _config.GetValue<bool>($"ErpAdapter:Tenants:{tenantId}:PermiteAlterarTabelaPreco", true)
  };
  ```

- [x] **2.9** Adicionar novos parâmetros ao `appsettings.json` do erp-adapter, dentro de cada tenant:
  ```json
  "TabelaPrecoIdDefault": 1,
  "PermiteAlterarTabelaPreco": true
  ```

### Exportação FTP

- [x] **2.10** Criar e enviar arquivo `tabelas-preco-metadata.json` via FTP (mesmo padrão do `CatalogFileWrapper<T>`)
- [x] **2.11** Criar e enviar arquivo `tenant-parameters.json` via FTP

**Critério**: O ErpAdapter deve compilar e, ao rodar com SQL Server real, exportar os 6 arquivos JSON para o FTP (clientes, produtos, tabelas-preco, tabelas-preco-metadata, condicoes-pagamento, tenant-parameters).

---

## Fase 3 — Transporte FTP (Download dos novos arquivos)

**Arquivo**: [FtpIntegrationTransport.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Ftp/FtpIntegrationTransport.cs)

- [x] **3.1** Adicionar download e desserialização de `tabelas-preco-metadata.json` no método `FetchCatalogAsync` (ambas as variantes: FTP e SFTP)
- [x] **3.2** Adicionar download e desserialização de `tenant-parameters.json`
- [x] **3.3** Popular `snapshot.TabelasPrecoMetadata` e `snapshot.TenantParameters` com os dados baixados
- [x] **3.4** Tratar ausência dos novos arquivos como graceful (lista vazia / parâmetros default) para não quebrar a retrocompatibilidade

**Critério**: O `FetchCatalogAsync` deve retornar um `CatalogSnapshot` com as novas listas populadas quando os arquivos existirem, e com valores default quando não existirem.

---

## Fase 4 — Worker (Sync Redis)

**Arquivo**: [CatalogSyncJob.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/worker/Versatus.ForcaVendas.Worker/Jobs/CatalogSyncJob.cs)

- [x] **4.1** Adicionar serialização e persistência no Redis para `TabelasPrecoMetadata`:
  - Chave: `catalogo:{tenantId}:tabelas-preco-metadata`
- [x] **4.2** Adicionar serialização e persistência no Redis para `TenantParameters`:
  - Chave: `catalogo:{tenantId}:tenant-parameters`
- [x] **4.3** Incluir merge delta para `TabelasPrecoMetadata` (key = `TabelaPrecoIdERP`)
- [x] **4.4** Atualizar o log de confirmação para incluir contagem dos novos segmentos

**Critério**: Após a sincronização, as chaves `catalogo:{tenantId}:tabelas-preco-metadata` e `catalogo:{tenantId}:tenant-parameters` devem existir no Redis com dados válidos.

---

## Fase 5 — API Backend (Repositório + Endpoints)

### ProductSummary

**Arquivo**: [ProductSummary.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Application/Catalogo/ProductSummary.cs)

- [x] **5.1** Adicionar campo opcional `IReadOnlyList<PriceTableEntry>? PricesByTable = null` ao record `ProductSummary`
- [x] **5.2** Criar novo record `PriceTableEntry`:
  - `int TabelaPrecoIdERP`
  - `int TabelaPrecoEstoqueIdERP`
  - `string Descricao`
  - `decimal ValorUnitario`
  - `bool IsPromocional`
  - `DateTime? VigenciaInicio`
  - `DateTime? VigenciaFim`

### RedisProductCatalogRepository

**Arquivo**: [RedisProductCatalogRepository.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Data/Repositories/RedisProductCatalogRepository.cs)

- [x] **5.3** Ler `tenant-parameters` do Redis para obter `TabelaPrecoIdDefault` (não usar mais hardcode `== 1`)
- [x] **5.4** Usar `TabelaPrecoIdDefault` do tenant para definir o preço principal (`Price`) do `ProductSummary`
- [x] **5.5** Agrupar todos os preços por produto e popular `PricesByTable` com a lista de `PriceTableEntry` (incluindo todos os preços de todas as tabelas ativas)
- [x] **5.6** Atualizar o record `RedisPriceItem` para incluir os novos campos (`IsPromocional`, `VigenciaInicio`, `VigenciaFim`, `Descricao`, `TabelaPrecoEstoqueIdERP`)

### Endpoints

**Arquivo**: [CatalogoEndpoints.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Api/CatalogoEndpoints.cs)

- [x] **5.7** Criar endpoint `GET /catalogo/tabelas-preco-metadata` — retorna os metadados das tabelas de preço do Redis
- [x] **5.8** Criar endpoint `GET /catalogo/tenant-parameters` — retorna os parâmetros do tenant do Redis

**Critério**: `dotnet build` sem erros. Endpoint `/catalogo/produtos` retorna produtos com `pricesByTable` populado. Novos endpoints retornam dados válidos.

---

## Fase 6 — Frontend (Tipos + API)

### Types

**Arquivo**: [vendas.ts](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/types/vendas.ts)

- [x] **6.1** Adicionar `precosPorTabela: PriceTableEntry[]` à interface `Produto`
- [x] **6.2** Criar interface `PriceTableEntry` com campos: `tabelaPrecoIdERP`, `tabelaPrecoEstoqueIdERP`, `descricao`, `valorUnitario`, `isPromocional`, `vigenciaInicio?`, `vigenciaFim?`
- [x] **6.3** Criar interface `TabelaPrecoMetadata` com campos: `tabelaPrecoIdERP`, `descricao`, `isPromocional`, `ativa`, `vigenciaInicio?`, `vigenciaFim?`
- [x] **6.4** Criar interface `TenantParameters` com campos: `tabelaPrecoIdDefault`, `permiteAlterarTabelaPreco`

### API Client

**Arquivo**: [vendaApi.ts](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/lib/vendaApi.ts)

- [x] **6.5** Atualizar `searchProdutos`: mapear `p.pricesByTable` → `precosPorTabela`
- [x] **6.6** Criar função `getTabelasPrecoMetadata()` — chama `GET /catalogo/tabelas-preco-metadata`
- [x] **6.7** Criar função `getTenantParameters()` — chama `GET /catalogo/tenant-parameters`
- [x] **6.8** Adicionar cache offline (IndexedDB) para os dois novos endpoints

### Offline DB

**Arquivo**: [offlineDb.ts](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/lib/offlineDb.ts)

- [x] **6.9** Incrementar versão do Dexie para `3`
- [x] **6.10** Adicionar store `tabelasPrecoMetadata` (key: `tabelaPrecoIdERP`)
- [x] **6.11** Adicionar store `tenantParameters` (key simples, único registro)

**Critério**: `npm run build` sem erros. As novas funções da API retornam dados corretamente.

---

## Fase 7 — Frontend (UI — ItemModal + Nova Venda)

### Nova Venda Page

**Arquivo**: [page.tsx (nova venda)](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/app/(admin)/vendas/nova/page.tsx)

- [x] **7.1** Adicionar `useEffect` para carregar `tenantParameters` via `getTenantParameters()` ao montar a página
- [x] **7.2** Passar `tenantParameters` como prop para o `ItemModal`

### ItemModal

**Arquivo**: [ItemModal.tsx](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/components/vendas/ItemModal.tsx)

- [x] **7.3** Adicionar novo prop `tenantParameters: TenantParameters` à interface `ItemModalProps`
- [x] **7.4** Adicionar state `selectedTabelaPrecoId` (número) para a tabela de preço selecionada
- [x] **7.5** Implementar lógica de **auto-seleção de tabela** ao escolher produto (`handleProductChange`):
  - Se produto tem preço em tabela com `isPromocional === true` E hoje está entre `vigenciaInicio` e `vigenciaFim` → pré-seleciona essa tabela
  - Senão → pré-seleciona `tenantParameters.tabelaPrecoIdDefault`
- [x] **7.6** Adicionar componente `<Select>` de tabela de preço no formulário:
  - Populado com as tabelas disponíveis do `produto.precosPorTabela`
  - **Habilitado** apenas se `tenantParameters.permiteAlterarTabelaPreco === true`
  - Se desabilitado, exibir como texto informativo (readonly)
- [x] **7.7** Ao trocar tabela no Select: atualizar `valorUnitario` com o `valorUnitario` da tabela escolhida
- [x] **7.8** Adicionar badge visual **"PROMOÇÃO"** ao lado do preço quando tabela promocional vigente está selecionada
- [x] **7.9** Incluir `tabelaPrecoEstoqueIdERP` no objeto `ItemPedido` criado no `onSubmit` — necessário para o payload de exportação

### Envio do pedido

**Arquivo**: [page.tsx (nova venda)](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/app/(admin)/vendas/nova/page.tsx)

- [x] **7.10** No `handleConfirmarPedido`, passar o `tabelaPrecoEstoqueIdERP` de cada item no payload enviado à API (campo `sku` ou novo campo dedicado)

**Critério**: `npm run build` sem erros. Ao selecionar produto com promoção vigente, o preço promocional aparece pré-selecionado. Ao desabilitar `permiteAlterarTabelaPreco`, o Select fica readonly.

---

## Fase 8 — Verificação Final

- [x] **8.1** Rodar `dotnet build` em todo o backend sem erros
- [x] **8.2** Rodar `dotnet test --filter "Catalog"` — corrigir testes quebrados por mudanças de assinatura
- [x] **8.3** Rodar `npm run build` no frontend sem erros
- [x] **8.4** Testar manualmente: sincronizar catálogo → criar pedido com tabela promocional → verificar payload
- [x] **8.5** Testar cenário de vigência expirada: tabela promocional com `vigenciaFim` no passado não deve ser pré-selecionada
- [x] **8.6** Testar cenário `PermiteAlterarTabelaPreco = false`: Select desabilitado
- [x] **8.7** Testar offline: tabelas e parâmetros disponíveis no IndexedDB
- [x] **8.8** Fazer commit final e push da branch `feature/tabela-preco-dinamica`

```bash
git add -A
git commit -m "feat: suporte a múltiplas tabelas de preço com prioridade promocional"
git push origin feature/tabela-preco-dinamica
```

> [!CAUTION]
> **NÃO fazer merge em `develop` ou `main` sem autorização explícita do usuário.**
