# PROMPT DE RETOMADA — Versatus Force Sales MVP

> **Como usar**: Copie TODO o conteúdo deste arquivo e cole como mensagem inicial para qualquer IA assistente de código (Claude, ChatGPT, Gemini, Copilot, Cursor, etc). O prompt contém todo o contexto necessário para que a IA entenda o projeto e comece a trabalhar imediatamente.

---

## CONTEXTO DO PROJETO

Você é um assistente sênior de desenvolvimento que vai me ajudar a retomar e concluir o projeto **Versatus Force Sales MVP**. Este é um sistema **SaaS multi-tenant de Força de Vendas** com integração assíncrona a ERP legado.

### Objetivo do MVP
Entregar um fluxo ponta a ponta demonstrável para stakeholders:
1. Login por email/senha com resolução automática de tenant e controle de licenças simultâneas
2. Consulta de catálogo (clientes e produtos) isolado por tenant, servidos via Redis cache na API e sincronizados offline via IndexedDB no frontend
3. Criação de pedidos com itens, parcelas e cálculo automático de totais
4. Integração assíncrona com o ERP via transporte configurável (FTP/SFTP, Google Drive ou RabbitMQ)
5. Demonstração guiada do fluxo completo integrado com o banco de dados SQL Server do ERP legado

### Origem do Projeto — ERP Legado "Small" (Módulo de Força de Venda)

Este MVP substitui o módulo **Small** do ERP Versatus — uma aplicação **WinForms offline-first** para vendedores de campo. O adaptador `ErpAdapter` (.NET 8) foi implementado para conectar-se ao banco de dados SQL Server real do ERP legado (`versatus`), suportando a exportação real de catálogo e a importação real de pedidos segmentada por filial com base no tenant configurado.

As entidades legadas mapeadas são:

| Tabela legada | Nova Entidade / Tabela | Descrição da Integração |
|---|---|---|
| `MobVenda` | `MOBVENDA` / `Pedido` | Gravado pelo adaptador no faturamento; polled para obter status |
| `MobVendaItem` | `MOBVENDAITEM` / `PedidoItem` | Itens com quantidades, descontos e valores finais |
| `MobVendaParcela` | `MOBVENDAPARCELA` / `PedidoParcela` | Parcelas financeiras baseadas na condição de pagamento |
| `MobCliente` | `VWCLIENTE` (View ERP) | Clientes reais exportados filtrados por filial |
| `MobEstoque` | `VWRITEMESTOQUE` (View ERP) | Produtos e saldos de estoque atuais por filial |
| `MobConfiguracao` | Mapeamento no `appsettings.json` | Configuração por tenant de filial (`FilialId`) e limites |

**Integração com faturamento ERP** — Método `VendaBase.GerarDocumentoVendaVersatus()`:
Na nova app, o fluxo do pedido inicia na web app, salva no banco local em status `rascunho`, é despachado via publicador de integração (`IIntegrationTransport`), que o escreve no transporte configurado em formato JSON. O adaptador do ERP (`ErpAdapter`) lê o arquivo do transporte, insere no SQL Server Express nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA` com a `FilialId` apropriada para que o faturamento do ERP o processe (gerando o documento correspondente e marcando `PROCESSADA = 1`). Em seguida, o adaptador faz polling para devolver o status faturado de volta para a app móvel via arquivos de resultado no transporte.

**Cálculo de preço legado** (extraído do código-fonte `VendaSmallItem.cs`):
```csharp
// valorTotal = Arredondar(quantidade * valorUnitario, 2)
// valorFinal = valorTotal - valorDesconto + valorAcrescimo
```

**Parcelas** — Geradas por condição de pagamento:
O backend calcula o parcelamento conforme a quantidade de parcelas, tolerância monetária e prazos da condição de pagamento.

---

## CONSTITUIÇÃO DO PROJETO

O projeto usa o framework **SpecKit** para governança. A constituição define 5 princípios obrigatórios que TODA mudança DEVE respeitar:

**I. MVP Value Slice First** — Cada mudança DEVE entregar uma fatia demonstrável do fluxo MVP.
**II. Tenant Isolation and Session Licensing** — Isolamento multi-tenant obrigatório em dados, cache, eventos e controle de acesso.
**III. Contract-Driven Integration and Status Flow** — Contratos de API e eventos DEVEM ser explícitos e usar o Strategy Pattern (`IIntegrationTransport`) para trocar o meio de transporte (FTP, Google Drive, RabbitMQ) dinamicamente.
**IV. Test and CI Quality Gates** — Testes existentes NÃO podem ser desabilitados e devem passar sempre.
**V. Observability and Operational Traceability** — Logs estruturados com correlação (`tenantId`, `pedidoId`, `correlationId`).

---

## INTEGRATION MULTI-TRANSPORT (FTP, GOOGLE DRIVE, RABBITMQ)

Uma premissa de arquitetura crucial é o suporte a múltiplos meios de transporte para integração com o ERP. Isso é abstraído pela interface `IIntegrationTransport` na camada de infraestrutura:

```csharp
namespace Versatus.ForcaVendas.Infrastructure.Integration;

public interface IIntegrationTransport
{
    Task PublishOrderAsync(string tenantId, OrderExportPayload order, CancellationToken ct);
    Task<IReadOnlyList<OrderResultPayload>> FetchPendingResultsAsync(string tenantId, CancellationToken ct);
    Task AcknowledgeResultAsync(string tenantId, string resultFileId, CancellationToken ct);
    Task<CatalogSnapshot?> FetchCatalogAsync(string tenantId, CancellationToken ct);
}
```

O transporte a ser utilizado é resolvido por DI no bootstrap da aplicação com base no parâmetro `Integration:Transport` definido no `appsettings.json`. Os transportes disponíveis são:

1. **FTP / SFTP (Ativo & Operacional)**:
   - Implementado através do `FtpIntegrationTransport`.
   - Utiliza a biblioteca `FluentFTP` para FTP tradicional e `Renci.SshNet` para SFTP seguro.
   - Envia pedidos para `/{tenantId}/pedidos/pendentes/`, lê resultados de `/{tenantId}/resultados/pendentes/` e exporta o catálogo em `/{tenantId}/catalogo/`.

2. **Google Drive (Projetado & Pronto para Extensão)**:
   - Abstraído na classe `GoogleDriveIntegrationTransport`.
   - Projetado para ler e gravar arquivos JSON usando a API do Google Drive (v3) em uma estrutura de pastas espelhada, autenticado via Service Account.
   - Atualmente possui stubs lançando `NotImplementedException`, pronto para ser implementado se a infraestrutura em nuvem do cliente requerer o Drive.

3. **RabbitMQ (Projetado & Pronto para Extensão)**:
   - Abstraído na classe `RabbitMqIntegrationTransport`.
   - Projetado para comunicação direta baseada em eventos assíncronos (`pedido.enviado.v1` e `pedido.resultado.v1`).
   - Atualmente possui stubs lançando `NotImplementedException`, pronto para ambientes de alta frequência e baixa latência.

---

## O QUE JÁ FOI IMPLEMENTADO (82% COMPLETO)

### ✅ US1 — Autenticação e Licenciamento (100% COMPLETA)
- Login por `email` + `senha` com resolução de tenant interna.
- Controle de seats simultâneos em Redis por tenant.
- Auditoria de sessão persistida no PostgreSQL.

### ✅ US2 — Catálogo e Pedidos (100% COMPLETA)
- Endpoints REST `/pedidos` e `/catalogo` operacionais no backend principal.
- Integração de catálogo e pedidos reais concluída no frontend.
- Redis cache para busca rápida de catálogo na API backend.

### ✅ US3 — Integração Assíncrona ERP via FTP/SFTP (100% COMPLETA)
- Strategy Pattern `IIntegrationTransport` e extensão de DI `AddIntegrationTransport()` registrando o transporte configurado.
- Provider FTP/SFTP `FtpIntegrationTransport` 100% funcional.
- Worker de backend (`Versatus.ForcaVendas.Worker`) rodando com `CatalogSyncJob` (sincroniza FTP -> Redis) e `ResultPollingJob` (sincroniza resultados FTP -> Banco local) ativos.
- Deduplicação e idempotência via `EventoIntegracaoPedidoEntity`.

### ✅ Adaptador ERP local (100% COMPLETA)
- O projeto `Versatus.ForcaVendas.ErpAdapter` foi criado e integrado localmente.
- **Mapeamento de Filial**: O arquivo `appsettings.json` do adaptador associa UUIDs de tenants a IDs de filiais do ERP (`FilialId`).
- **CatalogExporter**: Exporta dados reais do banco SQL Server Express do ERP legado a partir das views `VWCLIENTE`, `VWRITEMESTOQUE`, `VENTABELAPRECOESTOQUE` e `GLOCONDICAOPAGAMENTO` filtrados por filial, gravando no FTP de integração.
- **OrderImporter**: Lê pedidos JSON do FTP, insere-os diretamente nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA` com a filial correspondente, e monitora o faturamento das vendas (`PROCESSADA = 1` no banco) para devolver arquivos de resultado no FTP.

### ✅ PWA e Modo Offline (Fase P1 - 100% COMPLETA)
- App configurada como PWA com manifest.json.
- Banco IndexedDB (via Dexie.js) armazenando cache local do catálogo de clientes, produtos e tabelas no login.
- Criação de pedidos offline persistida localmente.
- Fila de sincronização automática (`syncQueue.ts`) que despacha os pedidos salvos assim que a rede é restabelecida.

---

## PRÓXIMOS PASSOS E TAREFAS PENDENTES

### 🔴 BLOCO 1: Validação E2E com Dados Reais
- **T-VAL-01**: Iniciar o `ErpAdapter` localmente apontando para a base real do SQL Server.
- **T-VAL-02**: Verificar a geração correta dos catálogos (clientes, produtos, preços) e validação da segmentação por filial.
- **T-VAL-03**: Submeter um pedido pela aplicação web e validar a correta inserção de cabeçalhos, itens e parcelas nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA`.
- **T-VAL-04**: Atualizar manualmente o status de faturamento no SQL Server (`PROCESSADA = 1`, `IDVENDOCUMENTO = 999`) e certificar-se de que o retorno chega à app principal através do FTP.

### 🔴 BLOCO 2: Sincronização Incremental Híbrida e Controle Manual sob Demanda
- **T-INC-01**: Configurar intervalo do Delta Sync no `appsettings.json` do `ErpAdapter`.
- **T-INC-02**: Implementar o filtro por data de alteração (`@UltimoSync`) no `CatalogExporter.cs` para clientes, produtos e preços.
- **T-INC-03**: Implementar o agendamento de Full Sync diário (carga total às 03:00h da madrugada) no `CatalogExporter.cs`.
- **T-INC-04**: Adaptar o `CatalogSyncJob.cs` no Worker para aplicar deltas de catálogo no Redis via `HSET`.
- **T-INC-05**: Implementar a tela `/vendedor/sincronismo` com status de tabelas locais.
- **T-INC-06**: Adicionar o atalho rápido `🔄 Sincronizar catálogo` na tela de Nova Venda do frontend para rodar atualização unificada (todas as tabelas) em background.
- **T-INC-07**: Escrever testes e validar o fluxo de atualização manual em tempo de venda.

> **Nota de Volumetria e Escala**: 
> - **Redis**: O catálogo unificado de 10k produtos, 5k clientes e preços consome ~8.5 MB por tenant. Para 100 tenants ativos, isso representa ~850 MB, o que é de baixíssimo custo de RAM e gerencia sub-milissegundos.
> - **IndexedDB**: Navegadores modernos permitem usar até 50% do espaço livre em disco para IndexedDB. O catálogo de 8.5 MB consome menos de 0.2% da cota. O download do JSON trafega compactado na rede como ~1.5 MB (menos de 3s em 4G) e a inserção `bulkPut` no Dexie.js leva menos de 100ms. Buscas indexadas levam menos de 5ms.

### 🟡 BLOCO 3: PWA Fase P2 — Controle Híbrido de Estoque (Estoque Estrito)
- **T-STOCK-01**: Criar parâmetro global no backend `"Sales:StockControlMode"` com valor `Strict` / `Disabled`.
- **T-STOCK-02**: Expor essa regra operacional por tenant através de um endpoint `/api/sales/config`.
- **T-STOCK-03**: No frontend, validar a quantidade vendida de acordo com a configuração de controle de estoque do tenant e a flag individual de controle de estoque do produto.
- **T-STOCK-04**: No adaptador ERP (`OrderImporter.cs`), validar se há saldo físico real no ERP antes de inserir a venda caso o modo seja `Strict`, gerando retorno de erro se houver ruptura.

### 🟡 BLOCO 4: Extensão de Transportes (Google Drive / RabbitMQ)
- **T-TRANS-01**: Concluir a implementação da classe `GoogleDriveIntegrationTransport` com a API do Google Drive v3 caso o cliente solicite sincronização via nuvem.
- **T-TRANS-02**: Concluir a implementação da classe `RabbitMqIntegrationTransport` conectando-se ao broker para ambientes com faturamento push em tempo real.
- **T-TRANS-03**: Garantir que as configurações de `appsettings.json` do backend principal e do `ErpAdapter` permitam mudar o `Integration:Transport` de forma transparente sem quebrar o faturamento.

---

## REGRAS DE EXECUÇÃO PARA A IA

1. **Acessar Dados Reais**: Use as views e tabelas do banco local `versatus` na instância `DESKTOP-PA7RCSD\SQLEXPRESS2008` (credenciais em `appsettings.json`).
2. **Strategy Pattern para Integração**: Qualquer alteração no fluxo de envio/recebimento de dados da integração DEVE utilizar a abstração `IIntegrationTransport`.
3. **Isolamento de Filial**: Garanta que as operações do ERP respeitem o mapeamento de filial por tenant.
4. **Preservação de Código**: Mantenha a estabilidade do PWA Offline e do cache local do catálogo ao fazer novos ajustes no frontend.
