# Plano de Trabalho — Pré-Cadastro de Clientes (Legado MOBPRECLIENTE)

Este documento centraliza as especificações técnicas, a matriz de tarefas e o prompt de execução para a implementação da funcionalidade de pré-cadastro de novos clientes durante o fluxo de vendas do aplicativo **Versatus Force Sales**.

---

## 📌 PARTE 1: Plano de Implementação

### Análise e Sugestões de Melhoria
1. **Payload Unificado (Atômico)**: Os dados de pré-cadastro viajam embutidos no próprio JSON do Pedido (`isNovoCliente: true` + objeto `preCliente`), eliminando riscos de inconsistência por entrega assíncrona parcial no FTP.
2. **Resolução de Status Pós-Faturamento**: Quando o faturamento ocorrer, o faturista cadastra o cliente de forma oficial no ERP, associando-o ao pedido. O ERP Adapter detecta a mudança (`PROCESSADA = 1`), exporta o novo ID oficial ao FTP/Nuvem, e limpa o cadastro temporário de `MOBPRECLIENTE`.
3. **Validação de Duplicados (PWA & API)**: Impedir cadastros duplicados validando Nome e CPF/CNPJ contra a base IndexedDB local no PWA (modal do vendedor) e contra o cache Redis/Postgres na API central.

### ⚠️ Regras de Git e Branches (Crítico)
* **Proibido** realizar commits diretos, checkouts ou merges automáticos nas branches principais `develop` e `main` sem autorização explícita do usuário.
* Toda a implementação deste plano deve ser executada em uma branch de funcionalidade dedicada: **`feature/pre-cadastro-cliente`** (criada a partir da `develop`).

### Mudanças Propostas

#### 1. Frontend App (PWA)
* **[vendas.ts](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/types/vendas.ts)**: Criar interface `PreCliente` e atualizar `CriarPedidoPayload`.
* **[ClientSearch.tsx](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/components/vendas/ClientSearch.tsx)**: Adicionar opção `[+] Novo Cliente (Pré-Cadastro)` e o modal pop-up `PreClienteModal`. Validar duplicidade contra o IndexedDB antes de confirmar.
* **[nova/page.tsx](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/app/(admin)/vendas/nova/page.tsx)**: Manter estado do pré-cliente, injetar no payload de envio e persistir na fila offline IndexedDB (`offlineDb.ts`).

#### 2. Backend API (.NET Core)
* **[Pedido.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Domain/Pedidos/Pedido.cs)**: Incluir propriedades `IsNovoCliente`, `NomePreCliente` e `PreClienteJson`.
* **[PedidosDbContext.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs)**: Configurar mapeamentos no Fluent API e gerar nova Migration `AddPreClienteFieldsToPedido`.
* **[OrderExportPayload.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/OrderExportPayload.cs)**: Estender DTO de exportação FTP com dados do pré-cliente.
* **[CriarPedidoCommand.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoCommand.cs)**: Inserir verificação de duplicidade de cliente no Redis/Banco e mapear criação/exportação do pedido.

#### 3. ERP Adapter (.NET Worker)
* **[OrderImporter.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/OrderImporter.cs)**:
  * **Importação (`ProcessOrderInDatabaseAsync`)**: Se `IsNovoCliente == true`, gera ID manual em `MOBPRECLIENTE`, insere o cadastro e vincula o pedido em `MOBVENDA` (`IDMOBCLIENTE = NULL`, `NOVOCLIENTE = 1`).
  * **Faturamento (`ProcessFaturamentoRetornosAsync`)**: Lê `IDMOBCLIENTE` e `NOVOCLIENTE`. Envia o resultado com o ID oficial para o FTP. **Apenas se o envio for bem-sucedido**, executa o `DELETE FROM MOBPRECLIENTE` no SQL Server.
  * **Processador de Resultados na API**: Atualiza o ID do cliente final no PostgreSQL (`ClienteId = "cli-" + ClienteIdERP`) e limpa as colunas temporárias do pré-cadastro.

---

## 📋 PARTE 2: Checklist de Tarefas

- [x] **Git e Inicialização**
  - [x] Criar a branch de funcionalidade `feature/pre-cadastro-cliente` a partir da `develop`.
- [x] **Passo 1: Frontend App (PWA)**
  - [x] Adicionar interface `PreCliente` e atualizar `CriarPedidoPayload` no arquivo [vendas.ts](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/types/vendas.ts).
  - [x] Criar modal `PreClienteModal` e opção `[+] Novo Cliente` no autocomplete em [ClientSearch.tsx](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/components/vendas/ClientSearch.tsx), adicionando a validação de nome/documento duplicado no IndexedDB.
  - [x] Adaptar a lógica da tela principal em [nova/page.tsx](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/frontend/app/src/app/(admin)/vendas/nova/page.tsx) para controlar o estado do pré-cliente e integrá-lo com a fila de sincronização offline.
- [/] **Passo 2: Backend API (Postgres)**
  - [x] Atualizar entidade no arquivo [Pedido.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Domain/Pedidos/Pedido.cs).
  - [x] Adicionar campos no Fluent API em [PedidosDbContext.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Data/PedidosDbContext.cs) e gerar a Migration do Entity Framework.
  - [x] Modificar DTO de sincronização no arquivo [OrderExportPayload.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Infrastructure/Integration/Models/OrderExportPayload.cs).
  - [x] Validar duplicidade (Nome ou CPF/CNPJ) no Redis antes de registrar o pedido em [CriarPedidoCommand.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/backend/Versatus.ForcaVendas.Api/Pedidos/CriarPedidoCommand.cs).
- [x] **Passo 3: ERP Adapter (.NET Worker)**
  - [x] Integrar inserção em `MOBPRECLIENTE` (sequenciamento ID manual) e pedido em `MOBVENDA` na mesma transação atômica em [OrderImporter.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/OrderImporter.cs).
  - [x] Adaptar loop de retorno e envio de payload ao FTP em [OrderImporter.cs](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/Jobs/OrderImporter.cs).
  - [x] Implementar a regra de deleção local em `MOBPRECLIENTE` **apenas após** o sucesso do upload FTP.
  - [x] Atualizar o processador de resultados na API da nuvem para trocar o ID do cliente temporário para o ID oficial final do ERP e limpar o JSON temporário.
- [x] **Passo 4: Validação Final**
  - [x] Executar testes de integração na API.
  - [x] Realizar teste manual de fluxo completo (Venda -> Inserção SQL Server -> Faturamento -> Limpeza SQL/Postgres -> Atualização do status no PWA).

---

## 🤖 PARTE 3: Prompt para a Próxima IA

Copie e cole as instruções abaixo na caixa de diálogo de entrada da próxima IA desenvolvedora para que ela possa assumir a execução do trabalho imediatamente:

```markdown
Você deve assumir o desenvolvimento de uma funcionalidade no projeto Versatus Force Sales.
O objetivo é implementar o pré-cadastro de novos clientes durante o fluxo de pedidos de venda (mapeando para a tabela MOBPRECLIENTE do ERP legado) e resolvendo o cadastro quando o pedido for faturado.

A especificação detalhada está contida no documento físico:
docs/PLANO_PRE_CADASTRO_CLIENTE.md

Instruções Críticas para Execução:
1. REGRAS DE GIT (CRÍTICO): 
   - É estritamente proibido fazer commits diretos, checkouts ou merges automáticos nas branches "develop" e "main".
   - Antes de começar, crie uma branch de funcionalidade dedicada chamada "feature/pre-cadastro-cliente" a partir da "develop".
   - Todo o seu trabalho deve ser mantido e commitado nesta branch.
2. CHECKLIST DE TAREFAS:
   - Siga o checklist contido no documento "docs/PLANO_PRE_CADASTRO_CLIENTE.md". Marque o progresso alterando os símbolos para concluído "[x]" ou em andamento "[/]" no próprio arquivo à medida que avança.
3. COMPILAÇÃO E TESTES:
   - Certifique-se de que a API (Postgres) e o ERP Adapter (SQL Server) compilam sem avisos/erros após as alterações de modelo e migration.

Por favor, crie a branch "feature/pre-cadastro-cliente", abra a primeira tarefa do plano e inicie a execução.
```
