# Gerenciamento de Serviços em Background (.NET / PowerShell)

Este documento descreve como gerenciar os processos e serviços em background da aplicação **Versatus Force Sales** rodando em ambiente Windows/PowerShell.

---

## 🗺️ Sumário

* [1. Os Serviços do Ecossistema](#1-os-serviços-do-ecossistema) — Papel e tecnologias de cada componente.
* [2. Comandos do PowerShell (Windows)](#2-comandos-do-powershell-windows) — Como listar e encerrar serviços locais.
  * [2.1. Listar Serviços em Execução](#21-listar-serviços-em-execução)
  * [2.2. Parar/Encerrar Todos os Serviços](#22-pararencerrar-todos-os-serviços-recomendado-antes-de-novos-builds)
  * [2.3. Parar um Serviço Específico](#23-parar-um-serviço-específico)
* [3. Como Inicializar os Serviços](#3-como-inicializar-os-serviços) — Inicialização interativa ou em segundo plano.
  * [3.1. Modo Interativo (Terminal Aberto)](#31-modo-interativo-terminal-aberto)
  * [3.2. Modo Background (Sem travar o terminal)](#32-modo-background-executar-sem-travar-o-terminal)
* [4. Guia do Docker para Leigos](#4-guia-do-docker-para-leigos) — Entendendo a infraestrutura local.
  * [4.1. O que é o Docker?](#41-o-que-é-o-docker-e-por-que-usamos)
  * [4.2. Pré-requisito Fundamental: Docker Desktop](#42-pré-requisito-fundamental-docker-desktop)
  * [4.3. Os 4 Serviços Rodando no Docker e Credenciais](#43-os-4-serviços-rodando-no-docker-e-suas-credenciais)
  * [4.4. Gerenciando Pelo Docker Desktop (Modo Visual)](#44-gerenciando-pelo-docker-desktop-modo-visual---recomendado-para-leigos)
  * [4.5. Gerenciando Pelo Terminal](#45-gerenciando-pelo-terminal-powershell-ou-cmd-na-raiz-do-projeto)
  * [4.6. Comandos de Limpeza e Manutenção](#46-comandos-de-limpeza-e-manutenção-do-sistema)
  * [4.7. Solução de Problemas Comuns (Troubleshooting)](#47-solução-de-problemas-comuns-troubleshooting)
* [5. Fluxo de Criação de Tenant e Sincronização de Catálogo (Alimentação do Redis)](#5-fluxo-de-criação-de-tenant-e-sincronização-de-catálogo-alimentação-do-redis) — Como o cache do Redis é atualizado.
* [6. Configuração para Execução Local de Desenvolvimento (Ambiente Misto)](#6-configuração-para-execução-local-de-desenvolvimento-ambiente-misto) — Como apontar os serviços locais para o Docker e para o SQL Server local.

---

## 1. Os Serviços do Ecossistema

A aplicação **Versatus Force Sales** é composta por quatro componentes principais que trabalham de forma coordenada:

1.  **Frontend (`Next.js / React`)**:
    *   **O que é**: A interface visual (aplicativo web responsivo) acessada pelos vendedores em smartphones, tablets ou computadores.
    *   **Tecnologia**: Next.js, React, TailwindCSS / NextUI.
    *   **Função**: Exibir o catálogo de produtos/preços, gerenciar a seleção de clientes, registrar novos pedidos de venda (mesmo offline) e exibir o histórico de pedidos.

2.  **API Gateway (`Versatus.ForcaVendas.Api`)**:
    *   **O que é**: O backend principal da aplicação que expõe os endpoints HTTP/REST.
    *   **Tecnologia**: C# .NET 10 (ASP.NET Core).
    *   **Função**: Atender às requisições do Frontend, validar regras de negócio, gerenciar a autenticação e autorização dos usuários, e gravar/ler dados diretamente no banco de dados operacional (**PostgreSQL**).

3.  **Worker (`Versatus.ForcaVendas.Worker`)**:
    *   **O que é**: Um serviço de processamento assíncrono em segundo plano (background service) rodando na nuvem.
    *   **Tecnologia**: C# .NET 10.
    *   **Função**: Escutar e processar filas de mensagens do **RabbitMQ**, além de gerenciar a sincronização do catálogo de produtos e clientes direto no banco de cache (**Redis**).

4.  **ERP Adapter (`Versatus.ForcaVendas.ErpAdapter`)**:
    *   **O que é**: Um serviço integrador executado localmente (on-premise) na infraestrutura ou rede interna do cliente.
    *   **Tecnologia**: C# .NET 10.
    *   **Função**: Conecta-se diretamente ao banco de dados **SQL Server** do ERP legado da filial para extrair dados de catálogo (clientes, produtos, tabelas de preço e condições de pagamento) e enviá-los para a nuvem via FTP/SFTP, além de baixar novos pedidos da nuvem e inseri-los no SQL Server local.

---

## 2. Comandos do PowerShell (Windows)

Ao trabalhar localmente, esses serviços podem bloquear arquivos DLL comuns (como `Versatus.ForcaVendas.Domain.dll`), impedindo novos builds se já estiverem em execução. Use os comandos abaixo no PowerShell para gerenciá-los:

### 2.1. Listar Serviços em Execução
Para checar quais processos da aplicação estão rodando no momento:

```powershell
Get-Process | Where-Object { $_.Name -like "*ForcaVendas*" }
```

### 2.2. Parar/Encerrar Todos os Serviços (Recomendado antes de novos builds)
Para encerrar todos os três serviços simultaneamente de forma forçada:

```powershell
Get-Process | Where-Object { $_.Name -like "*ForcaVendas*" } | Stop-Process -Force
```

### 2.3. Parar um Serviço Específico
Caso queira fechar apenas um dos processos:

*   **Parar apenas a API:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.Api" -Force
    ```
*   **Parar apenas o Worker:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.Worker" -Force
    ```
*   **Parar apenas o Adaptador ERP:**
    ```powershell
    Stop-Process -Name "Versatus.ForcaVendas.ErpAdapter" -Force
    ```

---

## 3. Como Inicializar os Serviços

### 3.1. Modo Interativo (Terminal Aberto)
Execute na pasta raiz do projeto:

*   **API:** `dotnet run --project src/backend/Versatus.ForcaVendas.Api`
*   **Worker:** `dotnet run --project src/worker/Versatus.ForcaVendas.Worker`
*   **ERP Adapter:** `dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter`

### 3.2. Modo Background (Executar sem travar o terminal)
Se você deseja iniciar os serviços em segundo plano no PowerShell sem precisar manter três abas do terminal abertas:

*   **API:**
    ```powershell
    Start-Job -Name "FV-Api" -ScriptBlock { dotnet run --project src/backend/Versatus.ForcaVendas.Api }
    ```
*   **Worker:**
    ```powershell
    Start-Job -Name "FV-Worker" -ScriptBlock { dotnet run --project src/worker/Versatus.ForcaVendas.Worker }
    ```
*   **ERP Adapter:**
    ```powershell
    Start-Job -Name "FV-Adapter" -ScriptBlock { dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter }
    ```

*   **Para listar os jobs ativos:** `Get-Job`
*   **Para parar os jobs ativos:** `Get-Job | Stop-Job`

---

## 4. Guia do Docker para Leigos

### 4.1. O que é o Docker e por que usamos?
Imagine que cada banco de dados ou serviço que o sistema precisa é como um eletrodoméstico que precisa de uma tomada e voltagem específicas. Se tentarmos instalar todos diretamente no seu Windows (como PostgreSQL, Redis, RabbitMQ e um Servidor FTP), isso pode gerar conflitos com outros programas já instalados, exigir configurações manuais complexas e deixar o computador lento.

O **Docker** funciona criando "caixas virtuais isoladas" (chamadas de **Contêineres**) para cada um desses serviços. Tudo já vem configurado e pronto para uso dentro dessas caixas. 
Toda a infraestrutura necessária para o **Versatus Force Sales** rodar localmente é descrita em um único arquivo de receitas na raiz do projeto chamado `docker-compose.yml`.

---

### 4.2. Pré-requisito Fundamental: Docker Desktop
Para que qualquer comando ou serviço do Docker funcione, o aplicativo **Docker Desktop** deve estar instalado no seu computador e **em execução**.

*   **Como saber se está rodando?** 
    *   Verifique se há um ícone de baleia na barra de tarefas do Windows (perto do relógio).
    *   Abra o programa **Docker Desktop**. No canto inferior esquerdo, deve haver uma barra verde indicando **"Engine running"** (Motor em execução).
*   **Erro comum se não estiver rodando:** Ao tentar usar o Docker no terminal, você verá uma mensagem de erro longa contendo termos como *"error during connect"* ou *"daemon is not running"*. Para corrigir, basta abrir o Docker Desktop e aguardar ele inicializar.

---

### 4.3. Os 4 Serviços Rodando no Docker e suas Credenciais
Quando você inicia o Docker do projeto, quatro servidores independentes sobem automaticamente na sua máquina. Abaixo estão os detalhes de acesso para cada um, caso precise conectar ferramentas externas (como DBeaver, pgAdmin, ou clientes FTP):

#### 1. Banco de Dados: `fvs-postgres` (PostgreSQL)
*   **O que faz:** Guarda todas as informações persistentes do sistema (usuários, sessões, pedidos e tabelas de configuração).
*   **Como conectar por fora (ex: DBeaver / pgAdmin):**
    *   **Host (Servidor):** `localhost` (ou `127.0.0.1`)
    *   **Porta:** `5432`
    *   **Banco de Dados (Database):** `forca_vendas_dev`
    *   **Usuário (Username):** `postgres`
    *   **Senha (Password):** `Mudar@!123`

#### 2. Cache em Memória: `fvs-redis` (Redis)
*   **O que faz:** Armazena temporariamente os catálogos de produtos, preços e clientes organizados por Tenant (Cliente/Empresa) para que o aplicativo abra os dados instantaneamente.
*   **Como conectar:**
    *   **Host:** `localhost`
    *   **Porta:** `6379`
    *   *Sem senha padrão no ambiente local.*

#### 3. Servidor de Arquivos: `fvs-ftp` (Servidor FTP)
*   **O que faz:** Simula o servidor FTP da empresa. É a pasta de integração onde os arquivos JSON de sincronização (carga de produtos e retorno de pedidos) são depositados.
*   **Como conectar (ex: FileZilla):**
    *   **Host:** `localhost`
    *   **Porta:** `21`
    *   **Usuário:** `test`
    *   **Senha:** `test`
*   **Pasta no seu computador:** O Docker espelha esse servidor diretamente na pasta `integration-sync` localizada na raiz do projeto. Tudo o que você colocar lá aparecerá no FTP e vice-versa.

#### 4. Fila de Mensagens: `fvs-rabbitmq` (RabbitMQ)
*   **O que faz:** Recebe e distribui tarefas em background, garantindo que o sistema processe dados sem travar a interface.
*   **Painel Visual Web de Controle:** Você pode abrir o navegador e acessar `http://localhost:15672` para ver as filas rodando.
    *   **Usuário:** `fvs`
    *   **Senha:** `fvs_dev_pass`
*   **Porta interna de comunicação:** `5672`

---

### 4.4. Gerenciando Pelo Docker Desktop (Modo Visual - Recomendado para Leigos)
Se você prefere não usar o terminal, pode controlar os servidores visualmente pelo aplicativo **Docker Desktop**:

1.  Abra o **Docker Desktop** e vá na aba **Containers**.
2.  Você verá um grupo chamado `versatus-force-sales-mvp`. Clique nele para expandir.
3.  Lá você verá os quatro contêineres (`fvs-postgres`, `fvs-redis`, `fvs-ftp`, `fvs-rabbitmq`) com uma luz verde ao lado de cada um (indicando que estão ligados).
4.  **Botões rápidos de controle (passar o mouse por cima do contêiner):**
    *   **Play/Pause (Triângulo/Duas Barras):** Liga/Pausa o contêiner.
    *   **Stop (Quadrado):** Desliga o contêiner.
    *   **Restart (Seta circular):** Reinicia o contêiner (ótimo se algum travar).
    *   **Lixeira:** Exclui o contêiner (ele será recriado no próximo início).
5.  **Ver Logs:** Clique no nome de qualquer contêiner (ex: `fvs-postgres`) para ver as linhas de logs em tempo real na tela. Isso ajuda a ver se o banco está recebendo conexões ou acusando erros.

---

### 4.5. Gerenciando Pelo Terminal (PowerShell ou CMD na raiz do projeto)
Caso prefira usar comandos de texto, abra o terminal na pasta raiz do projeto e use os comandos abaixo:

*   **Ligar toda a infraestrutura (Iniciar tudo):**
    ```bash
    docker-compose up -d
    ```
    *(O `-d` serve para "desacoplar", rodando os contêineres silenciosamente em segundo plano, deixando o terminal livre para outros comandos).*

*   **Desligar toda a infraestrutura:**
    ```bash
    docker-compose down
    ```
    *(Desliga os servidores, mas mantém todas as tabelas e dados gravados no banco intactos).*

*   **Reiniciar os contêineres:**
    ```bash
    docker-compose restart
    ```

*   **Verificar se estão ativos e quais as portas ocupadas:**
    ```bash
    docker-compose ps
    ```

*   **Verificar logs em tempo real de todos os contêineres juntos:**
    ```bash
    docker-compose logs -f
    ```
    *(Pressione `Ctrl + C` para sair visualização de logs).*

---

### 4.6. Comandos de Limpeza e Manutenção do Sistema

*   **Zerar o Banco de Dados e Logs (Limpeza Pesada / Começar do Zero):**
    ```bash
    docker-compose down -v
    ```
    > [!WARNING]
    > O parâmetro `-v` (volumes) apaga permanentemente os dados do PostgreSQL e RabbitMQ. Use isso apenas se quiser limpar todos os dados de teste e iniciar um banco totalmente limpo. Os dados do FTP na pasta `integration-sync` **não** são apagados.

*   **Limpar TODO o cache do Redis (Forçar recarregamento do catálogo):**
    ```bash
    docker exec fvs-redis redis-cli FLUSHALL
    ```

*   **Limpar o cache do Redis de apenas um Tenant específico:**
    ```bash
    docker exec fvs-redis redis-cli DEL catalogo:{tenantId}:clientes catalogo:{tenantId}:produtos catalogo:{tenantId}:precos tenant:{tenantId}:sessions
    ```
    *(Substitua `{tenantId}` pelo ID do tenant desejado, ex: `00000000-0000-0000-0000-000000000001`).*

---

### 4.7. Solução de Problemas Comuns (Troubleshooting)

#### 1. Erro: "Port 5432 (ou 6379) is already allocated" (Porta já alocada)
*   **Causa:** Você já possui o PostgreSQL ou o Redis instalado diretamente no Windows de forma nativa e ele está usando a porta. O Docker não consegue usar a mesma porta ao mesmo tempo.
*   **Solução:** 
    1.  Abra o menu Iniciar do Windows, digite **Serviços** e abra o aplicativo.
    2.  Procure na lista por `postgresql-x64-...` (ou o nome do serviço de banco local).
    3.  Clique com o botão direito nele e escolha **Parar**.
    4.  Tente executar `docker-compose up -d` novamente no terminal.

#### 2. Erro: "Cannot connect to the Docker daemon" (Não conecta ao Docker)
*   **Causa:** O aplicativo Docker Desktop está fechado ou ainda está inicializando.
*   **Solução:** Abra o Docker Desktop e espere até que o ícone de baleia no canto inferior esquerdo fique verde.

#### 3. Os contêineres subiram, mas os serviços em C# (.NET) dão erro de conexão
*   **Causa:** Se você zerou os dados do banco Docker usando `docker-compose down -v`, a estrutura de tabelas sumiu. Os microsserviços em execução (.NET) podem se perder.
*   **Solução:** Sempre que apagar os volumes do Docker, pare os serviços rodando em C# (.NET) e inicie-os novamente para que eles refaçam as migrações automáticas de banco de dados (`database migrations`) durante a inicialização.

---

## 5. Fluxo de Criação de Tenant e Sincronização de Catálogo (Alimentação do Redis)

Quando um novo Tenant é criado (por exemplo, `00000000-0000-0000-0000-000000000003`), o fluxo para que o catálogo de produtos e clientes seja carregado com sucesso no banco de cache do **Redis** depende de dois serviços específicos atuando de forma coordenada:

### 1. ERP Adapter (`Versatus.ForcaVendas.ErpAdapter`)
* **Papel**: Exportar os dados do ERP legado local e enviá-los para a nuvem.
* **Ações necessárias**: 
  1. No arquivo `appsettings.json` (ou `appsettings.Production.json`) da instalação local do cliente, configure o novo UUID de tenant no array `Auth:Tenants` e as configurações específicas de filial em `ErpAdapter:Tenants`.
  2. Ao inicializar o serviço do **ERP Adapter**, ele lerá o banco local (SQL Server), gerará os arquivos JSON com o catálogo do novo tenant e fará o upload via SFTP para o servidor (SFTPGo) na nuvem sob o diretório:
     `/integration-sync/00000000-0000-0000-0000-000000000003/catalogo`

### 2. Worker (`Versatus.ForcaVendas.Worker`)
* **Papel**: Consumir os arquivos de integração e popular o Redis.
* **Ações necessárias**:
  1. O serviço **Worker** deve estar em execução na nuvem (VPS).
  2. Ele monitora constantemente o diretório SFTP por novos arquivos de catálogo. Ao detectar o upload feito pelo **ERP Adapter** para a nova pasta de tenant, o Worker faz a leitura desses arquivos JSON e popula os dados diretamente nas chaves correspondentes no **Redis** (`catalogo:00000000-0000-0000-0000-000000000003:*`).

Desta forma, para que o catálogo da nova tenant passe a constar e ser disponibilizado via cache no aplicativo, o **ERP Adapter** precisa rodar localmente para exportar e subir as informações, e o **Worker** precisa estar no ar na nuvem para processar esse upload e alimentar o Redis.

---

## 6. Configuração para Execução Local de Desenvolvimento (Ambiente Misto)

Se você está rodando os contêineres Docker (PostgreSQL, Redis, RabbitMQ e FTP) e deseja rodar os serviços locais .NET (`dotnet run`) de forma que eles consumam a infraestrutura do Docker e se integrem com o seu **SQL Server local do Windows**, siga estas instruções de configuração.

### 6.1. Como funciona o redirecionamento
Como os contêineres Docker expõem suas portas na máquina Windows, suas aplicações locais conseguem enxergá-los usando **`localhost`**. Ao mesmo tempo, como as aplicações locais rodam nativamente no seu Windows, elas se conectam diretamente com o SQL Server local através do endereço do servidor (instância do SQL Server do Windows).

### 6.2. Arquivos `appsettings` de Desenvolvimento

#### A. API (`Versatus.ForcaVendas.Api`)
Edite o arquivo `appsettings.Development.json` na pasta do projeto da API. Garanta que as chaves de conexão apontem para `localhost` com as credenciais do Docker:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=forca_vendas_dev;Username=postgres;Password=Mudar@!123",
    "Redis": "localhost:6379,abortConnect=false",
    "RabbitMQ": "amqp://fvs:fvs_dev_pass@localhost:5672/"
  },
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "localhost",
      "Port": 21,
      "UseSftp": false,
      "Username": "test",
      "Password": "test",
      "BasePath": "/integration-sync"
    }
  },
  "Messaging": {
    "BrokerUrl": "amqp://fvs:fvs_dev_pass@localhost:5672/"
  }
}
```

#### B. Worker (`Versatus.ForcaVendas.Worker`)
Edite o arquivo `appsettings.Development.json` na pasta do Worker. Certifique-se de que a conexão do PostgreSQL e do Redis apontem para o Docker local:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=forca_vendas_dev;Username=postgres;Password=Mudar@!123",
    "Redis": "localhost:6379,abortConnect=false"
  }
}
```

#### C. Adaptador ERP (`Versatus.ForcaVendas.ErpAdapter`)
No ERP Adapter, edite o arquivo `appsettings.json` (ou `appsettings.Development.json` se criado). Aponte o banco de dados para a instância local do seu SQL Server no Windows e o FTP para o Docker (`localhost`):

```json
{
  "ConnectionStrings": {
    "ErpDatabase": "Server=DESKTOP-PA7RCSD\\SQLEXPRESS2008;Database=versatus;User Id=sa;Password=SUA_SENHA_DO_SQL;TrustServerCertificate=True;"
  },
  "Integration": {
    "Transport": "Ftp",
    "Ftp": {
      "Host": "localhost",
      "Port": 21,
      "UseSftp": false,
      "Username": "test",
      "Password": "test",
      "BasePath": "/integration-sync"
    }
  }
}
```

### 6.3. Como Executar os Serviços
1. Verifique no Docker Desktop se os 4 contêineres estão **ligados** (indicador verde).
2. Verifique se o serviço do seu **SQL Server local** está rodando no Windows.
3. Inicie os três serviços via terminal ou PowerShell:
   * **API**: `dotnet run --project src/backend/Versatus.ForcaVendas.Api`
   * **Worker**: `dotnet run --project src/worker/Versatus.ForcaVendas.Worker`
   * **ERP Adapter**: `dotnet run --project src/erp-adapter/Versatus.ForcaVendas.ErpAdapter`
