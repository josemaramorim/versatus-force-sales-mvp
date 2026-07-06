# Guia Unificado de Problemas e Soluções (Troubleshooting)

Este documento reúne de forma estruturada os diagnósticos de falhas, causas raízes e procedimentos passo a passo de resolução para todo o ecossistema do **Versatus Force Sales** (PWA, API, Worker, ERP Adapter e Infraestrutura).

---

## 🗺️ Sumário por Tema

1. [1. Frontend PWA e IndexedDB (Offline-First)](#1-frontend-pwa-e-indexeddb-offline-first)
   * [1.1. Tela Vazia (Sem Clientes, Produtos ou Condições de Pagamento)](#11-tela-vazia-sem-clientes-produtos-ou-condições-de-pagamento)
   * [1.2. Bloqueio de Rede por Erro de CORS](#12-bloqueio-de-rede-por-erro-de-cors)
   * [1.3. Conexão do Frontend com a API Aponta para `localhost` ou `undefined`](#13-conexão-do-frontend-com-a-api-aponta-para-localhost-ou-undefined)
   * [1.4. Conflito/Colisão de Cadastro ao Sincronizar Novo Cliente Offline](#14-conflitocolisão-de-cadastro-ao-sincronizar-novo-cliente-offline)
   * [1.5. Significado e Exclusão de Pedidos em Status "Rascunho" ou "Pendente Sync"](#15-significado-e-exclusão-de-pedidos-em-status-rascunho-ou-pendente-sync)
2. [2. Sincronização do Catálogo (FTP/SFTP, Redis e Worker)](#2-sincronização-do-catálogo-ftpsftp-redis-e-worker)
   * [2.1. Catálogo Sumiu após Carga Incremental (Delta Sync)](#21-catálogo-sumiu-após-carga-incremental-delta-sync)
   * [2.2. Falha de Autenticação FTP/SFTP nos Logs do Worker ou ERP Adapter](#22-falha-de-autenticação-ftpsftp-nos-logs-do-worker-ou-erp-adapter)
   * [2.3. Redis Sem Dados / Falha de Conexão com o Redis](#23-redis-sem-dados--falha-de-conexão-com-o-redis)
   * [2.4. Falta ou Inconsistência de Preços por Tabela de Preço não Atribuída](#24-falta-ou-inconsistência-de-preços-por-tabela-de-preço-não-atribuída)
3. [3. Processamento de Pedidos e ERP Legado (API, RabbitMQ, SQL Server)](#3-processamento-de-pedidos-e-erp-legado-api-rabbitmq-sql-server)
   * [3.1. Pedido Sincronizado no App Não Entra no Banco do ERP Local](#31-pedido-sincronizado-no-app-não-entra-no-banco-do-erp-local)
   * [3.2. Erro: "String or binary data would be truncated" no SQL Server local](#32-erro-string-or-binary-data-would-be-truncated-no-sql-server-local)
   * [3.3. Pré-cliente (Novo Cliente) Não É Excluído de `MOBPRECLIENTE`](#33-pré-cliente-novo-cliente-não-é-excluído-de-mobprecliente)
   * [3.4. Erros Contábeis/Fiscais no Faturamento Bloqueiam o Pedido no ERP](#34-erros-contábeisfiscais-no-faturamento-bloqueiam-o-pedido-no-erp)
4. [4. Infraestrutura e Docker Local](#4-infraestrutura-e-docker-local)
   * [4.1. Erro: "Port 5432 (ou 6379, 5672) is already allocated"](#41-erro-port-5432-ou-6379-5672-is-already-allocated)
   * [4.2. Erro: "Cannot connect to the Docker daemon"](#42-erro-cannot-connect-to-the-docker-daemon)
   * [4.3. Contêineres Iniciados, mas Serviços .NET Acusam Erros de Conexão](#43-contêineres-iniciados-mas-serviços-net-acusam-erros-de-conexão)
   * [4.4. Bloqueio de DLLs em C# Durante Builds Locais](#44-bloqueio-de-dlls-em-c-durante-builds-locais)
5. [5. Deploy e Compilação VPS (Painel ICP, Next.js, Node)](#5-deploy-e-compilação-vps-painel-icp-nextjs-node)
   * [5.1. Limite de Memória Atingido no Build do Next.js (Heap Out of Memory)](#51-limite-de-memória-atingido-no-build-do-nextjs-heap-out-of-memory)
   * [5.2. Erro `mkdir ...: not a directory` ao Descompactar ZIP no ICP](#52-erro-mkdir--not-a-directory-ao-descompactar-zip-no-icp)
   * [5.3. Erro `sh: next: Permission denied` ao Iniciar Frontend](#53-erro-sh-next-permission-denied-ao-iniciar-frontend)
   * [5.4. Erro `ERR_SSL_PROTOCOL_ERROR` ao Acessar via Porta HTTPS](#54-erro-err_ssl_protocol_error-ao-acessar-via-porta-https)
   * [5.5. Erro `MSB1003: Specify a project or solution file` no Deploy do Worker/API](#55-erro-msb1003-specify-a-project-or-solution-file-no-deploy-do-workerapi)

---

## 1. Frontend PWA e IndexedDB (Offline-First)

### 1.1. Tela Vazia (Sem Clientes, Produtos ou Condições de Pagamento)
* **Sintomas**: O usuário faz login, a tela de nova venda carrega, mas os filtros de clientes, produtos e condições de pagamento estão vazios.
* **Causa Raiz**: O banco local no IndexedDB do navegador não foi populado ou o Redis na nuvem foi limpo/sobrescrito com arquivos Delta vazios da integração.
* **Solução**:
  1. No aplicativo PWA, acesse a página de **Sincronismo** (`/sincronismo`).
  2. Clique em **Sincronizar Catálogo Completo**. Isso força a API a repopular o banco local (IndexedDB) a partir do Redis.
  3. Caso persista, execute uma carga total do catálogo no **ERP Adapter** (excluindo os arquivos `last_sync_*.txt` na máquina local do cliente) e force o reinício do **Worker** no painel VPS para reabastecer o Redis.

### 1.2. Bloqueio de Rede por Erro de CORS
* **Sintomas**: A tela não carrega informações da API e o console do navegador exibe mensagens em vermelho de bloqueio por política de CORS.
* **Causa Raiz**: A URL do frontend (IP/porta da VPS ou domínio do cliente) não está explicitamente registrada na lista de origens autorizadas pela API.
* **Solução**:
  1. Acesse o **Painel ICP** da API (`force-sales-api`).
  2. Vá em **Variáveis de Ambiente** e localize `CORS__ALLOWEDORIGINS`.
  3. Adicione o domínio/IP exato do frontend (ex: `https://meuapp.vendas.com` ou `http://23.80.91.77:3000`), separando por vírgula se houver outros.
  4. Salve e reinicie a aplicação da API.

### 1.3. Conexão do Frontend com a API Aponta para `localhost` ou `undefined`
* **Sintomas**: Cliques no aplicativo dão erro de rede e o console do navegador indica tentativas de conexão falhas para `http://localhost:5000/...` ou `http://undefined/...`.
* **Causa Raiz**: A variável de build `NEXT_PUBLIC_API_URL` não foi configurada antes de gerar a compilação do Next.js.
* **Solução**:
  1. No Painel ICP, vá nas configurações do Frontend (`force-sales-web`).
  2. Garanta que a variável `NEXT_PUBLIC_API_URL` esteja criada com o valor correto (ex: `https://api.vendas.com` ou `http://23.80.91.77:5000`).
  3. Faça o **Rebuild** (ou novo deploy) da aplicação para reinjetar a URL estaticamente nos arquivos finais.

### 1.4. Conflito/Colisão de Cadastro ao Sincronizar Novo Cliente Offline
* **Sintomas**: O vendedor faz o pré-cadastro de um novo cliente em modo offline. Ao retornar online, a fila falha em sincronizar e exibe erro de duplicação.
* **Causa Raiz**: O CPF/CNPJ ou Nome fornecido no pré-cadastro local do tablet já foi cadastrado por outro vendedor no catálogo oficial.
* **Solução**:
  1. A API rejeita a requisição retornando `ValidationProblem` para evitar cadastros repetidos na base.
  2. O suporte ou o vendedor devem ajustar os dados de pré-cadastro no modal ou cancelar a venda offline se o cadastro oficial já existir no sistema, vinculando o pedido de venda diretamente ao cliente oficial já cadastrado.

### 1.5. Significado e Exclusão de Pedidos em Status "Rascunho" ou "Pendente Sync"
* **Sintomas**: O usuário visualiza pedidos com o status "Rascunho", "Aguardando Rede" (`pendente_sync`) ou "Erro de Estoque" (`erro_sync`) no histórico de pedidos e deseja removê-los ou entender o que significam.
* **O que significa**:
  * **Rascunho / Pendente Sync**: Pedidos criados localmente no dispositivo (offline ou online) que ainda não foram enviados com sucesso para o banco de dados oficial do servidor.
  * **Erro de Estoque / Erro Sync**: Pedidos que tentaram sincronizar com a API na nuvem mas foram rejeitados por problemas cadastrais, falta de estoque ou duplicidade de dados no ERP.
* **Ação de Exclusão ("Excluir Rascunho")**:
  1. A ação de **Excluir Rascunho** fica disponível no menu de três pontos apenas para os pedidos que residem no IndexedDB local do dispositivo.
  2. Ao selecionar essa ação, o registro é removido fisicamente do banco de dados local IndexedDB (`db.pedidos.delete(orderId)`).
  3. **Nota de Atenção**: Pedidos que já foram sincronizados com sucesso (status `sincronizado`, `enviado`, `processado`) **não podem ser excluídos pelo vendedor** no aplicativo para garantir a integridade dos dados integrados ao ERP.
---

## 2. Sincronização do Catálogo (FTP/SFTP, Redis e Worker)

### 2.1. Catálogo Sumiu após Carga Incremental (Delta Sync)
* **Sintomas**: Clientes e produtos de um determinado tenant desaparecem após alguns minutos de uso.
* **Causa Raiz**: O ERP Adapter executa ciclos Delta (incrementais). Se não houver modificações recentes no SQL Server local, ele gera arquivos JSON com o array de dados vazio (`"data": []`) e os envia ao FTP, sobrescrevendo os arquivos completos. O Worker lê esses arquivos vazios e apaga o cache do Redis.
* **Solução**:
  1. A alteração na lógica de consulta no ERP Adapter e no Worker deve ser implementada para nunca limpar o Redis caso os dados incrementais estejam vazios.
  2. Para corrigir imediatamente no banco: na máquina local do cliente, vá até a pasta do **ERP Adapter**, exclua os arquivos de controle `last_sync_*.txt` e reinicie o ERP Adapter para forçar uma Carga Total (Full Sync).

### 2.2. Falha de Autenticação FTP/SFTP nos Logs do Worker ou ERP Adapter
* **Sintomas**: Mensagem de log `Renci.SshNet.Common.SshAuthenticationException: Permission denied (password)`.
* **Causa Raiz**: Credenciais do SFTPGo de integração para o tenant estão incorretas.
* **Solução**:
  1. Acesse o painel de controle do **SFTPGo** (padrão porta `8282` na VPS).
  2. Confirme se a conta de usuário está ativa e se a senha configurada no banco confere.
  3. Confirme se as variáveis de ambiente (como `INTEGRATION__FTP__PASSWORD` no Worker e no `appsettings.json` do ERP Adapter) possuem o valor correspondente atualizado.

### 2.3. Redis Sem Dados / Falha de Conexão com o Redis
* **Sintomas**: Logs da API ou Worker mostram `StackExchange.Redis.RedisConnectionException: It was not possible to connect...`.
* **Causa Raiz**: O contêiner de Redis não está respondendo, está desligado ou o formato da Connection String está incorreto.
* **Solução**:
  1. No painel de contêineres/VPS, verifique se o contêiner do Redis está rodando normalmente.
  2. Verifique o formato da variável `CONNECTIONSTRINGS__REDIS`:
     * A senha deve estar no padrão: `localhost:6379,password=SUA_SENHA,abortConnect=false`
     * Certifique-se de que o host da conexão está correto para cada serviço (se estiver rodando dentro do Docker na VPS, use o nome do serviço/rede do Redis, ex: `fvs-redis`).

### 2.4. Falta ou Inconsistência de Preços por Tabela de Preço não Atribuída
* **Sintomas**: Produtos aparecem no catálogo do PWA mas sem preço associado, impossibilitando a venda.
* **Causa Raiz**: O cliente no ERP legado não possui uma tabela de preço padrão configurada, ou a tabela de preços não foi enviada no arquivo `tabelas-preco.json`.
* **Solução**:
  1. Verifique a tabela `VENTABELAPRECOESTOQUE` no SQL Server da filial local para confirmar se há preços cadastrados para a filial em questão.
  2. Certifique-se de que o parâmetro `tabelaPrecoIdDefault` nos parâmetros do tenant (`tenant-parameters.json`) aponta para uma tabela de preços existente e ativa.

---

## 3. Processamento de Pedidos e ERP Legado (API, RabbitMQ, SQL Server)

### 3.1. Pedido Sincronizado no App Não Entra no Banco do ERP Local
* **Sintomas**: O vendedor recebe a confirmação de que o pedido foi enviado, mas o faturamento local não visualiza a venda na tabela `MOBVENDA`.
* **Causa Raiz**: O fluxo de integração quebrou em algum dos nós.
* **Diagnóstico Passo a Passo**:
  1. **API (PostgreSQL)**: Acesse o banco Postgres e veja se o pedido está lá com status `enviado` ou `pendente_sync`. Se não estiver, o app falhou no envio.
  2. **RabbitMQ**: Acesse o painel do RabbitMQ (porta `15672`). Se a fila `pedidos.pendentes.erp` tiver mensagens retidas, o **ERP Adapter** local está desligado ou sem comunicação com o broker.
  3. **Servidor FTP (SFTPGo)**: Acesse o FTP. Se o arquivo `pedido-GUID.json` estiver na pasta `/pedidos/pendentes`, a nuvem funcionou. O erro está no ERP Adapter local ao processar o arquivo.
  4. **Logs do ERP Adapter**: Verifique os arquivos de log locais à procura de erros de conexão com o banco SQL Server do ERP legado.

### 3.2. Erro: "String or binary data would be truncated" no SQL Server local
* **Sintomas**: O ERP Adapter acusa erro crítico de banco de dados SQL Server ao tentar processar o JSON do pedido.
* **Causa Raiz**: O vendedor preencheu campos como `Observacao` ou cadastrou um pré-cliente com campos muito longos que excedem o tamanho máximo das colunas da tabela legado (ex: `MOBVENDA.OBSERVACAO` com limite de 250 caracteres).
* **Solução**:
  1. O ERP Adapter possui um helper dinâmico `SafeSubstring` que lê o comprimento das colunas do banco do cliente e trunca os valores excedentes antes de fazer o insert.
  2. Caso o erro continue ocorrendo em campos não tratados, verifique quais tabelas geraram o erro nos logs e adicione o tratamento `SafeSubstring` nelas.

### 3.3. Pré-cliente (Novo Cliente) Não É Excluído de `MOBPRECLIENTE`
* **Sintomas**: Clientes cujos pedidos já foram faturados continuam aparecendo na tabela local `MOBPRECLIENTE` indefinidamente.
* **Causa Raiz**: O ERP Adapter não conseguiu enviar a confirmação de faturamento de volta para o FTP da nuvem, ou a deleção local falhou.
* **Solução**:
  1. Certifique-se de que a conexão SFTP de retorno do ERP Adapter está funcionando sem interrupções.
  2. O comando de exclusão física local `DELETE FROM MOBPRECLIENTE WHERE NOME = @NomePreCliente` está encapsulado em transação local e é disparado **estritamente apenas após** a confirmação do upload de resultado no FTP. Verifique nos logs se há erros de permissão de escrita ou exclusão na tabela.

### 3.4. Erros Contábeis/Fiscais no Faturamento Bloqueiam o Pedido no ERP
* **Sintomas**: Pedidos de novos clientes ficam trancados na retaguarda com erro e não concluem o ciclo.
* **Causa Raiz**: O faturista do ERP cadastrou o pré-cliente no banco oficial, mas não o associou corretamente ao pedido em `MOBVENDA` (deixando `NOVOCLIENTE = 1` e `IDMOBCLIENTE` nulo), impedindo a finalização da nota.
* **Solução**:
  1. O faturista deve efetivar o pré-cadastro em `MOBPRECLIENTE` transformando-o em um cliente oficial no cadastro geral do ERP.
  2. O faturista deve vincular o ID desse novo cliente oficial ao campo `IDMOBCLIENTE` do pedido na tabela `MOBVENDA` e marcar o pedido como processado/faturado.

---

## 4. Infraestrutura e Docker Local

### 4.1. Erro: "Port 5432 (ou 6379, 5672) is already allocated"
* **Sintomas**: Ao rodar `docker-compose up` no terminal local de desenvolvimento, o Docker aborta a execução relatando porta já alocada.
* **Causa Raiz**: Você já tem uma instância nativa do PostgreSQL, Redis ou RabbitMQ instalada e rodando como serviço do Windows em segundo plano na mesma máquina.
* **Solução**:
  1. Pressione `Win + R`, digite `services.msc` e tecle Enter.
  2. Localize os serviços locais correspondentes (ex: `postgresql-x64`, `Redis` ou `RabbitMQ`).
  3. Clique com o botão direito sobre eles e escolha **Parar** (Stop). Altere o tipo de inicialização para **Manual** para evitar conflitos em novos reinícios da máquina.

### 4.2. Erro: "Cannot connect to the Docker daemon"
* **Sintomas**: Comandos do Docker no terminal falham relatando perda de conexão com o daemon do Docker.
* **Causa Raiz**: O aplicativo do Docker Desktop não está aberto ou ainda está carregando o motor de virtualização.
* **Solução**:
  1. Abra o **Docker Desktop** no Windows e aguarde até que o ícone de baleia no canto inferior esquerdo fique verde com o status "Engine Running".

### 4.3. Contêineres Iniciados, mas Serviços .NET Acusam Erros de Conexão
* **Sintomas**: Os contêineres do Docker mostram status online, mas o build/execução do código local .NET falha relatando falta de comunicação com PostgreSQL, Redis ou RabbitMQ.
* **Causa Raiz**: Conflito de resolução de endereços. A Connection String está configurada para usar nomes internos de contêineres (ex: `fvs-postgres`), que só funcionam dentro da rede privada do Docker, e não no Windows hospedeiro.
* **Solução**:
  1. Para rodar a aplicação nativamente no Windows em desenvolvimento, edite o `appsettings.Development.json` (ou `.env` correspondente) para apontar para `localhost` ou `127.0.0.1` (ex: `Host=localhost;Port=5432;...`).

### 4.4. Bloqueio de DLLs em C# Durante Builds Locais
* **Sintomas**: O comando `dotnet build` falha relatando que arquivos `.dll` do projeto estão sendo usados por outro processo.
* **Causa Raiz**: Algum serviço local (como o Worker ou a API) está ativo em background segurando as DLLs comuns de domínio e infraestrutura.
* **Solução**:
  1. Feche todos os terminais ativos de execução.
  2. Se a falha persistir por processos zumbis, rode o comando abaixo no PowerShell para encerrar todas as instâncias da aplicação:
     ```powershell
     Get-Process | Where-Object { $_.Name -like "*ForcaVendas*" } | Stop-Process -Force
     ```

---

## 5. Deploy e Compilação VPS (Painel ICP, Next.js, Node)

### 5.1. Limite de Memória Atingido no Build do Next.js (Heap Out of Memory)
* **Sintomas**: O build do frontend no painel do servidor falha silenciosamente ou exibe a mensagem `FATAL ERROR: Ineffective mark-compacts near heap limit Allocation failed - JavaScript heap out of memory`.
* **Causa Raiz**: Compilar o Next.js exige muita memória RAM temporária. Servidores VPS de menor porte (com 1GB ou 2GB de RAM) estouram a memória física e sofrem crash.
* **Solução**:
  1. No painel ICP, adicione a seguinte variável de ambiente à aplicação:
     ```env
     NODE_OPTIONS=--max-old-space-size=1024
     ```
  2. Caso a compilação no servidor continue quebrando por falta de memória física da VPS, use a **Publicação via ZIP**: compile localmente com `npm run build` e envie os arquivos compactados finais `.next`, `public`, `node_modules` e `package.json` prontos via painel.

### 5.2. Erro `mkdir ...: not a directory` ao Descompactar ZIP no ICP
* **Sintomas**: A descompactação do pacote compactado del frontend no gerenciador de arquivos web do painel falha com erros de diretórios.
* **Causa Raiz**: O Next.js utiliza parênteses nos nomes de pastas internas do App Router para criar agrupamentos lógicos (ex: `.next/server/app/(admin)`). A biblioteca web de descompactação de alguns painéis possui bugs com caracteres especiais nos nomes de pastas, rejeitando os parênteses.
* **Solução**:
  1. Evite descompactar pela interface web do painel.
  2. Conecte-se via SSH terminal no servidor e faça a extração nativa por linha de comando:
     ```bash
     unzip publish-frontend.zip -d /home/apps/force-sales-web/
     ```

### 5.3. Erro `sh: next: Permission denied` ao Iniciar Frontend
* **Sintomas**: O log da aplicação mostra erro de permissão negada ao tentar executar o script de inicialização do Next.js.
* **Causa Raiz**: O binário ou os scripts na pasta local perderam as permissões Unix de execução (`chmod +x`) ao serem descompactados.
* **Solução**:
  1. Acesse o terminal da VPS via SSH.
  2. Conceda permissão de execução na pasta do frontend:
     ```bash
     chmod -R +x /home/apps/force-sales-web/node_modules/.bin/
     ```
  3. Reinicie a aplicação.

### 5.4. Erro `ERR_SSL_PROTOCOL_ERROR` ao Acessar via Porta HTTPS
* **Sintomas**: O navegador rejeita a conexão exibindo mensagem de erro de protocolo SSL.
* **Causa Raiz**: Tentativa de acesso a uma porta de aplicação direta sem criptografia (como a porta `3000` do frontend ou `5000` da API) utilizando o protocolo `https://`.
* **Solução**:
  1. Portas diretas de node/dotnet locais não escutam HTTPS nativo no container. Acesse usando `http://` (ex: `http://23.80.91.77:3000`).
  2. Para usar HTTPS, configure um Proxy Reverso (como Nginx ou Caddy) ou configure as portas SSL de domínio no painel ICP para gerenciar os certificados SSL Let's Encrypt na porta padrão `443`.

### 5.5. Erro `MSB1003: Specify a project or solution file` no Deploy do Worker/API
* **Sintomas**: O build do backend falha com mensagens relatando namespace não encontrado ou falta de especificação de projeto.
* **Causa Raiz**: O MSBuild é executado a partir da raiz da pasta configurada. Se o repositório completo for montado na raiz `/` e o comando de compilação não apontar diretamente para o arquivo `.csproj`, o compilador falha por não achar a solução.
* **Solução**:
  1. Certifique-se de que a **Pasta do projeto** esteja configurada apontando para a raiz do repositório `/` ou `.`.
  2. Altere o **Comando de build** nas configurações do painel ICP para apontar explicitamente para o caminho do arquivo `.csproj` específico:
     * **Para a API**: `dotnet publish src/backend/Versatus.ForcaVendas.Api/Versatus.ForcaVendas.Api.csproj -c Release -o /app`
     * **Para o Worker**: `dotnet publish src/worker/Versatus.ForcaVendas.Worker/Versatus.ForcaVendas.Worker.csproj -c Release -o /app`
