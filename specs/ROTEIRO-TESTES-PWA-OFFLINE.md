# Roteiro de Testes e Quickstart - PWA & Modo Offline

Este documento descreve o passo a passo completo para executar a infraestrutura do projeto localmente e validar as funcionalidades do modo offline (PWA), sincronização automática e tratamento de conflitos.

---

## 1. Credenciais de Teste (Usuários de Demonstração)

Os seguintes usuários estão previamente cadastrados e semeados no banco de dados:

* **Usuário Demo 1 (Tenant 1)**:
  * **E-mail**: `admin@demo1.versatus.com`
  * **Senha**: `Mudar@!123`
* **Usuário Demo 2 (Tenant 2)**:
  * **E-mail**: `gestor@demo2.versatus.com`
  * **Senha**: `Mudar@!123`

---

## 2. Preparação da Infraestrutura local

A aplicação necessita de **RabbitMQ**, **PostgreSQL** e **Redis** ativos localmente.

> [!WARNING]
> Certifique-se de que o **Docker Desktop** esteja iniciado e rodando no seu computador antes de executar os comandos abaixo.

### Opção A: Subir via Docker Compose
Se preferir subir os serviços configurados do repositório:
```powershell
docker compose up -d
```

### Opção B: Subir Containers Separados (Fallback)
Caso prefira rodar os containers individuais na porta padrão esperada pelos ambientes de desenvolvimento:
```powershell
# Iniciar o PostgreSQL com o banco de dados e credenciais padrão
docker run --name fvs-postgres -p 5432:5432 -e POSTGRES_USER=postgres -e POSTGRES_PASSWORD=Mudar@!123 -e POSTGRES_DB=forca_vendas_dev -d postgres

# Iniciar o Redis para gerenciamento de sessões
docker run --name fvs-redis -p 6379:6379 -d redis

# Iniciar o RabbitMQ para fila de mensagens assíncronas
docker run --name fvs-rabbitmq -p 5672:5672 -p 15672:15672 -d rabbitmq:3.13-management
```

---

## 3. Preparação do Banco de Dados (.NET Entity Framework)

Com o PostgreSQL rodando, execute os seguintes comandos na raiz do projeto para restaurar as ferramentas locais e atualizar a estrutura de tabelas:

```powershell
# Restaura o dotnet-ef local
dotnet tool restore

# Aplica as migrações e insere os dados de Seed iniciais
dotnet ef database update --project src/backend/Versatus.ForcaVendas.Infrastructure --startup-project src/backend/Versatus.ForcaVendas.Api
```

---

## 4. Iniciando a Aplicação (Multi-Serviços)

Abra **4 terminais diferentes** na raiz do projeto e execute os comandos a seguir em cada um:

### Terminal 1: Backend API (.NET 8)
```powershell
dotnet run --project src/backend/Versatus.ForcaVendas.Api
```
* **Endpoints API**: `http://localhost:5000`
* **Swagger UI**: `http://localhost:5000/swagger`

### Terminal 2: Worker de Sincronização (.NET 8)
```powershell
dotnet run --project src/worker/Versatus.ForcaVendas.Worker
```

### Terminal 3: Adaptador ERP Legado (.NET 8)
```powershell
dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter
```

### Terminal 4: Frontend Next.js (PWA)
```powershell
cd src/frontend/app
npm install
npm run dev
```
* **URL do App**: `http://localhost:3000`

> [!IMPORTANT]
> **Aviso sobre o teste Offline em modo de Desenvolvimento (`npm run dev`)**:
> Em modo de desenvolvimento (`npm run dev`), o Next.js (Turbopack) compila os arquivos e componentes dinamicamente sob demanda. Se você colocar o navegador em modo **Offline** sem antes ter carregado todos os elementos, o navegador não conseguirá baixar as partes do código pendentes, gerando o erro de carregamento de bloco (`ChunkLoadError`).
> Para validar o funcionamento offline real do PWA de forma robusta, recomenda-se rodar a aplicação em **modo de produção**:
> ```powershell
> npm run build
> npm run start
> ```
> *(Caso queira testar em modo `npm run dev`, clique em todos os menus e abra os dropdowns **antes** de mudar a conexão para Offline no DevTools).*

---

## 5. Roteiro de Teste Manual da Experiência Offline (PWA)

Siga os passos a seguir usando o **Google Chrome** ou **Microsoft Edge** para validar o PWA:

### 5.1. Instalação e Primeiro Acesso
1. Abra `http://localhost:3000` no navegador.
2. Efetue login com `admin@demo1.versatus.com` / `Mudar@!123`.
3. Na barra de endereços (próximo à estrela de favoritos), clique no ícone **Instalar aplicativo (+)** para rodar o app em modo Standalone (sem a moldura do navegador).
4. No console do desenvolvedor (F12), você verá logs indicando a sincronização inicial:
   `[Offline Sync] Sincronização do catálogo concluída com sucesso!` (clientes e produtos salvos no IndexedDB).

### 5.2. Testando a perda de conexão (Modo Offline)
1. Com a ferramenta do desenvolvedor aberta (**F12**), vá até a aba **Network (Rede)**.
2. Altere a opção de conexão de *No throttling (Sem limitação)* para **Offline**.
3. O aplicativo exibirá imediatamente o banner superior animado:
   **"Modo Offline Ativo. Vendas serão salvas localmente no dispositivo."**
4. A Topbar (cabeçalho) exibirá um status de bolinha amber pulsante com o texto **"Offline"**.

### 5.3. Cadastro de Pedido Offline
1. Vá até a tela de **Pedidos** e clique em **Novo Pedido**.
2. Faça a busca do cliente e adicione produtos no carrinho (todo o catálogo é servido instantaneamente a partir do cache IndexedDB).
3. Finalize a venda. O aplicativo gerará um ID único local (UUID) e salvará transacionalmente.
4. O cabeçalho na Topbar passará a indicar o badge pulsante **"1 Pendente"**.
5. O novo pedido estará no topo da tabela de histórico com o status amarelo **"Aguardando Rede"**.

### 5.4. Sincronização Automática com a Rede
1. No DevTools (aba Rede), retorne a conexão para **No throttling** (Online).
2. O aplicativo detectará o evento de rede reestabelecida automaticamente.
3. O banner superior mudará para **"Conexão restabelecida. Sincronizando pedidos pendentes..."** e em seguida **"Online. Todos os pedidos locais foram sincronizados com sucesso!"**.
4. A listagem de pedidos recarregará automaticamente e o pedido sincronizado receberá o status oficial integrado (ex: **"Enviado"** ou **"Processado"**).

### 5.5. Simulação de Erro de Estoque / Rejeição do ERP
1. Fique **Offline** no DevTools novamente.
2. Crie uma venda contendo a palavra `erro` no campo de **Observações** do pedido.
3. Finalize o pedido (ele ficará aguardando sincronização local).
4. Restabeleça a conexão para **Online**.
5. O aplicativo tentará enviar o pedido, mas o mock de negócio no Adaptador ERP rejeitará a gravação por erro de saldo/validação.
6. O pedido ficará com status vermelho **"Erro de Estoque"** e a causa da falha estará impressa em vermelho abaixo do nome do cliente.
7. No botão de Ações (três pontinhos) deste pedido, você poderá escolher entre **"Tentar Enviar Novamente"** ou **"Excluir Rascunho"** local.

### 5.6. Sincronização Manual sob Demanda e Diagnóstico do Catálogo
1. Vá até a tela de **Nova Venda**.
2. Note o botão **🔄 Sync** localizado logo ao lado do campo de busca de Clientes.
3. Clique em **🔄 Sync**: o ícone de sincronismo girará indicando a atualização enquanto o app baixa em paralelo todos os Clientes, Produtos, Tabelas de Preços e Condições de Pagamento em background da API e grava no banco local IndexedDB.
4. No menu lateral, navegue até a tela **Sincronismo** (ícone de setas circulares).
5. Esta tela apresentará o status diagnóstico detalhado da sincronização local:
   - Quantidade de registros locais salvos no IndexedDB para cada tabela: **Clientes**, **Produtos**, **Tabelas de Preço** e **Condições de Pagamento**.
   - Data e hora da última sincronização geral concluída com sucesso.
6. Clique no botão principal **[ Sincronizar Catálogo Completo ]** para forçar uma atualização transacionada de todas as entidades de uma vez só e observe os contadores e badges atualizarem reativamente.

### 5.7. Verificação de Descrições de Tabela de Preço
1. Ao adicionar itens no carrinho de vendas ou ao listar as Tabelas de Preço, certifique-se de que a **descrição textual** da tabela de preço (ex: *"Tabela Padrao Varejo"*) está sendo exibida e carregada corretamente em vez de apenas o ID numérico.
2. Isso valida a integração do SQL Server via `LEFT JOIN VENTABELAPRECO` que traz o nome descritivo de cada tabela.

### 5.8. Parametrizando os Horários e Intervalos de Sincronismo
1. No arquivo [appsettings.json](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/src/erp-adapter/Versatus.ForcaVendas.ErpAdapter/appsettings.json) do **Adaptador ERP**, você pode configurar o tempo entre as sincronizações parciais (Delta) e o horário da carga completa (Full):
   * `CatalogExportIntervalSeconds`: Período em segundos do Delta Sync (Padrão: `300` - 5 minutos).
   * `FullSyncHour` (por Tenant): Horário da madrugada para execução da carga completa (Padrão: `3`).
2. Para testar o **Delta Sync** rapidamente, diminua o `CatalogExportIntervalSeconds` para `10` segundos.
3. Para testar o **Full Sync** diário, ajuste o valor da propriedade `FullSyncHour` na seção do seu tenant correspondente para o horário local de agora (ex: se são 15h, coloque `15`). Reinicie o **Adaptador ERP** e observe nos logs o disparo da carga total (Full Sync).

### 5.9. Validação de Registros Ativos vs Inativos (Regra de Negócio)
1. No banco de dados do ERP (`SQL Server`), altere o campo `ATIVO` de um cliente ou produto para `0` (Inativo).
2. Execute ou aguarde o ciclo de sincronização do ERP Adapter.
3. No aplicativo PWA, clique em **🔄 Sync**.
4. Pesquise pelo registro inativado e confirme que ele **não é exibido** e não foi sincronizado para o IndexedDB local. O catálogo offline traz por padrão apenas os registros marcados como ativos (`ATIVO = 1` no ERP).
