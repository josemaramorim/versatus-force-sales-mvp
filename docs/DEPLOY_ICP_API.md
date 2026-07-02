# Deploy da API no Painel ICP (icontainer) via GitHub

Este documento descreve como publicar o serviço **`Versatus.ForcaVendas.Api`** utilizando a integração nativa do **Painel ICP** com o GitHub. Sempre que um novo commit for enviado para a branch configurada, o ICP buscará automaticamente a nova versão e publicará no servidor.

> [!NOTE]
> Este guia é baseado na documentação oficial da icontainer: https://wiki.icontainer.run/pt-br/deploy-exemplo-github-dotnet

---

## 🗺️ Índice do Tutorial de Deploy

Selecione a etapa que deseja consultar ou siga o passo a passo na ordem sugerida:

### 🏁 Preparação e Acessos
* [1. Visão Geral](#visão-geral) — Estrutura e portas padrão do projeto.
* [2. Pré-requisitos](#pré-requisitos) — Acessos necessários e validação local.
* [3. Token do GitHub](#etapa-1--criar-um-token-de-acesso-no-github) — Como autorizar o painel ICP a acessar seu código.
* [4. Conectar GitHub](#etapa-2--configurar-o-github-no-painel-icp) — Vinculação do token no painel.

### 🗄️ Infraestrutura (Banco e Arquivos)
* [5. PostgreSQL](#31-criar-o-banco-postgresql) — Criação do banco e string de conexão.
* [6. Redis Cache](#32-criar-a-instância-redis) — Configuração do cache do catálogo.
* [7. SFTPGo (FTP/SFTP)](#33-instalar-o-sftpgo-servidor-ftpsftp) — Servidor de arquivos para integração ERP.

### 🚀 Deploy Nível 1 — Fluxo Contínuo (Git + Auto-compilação)
* [8. Criar App .NET no ICP](#etapa-4--configurar-a-aplicação-net-no-painel-icp) — Passo a passo do assistente.
* [9. Variáveis de Ambiente](#45-configurar-as-variáveis-de-ambiente) — Chaves e senhas obrigatórias em CAIXA ALTA.
* [10. Banco & Seed Automático](#47-banco-de-dados--migrations-e-usuários-iniciais) — Como as tabelas e usuários iniciais são gerados no startup.
* [11. Configuração de Domínio e SSL](#48-configuração-de-domínio-próprio-e-ssl-lets-encrypt) — Como vincular o domínio real e configurar o SSL.
* [12. Webhooks de Auto-deploy](#etapa-5--configurar-o-webhook-do-github) — Atualização automática com `git push`.

### 🛡️ Diagnóstico e Monitoramento
* [13. Testes de Saúde (Health Checks)](#verificação-final) — Validar se a API e os bancos estão conectados.
* [14. Rotas da API](#estrutura-das-rotas-da-api) — Tabela de endpoints disponíveis.
* [15. Resolução de Erros Comuns](#solução-de-problemas) — O que fazer se algo der errado.
  * [15.1. Erro de Projetos Não Encontrados no Build](#erro-de-compilação-no-container-projetos-não-encontrados) — Como compilar projetos multi-camadas no container do painel.

### 🔌 Deploy Nível 2 — Método ZIP Local (Solução para conexões instáveis)
* [16. Por que usar o ZIP?](#método-alternativo--publicação-via-arquivo-zip-recomendado-para-conexões-instáveis) — Vantagens do deploy local.
* [17. Compilar e Zipar Localmente](#a1-compilar-e-gerar-o-zip-localmente) — Comandos rápidos em PowerShell.
* [18. Configurar a Aplicação ZIP](#a2-configurar-a-aplicação-no-painel-icp) — Criação da aplicação usando o ZIP.
* [19. Atualizar arquivos via Gerenciador ou SFTP](#a3-como-atualizar-ou-enviar-arquivos-grandes-evitando-limites-do-navegador) — Como enviar arquivos grandes sem restrição de tamanho.

---

## Visão Geral

O projeto da API está localizado em:
```
src/backend/Versatus.ForcaVendas.Api/
```

- **Framework:** .NET 8 (ASP.NET Core)
- **Arquivo de projeto:** `Versatus.ForcaVendas.Api.csproj`
- **Porta padrão:** `5000` (produção) / `5225` (desenvolvimento local)
- **Endpoint de saúde:** `/health/live` e `/health/ready`
- **Métricas Prometheus:** `/metrics`

---

## Pré-requisitos

Antes de começar, confirme que você possui:

- [ ] Conta no GitHub com acesso de administrador ao repositório `josemaramorim/versatus-force-sales-mvp`
- [ ] Acesso ao **Painel ICP** da icontainer (VPS `vps6755.panel.icontainer.net`)
- [ ] Painel ICP atualizado para a versão mais recente
- [ ] A aplicação compilando corretamente com os comandos abaixo (valide localmente antes):

```bash
cd src/backend
dotnet restore
dotnet build Versatus.ForcaVendas.Api
dotnet run --project Versatus.ForcaVendas.Api
```

---

## Etapa 1 — Criar um Token de Acesso no GitHub

O token permite que o ICP acesse o repositório e configure o webhook automaticamente.

### 1.1. Abrir as configurações da conta GitHub

1. Clique na sua foto no canto superior direito do GitHub
2. Vá em **Settings**

### 1.2. Gerar o token

1. No menu lateral esquerdo, clique em **Developer settings** (no final da lista)
2. Clique em **Personal access tokens** → **Tokens (classic)**
3. Clique em **Generate new token** → **Generate new token (classic)**
4. Preencha os campos:
   - **Note:** `icontainer-ICP-versatus-api`
   - **Expiration:** escolha uma data adequada (ex: 90 dias ou sem expiração)
5. Marque os escopos necessários:
   - ✅ `repo` (acesso completo ao repositório — necessário para o webhook)
6. Clique em **Generate token**
7. **Copie o token gerado** — ele não será exibido novamente!

---

## Etapa 2 — Configurar o GitHub no Painel ICP

### 2.1. Acessar o painel

1. Acesse: `https://vps9526.panel.icontainer.net`
2. Faça login com suas credenciais

### 2.2. Vincular o GitHub

1. No Painel ICP, vá em **Configurações** (ícone de engrenagem)
2. Clique na aba **Git / GitHub**
3. Insira o **Token de Acesso** gerado na Etapa 1
4. Clique em **Salvar** ou **Conectar**

O ICP irá validar o token e listar os repositórios disponíveis na sua conta.

---

## Etapa 3 — Criar o Banco de Dados e o Redis no Painel ICP

> [!IMPORTANT]
> O Painel ICP da icontainer oferece **PostgreSQL e Redis como serviços gerenciados**. Você não precisa instalar nem usar Docker para isso. Os serviços rodam no mesmo servidor e são acessíveis via endereço interno (não é `localhost` comum — o ICP usa endereços específicos por container).

### 3.1. Criar o banco PostgreSQL

1. No Painel ICP, vá em **Banco de Dados** → **PostgreSQL**
2. Clique em **Criar** (ou no container PostgreSQL já criado)
3. Clique em **Configuração de parâmetro** ou **Informações de conexão**
4. **Anote os valores exibidos:**

| Campo mostrado no ICP | Exemplo real | Onde usar na connection string |
|---|---|---|
| **Container** | `postgresql-forca-venda` | `Host=` |
| **Usuário** | `versatus` | `Username=` |
| **Senha** | `(definida por você)` | `Password=` |
| **Porta** | `5432` | `Port=` |

5. Crie o banco de dados `forca_vendas` dentro do container (Painel ICP → PostgreSQL → Databases → Criar)

> [!IMPORTANT]
> O campo **"Container"** do painel ICP é o **hostname** a ser usado nas conexões internas. Não use `localhost` nem `127.0.0.1`. Use exatamente o nome do container.

**Exemplo de connection string com os dados reais do painel:**
```
Host=postgresql-forca-venda;Port=5432;Database=forca_vendas;Username=versatus;Password=SUA_SENHA
```

Portanto a variável de ambiente na API ficará:
```
ConnectionStrings__DefaultConnection=Host=postgresql-forca-venda;Port=5432;Database=forca_vendas;Username=versatus;Password=SUA_SENHA
```

### 3.2. Criar a instância Redis

1. No Painel ICP, vá em **Banco de Dados** → **Redis**
2. Clique em **Configuração de parâmetro** ou **Informações de conexão**
3. **Anote os valores exibidos:**

| Campo mostrado no ICP | Exemplo real | Onde usar na connection string |
|---|---|---|
| **Container** | `redis-forca-venda` | hostname |
| **Senha** | `(definida por você)` | `password=` |
| **Porta** | `6379` | porta |

> [!IMPORTANT]
> O campo **"Container"** do painel ICP é o **hostname** a ser usado. Não use `localhost` nem `127.0.0.1`.

**Exemplo de connection string com os dados reais do painel:**
```
redis-forca-venda:6379,password=SUA_SENHA,abortConnect=false
```

Portanto a variável de ambiente na API ficará:
```
ConnectionStrings__Redis=redis-forca-venda:6379,password=SUA_SENHA,abortConnect=false
```

### 3.3. Instalar o SFTPGo (servidor FTP/SFTP)

O projeto usa FTP/SFTP para trocar arquivos com o ERP Adapter do cliente. O ICP tem o **SFTPGo** disponível na App Store — ele suporta SFTP (recomendado, mais seguro) e FTP clássico.

> [!TIP]
> **Use SFTP ao invés de FTP simples.** O código já suporta SFTP nativamente e é mais seguro pois criptografa a transferência via SSH. O SFTPGo do ICP suporta ambos os protocolos.

1. No Painel ICP, vá em **App Store**
2. Pesquise por **SFTPGo** e clique em **Instalar**
3. Após a instalação, acesse as **Informações de conexão** do SFTPGo
4. **Valores do seu painel ICP:**

| Campo mostrado no ICP | Valor real | Variável de ambiente |
|---|---|---|
| **Container** | `ftp-forca-venda` | `INTEGRATION__FTP__HOST=ftp-forca-venda` |
| **Porta SFTP** | `2022` ⚠️ | `INTEGRATION__FTP__PORT=2022` |
| **Usuário Admin** | `versatus` | `INTEGRATION__FTP__USERNAME=versatus` |
| **Senha** | `(a que você definiu)` | `INTEGRATION__FTP__PASSWORD=SUA_SENHA` |

> [!CAUTION]
> A **Porta SFTP é 2022** — não a porta padrão 22! Configure exatamente `2022` na variável de ambiente, caso contrário a API não conseguirá se conectar ao servidor de arquivos.

5. No SFTPGo, crie o diretório `/integration-sync` para o usuário `versatus` (pode ser feito via interface web na **Porta Web 8282**)

> [!NOTE]
> Para acessar a interface de administração do SFTPGo, use:
> ```
> http://ftp-forca-venda:8282
> ```
> Ou pelo IP externo: `http://23.80.91.77:8282` (acesso de fora da VPS)

> [!TIP]
> Para obter instruções detalhadas de como configurar, compilar e rodar o **ERP Adapter** na máquina ou servidor local do cliente integrado a este ambiente ICP, consulte o guia completo [DEPLOY_ICP_ERP_ADAPTER.md](file:///c:/Pasta%20de%20Trabalho/Projetos/Analises/Versatus.Net/versatus-force-sales-mvp/docs/DEPLOY_ICP_ERP_ADAPTER.md).

---

## Etapa 4 — Configurar a Aplicação .NET no Painel ICP

### 4.1. Criar uma nova aplicação

1. No Painel ICP, vá em **Aplicações** → **Nova Aplicação**
2. Escolha o tipo: **.NET** (ou **ASP.NET Core**)

### 4.2. Configurar o repositório

Preencha os campos:

| Campo | Valor |
|---|---|
| **Repositório GitHub** | `josemaramorim/versatus-force-sales-mvp` |
| **Branch** | `develop` (para homologação) ou `main` (para produção estável) |
| **Diretório do projeto** | `src/backend/Versatus.ForcaVendas.Api` |
| **Arquivo .csproj** | `Versatus.ForcaVendas.Api.csproj` |
| **Versão do .NET** | `.NET 8` |

### 4.3. Configurar o comando de inicialização

O ICP compilará e publicará o projeto automaticamente. O comando de inicialização deve ser:

```bash
dotnet Versatus.ForcaVendas.Api.dll
```

> [!NOTE]
> Alguns painéis ICP executam `dotnet publish` automaticamente e depois iniciam o `.dll` gerado. Verifique se o campo de comando de inicialização já está pré-preenchido corretamente.

### 4.4. Configurar a porta

| Campo | Valor |
|---|---|
| **Porta da aplicação** | `5000` |

### 4.5. Configurar as variáveis de ambiente

> [!CAUTION]
> **Nunca deixe senhas ou chaves secretas no arquivo `appsettings.json` commitado no repositório.** Configure-as como variáveis de ambiente no painel ICP.

Adicione as seguintes variáveis de ambiente no painel:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000

# Transporte de integração — OBRIGATÓRIO: define que o sistema usa FTP/SFTP (não RabbitMQ)
INTEGRATION__TRANSPORT=Ftp

# SFTPGo — configuração de conexão SFTP (dados reais do Painel ICP)
# Container = hostname interno; Porta SFTP = 2022 (não é a porta padrão 22!)
INTEGRATION__FTP__HOST=ftp-forca-venda
INTEGRATION__FTP__PORT=2022
INTEGRATION__FTP__USESFTP=true
INTEGRATION__FTP__USERNAME=versatus
INTEGRATION__FTP__PASSWORD=SUA_SENHA_SFTPGO
INTEGRATION__FTP__BASEPATH=/integration-sync

# Banco de dados PostgreSQL
# ⚠️ O "Container" mostrado no Painel ICP é o hostname (não use localhost)
CONNECTIONSTRINGS__DEFAULTCONNECTION=Host=postgresql-forca-venda;Port=5432;Database=forca_vendas;Username=versatus;Password=SUA_SENHA_POSTGRES

# Redis
# ⚠️ O "Container" mostrado no Painel ICP é o hostname (não use localhost)
CONNECTIONSTRINGS__REDIS=redis-forca-venda:6379,password=SUA_SENHA_REDIS,abortConnect=false

# JWT — use uma chave com no mínimo 64 caracteres aleatórios
AUTH__JWT__ISSUER=versatus-force-sales
AUTH__JWT__AUDIENCE=versatus-force-sales-clients
AUTH__JWT__SECRETKEY=GERE_UMA_CHAVE_ALEATORIA_LONGA_AQUI
AUTH__JWT__ACCESSTOKENMINUTES=60
AUTH__JWT__REFRESHTOKENDAYS=7
AUTH__JWT__SESSIONTIMEOUTMINUTES=20

# CORS — Origens permitidas do frontend Next.js (separadas por vírgula)
# Permite o localhost (HTTP) e o IP/domínio da sua VPS em HTTPS onde o frontend está rodando
CORS__ALLOWEDORIGINS=http://localhost:3000,http://localhost:3001,https://vps9526.panel.icontainer.net:3000,https://23.80.91.77:3000
```

> [!IMPORTANT]
> O valor de `INTEGRATION__TRANSPORT` deve ser `Ftp` (com F maiúsculo). O código agora aceita qualquer capitalização (`FTP`, `ftp`, `Ftp`), mas o padrão documentado é `Ftp`.
>
> O **nome da variável** pode ser em qualquer capitalização (`INTEGRATION__TRANSPORT` ou `Integration__Transport`) — o ASP.NET Core trata as chaves como case-insensitive.

> [!TIP]
> Para gerar uma chave JWT segura, execute no terminal:
> ```bash
> openssl rand -base64 64
> ```

**Mapeamento de nomes:** O ASP.NET Core converte automaticamente variáveis de ambiente com `__` (dois underscores) para a hierarquia de configuração JSON. Exemplo:
- `ConnectionStrings__DefaultConnection` equivale a `ConnectionStrings.DefaultConnection` no `appsettings.json`
- `Auth__Jwt__SecretKey` equivale a `Auth.Jwt.SecretKey`
- `Integration__Transport` equivale a `Integration.Transport`
- `Cors__AllowedOrigins` equivale a `Cors.AllowedOrigins` no `appsettings.json`

### ⚠️ RabbitMQ não é necessário

O projeto utiliza **FTP como transporte de integração** entre a API e o ERP Adapter do cliente. O transporte RabbitMQ existe no código como estrutura para uma fase futura, mas **todos os seus métodos lançam `NotImplementedException`** — portanto, usar RabbitMQ quebraria a aplicação.

| Serviço | Necessário? | Motivo |
|---|---|---|
| **PostgreSQL** | ✅ Sim | Armazena pedidos, usuários e tenants |
| **Redis** | ✅ Sim | Cache do catálogo (clientes, produtos, preços) |
| **RabbitMQ** | ❌ **Não** | Não implementado — o projeto usa FTP como transport |
| **FTP** | ✅ Sim | Troca de arquivos com o ERP Adapter do cliente |

A variável `Integration__Transport=Ftp` (já incluída na lista acima) garante que o sistema use o transporte correto.

### 4.6. Finalizar a criação

Clique em **Criar** ou **Salvar**. O ICP irá:
1. Clonar o repositório
2. Executar `dotnet restore` e `dotnet publish`
3. Iniciar a aplicação

### 4.7. Banco de dados — migrations e usuários iniciais

Ao iniciar pela primeira vez, a API **aplica automaticamente todas as migrations do Entity Framework Core** no PostgreSQL. O banco será criado e populado com os dados iniciais (seed) sem nenhuma intervenção manual.

O seed cria automaticamente:

#### Usuários padrão

| Email | Username | Senha | Role | Tenant |
|---|---|---|---|---|
| `admin@demo1.versatus.com` | `admin` | `123456` | `admin` | `00000000-...-0001` |
| `gestor@demo2.versatus.com` | `gestor` | `123456` | `gestor` | `00000000-...-0002` |

#### Dados estruturais criados automaticamente

- Status de pedido: `rascunho`, `enviado`, `processado`, `erro`
- 2 tenants demo ativos (IDs `...0001` e `...0002`)
- Todas as tabelas do sistema

> [!CAUTION]
> **Troque a senha `Mudar@!123` imediatamente após o primeiro acesso em produção!** Essa senha é padrão do repositório e conhecida publicamente. Use a API ou acesso direto ao banco para atualizá-la com um BCrypt hash seguro.

> [!NOTE]
> Para verificar se as migrations foram aplicadas com sucesso, acesse o endpoint de saúde:
> ```
> GET https://force-sales-api.vps9526.panel.icontainer.net/health/ready
> ```
> O status `Healthy` confirma que a API está conectada ao PostgreSQL e Redis.

### 4.8. Configuração de Domínio Próprio e SSL (Let's Encrypt)

Para que a API responda sob um domínio seguro de produção (ex: `https://api.versatusapp.com.br`), siga os passos de configuração no Painel ICP:

#### Passo 1 — Apontamento de DNS
No painel do seu provedor de domínio (Registro.br, Cloudflare, GoDaddy, etc.), crie o seguinte registro DNS apontando para o IP público da sua VPS:
* **Tipo:** `A`
* **Nome/Host:** `api` (o subdomínio desejado para o backend)
* **Destino/Aponta para:** IP público da sua VPS (ex: `23.80.91.77`)

#### Passo 2 — Vincular o Domínio no Painel ICP
1. Acesse o **Painel ICP** (`https://vps9526.panel.icontainer.net`).
2. Vá em **Aplicações** e selecione a aplicação do seu Backend (API).
3. Acesse a aba **Domínios** ou **Configuração Web**.
4. Insira o domínio completo da API: `api.versatusapp.com.br` e clique em salvar. O painel ICP criará automaticamente a configuração do proxy reverso no Nginx interno do servidor.

#### Passo 3 — Ativar o SSL (HTTPS Grátis)
1. Ainda na configuração de domínios no painel ICP, localize a opção de **SSL** ou **Let's Encrypt**.
2. Clique em **Ativar SSL** ou **Gerar Certificado**. O painel emitirá e instalará o certificado de segurança de forma automatizada.
3. A partir deste momento, a API estará acessível de forma segura em:
   ```text
   https://api.versatusapp.com.br
   ```

#### ⚠️ Passo 4 — Ajuste Crítico do CORS (Segurança de Origem)
Após migrar a API e o Frontend para domínios próprios seguros, você **deve** atualizar a variável de ambiente de CORS nas configurações da API no painel ICP para permitir que o navegador do cliente envie requisições ao backend:

1. Acesse as configurações da aplicação **API** no painel ICP.
2. Na aba **Variáveis de Ambiente**, atualize a variável `CORS__ALLOWEDORIGINS` incluindo a URL exata do seu frontend em HTTPS:
   ```env
   CORS__ALLOWEDORIGINS=http://localhost:3000,http://localhost:3001,https://vendas.versatusapp.com.br
   ```
3. Salve a alteração e **reinicie** a aplicação da API para aplicar a nova política de origens permitidas.


---

## Etapa 5 — Configurar o Webhook do GitHub

O webhook notifica o ICP automaticamente quando um novo commit é enviado ao repositório.

### 5.1. Via Painel ICP (automático — recomendado)

Na maioria dos casos, o ICP configura o webhook automaticamente ao vincular o repositório. Verifique se o webhook foi criado:

1. No GitHub, vá no repositório → **Settings** → **Webhooks**
2. Você deve ver um webhook com a URL do ICP (algo como `https://vps9526.panel.icontainer.net/api/webhook/...`)
3. O status deve estar como **verde** (entregues com sucesso)

### 5.2. Via GitHub (manual — se necessário)

Se o webhook não foi criado automaticamente:

1. No GitHub, vá em **Settings** → **Webhooks** → **Add webhook**
2. Preencha:
   - **Payload URL:** cole a URL fornecida pelo Painel ICP
   - **Content type:** `application/json`
   - **Secret:** use o segredo fornecido pelo painel (se solicitado)
   - **Which events?** → selecione **Just the push event**
3. Clique em **Add webhook**

---

## Etapa 6 — Enviar Alterações e Acompanhar o Deploy Automático

### 5.1. Enviar um commit para o GitHub

Com qualquer alteração no código:

```bash
git add .
git commit -m "feat: descrição da alteração"
git push origin develop
```

### 5.2. Acompanhar o deploy no Painel ICP

1. Acesse o Painel ICP
2. Vá em **Aplicações** → **Versatus Force Sales API**
3. Clique na aba **Logs** ou **Deploy**
4. Acompanhe a saída da compilação e inicialização em tempo real

O log de um deploy bem-sucedido deve mostrar algo como:

```
Cloning repository...
Running: dotnet restore
Running: dotnet publish --configuration Release
Starting application...
info: Microsoft.Hosting.Lifetime[14] - Now listening on: http://0.0.0.0:5000
info: Versatus.ForcaVendas - Application started
```

---

## Verificação Final

Após o deploy, verifique se a API está respondendo:

### Endpoint de saúde (Liveness)
```
GET https://force-sales-api.vps9526.panel.icontainer.net/health/live
```
Resposta esperada (HTTP 200):
```json
{ "status": "Alive" }
```

### Endpoint de prontidão (Readiness)
```
GET https://force-sales-api.vps9526.panel.icontainer.net/health/ready
```
Resposta esperada (HTTP 200):
```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "redis",
      "status": "Healthy",
      "description": "Pong: 1.9273ms",
      "duration": 13.7074
    }
  ]
}
```

> [!WARNING]
> Se o `/health/ready` retornar HTTP 503, algum serviço de infraestrutura (PostgreSQL, Redis ou RabbitMQ) não está acessível. Verifique se o Docker Compose está em execução no servidor:
> ```bash
> docker compose ps
> ```

---

## Estrutura das Rotas da API

| Prefixo | Descrição |
|---|---|
| `GET /health/live` | Verificação de liveness (processo vivo) |
| `GET /health/ready` | Verificação de readiness (serviços externos acessíveis) |
| `GET /metrics` | Métricas Prometheus |
| `/api/auth/**` | Autenticação e sessão (login, logout, refresh) |
| `/api/catalogo/**` | Catálogo de produtos, clientes e condições de pagamento |
| `/api/pedidos/**` | Criação e consulta de pedidos |

---

## Solução de Problemas

### A aplicação não inicia após o deploy

1. Verifique os logs de deploy no Painel ICP
2. Confirme que as variáveis de ambiente estão configuradas corretamente (especialmente as connection strings)
3. Verifique se o `ASPNETCORE_URLS` está configurado como `http://0.0.0.0:5000`

### Erro de conexão com o banco de dados

1. Confirme que o Docker Compose está rodando: `docker compose ps`
2. Teste a conexão manualmente: `psql -h localhost -U fvs -d forca_vendas`
3. Verifique se a senha no ambiente corresponde à senha configurada no Docker Compose

### O webhook não está disparando o deploy automático

1. Acesse GitHub → repositório → **Settings** → **Webhooks**
2. Clique no webhook → **Recent Deliveries** → verifique os erros
3. Certifique-se de que o payload URL está correto e acessível a partir da internet

### Erro de Compilação no Container: `MSB1003: Specify a project or solution file` (Projetos Não Encontrados)
Se ao tentar fazer o deploy usando a opção **"Compilar antes de publicar"** (Git Auto-compilação) o container de build falhar pulando os projetos irmãos com erros de namespaces ausentes ou apresentar a seguinte mensagem de erro:
`MSBUILD : error MSB1003: Specify a project or solution file. The current working directory does not contain a project or solution file.`

*   **Causa:** A API depende dos projetos irmãos localizados sob a pasta `src/backend/` (como `Application` e `Infrastructure`). Se a **Pasta do projeto** no painel estiver apontando diretamente para `src/backend/Versatus.ForcaVendas.Api`, o container de build copiará apenas essa subpasta, impedindo o compilador de acessar os níveis superiores (`..\`) do repositório. Caso você altere a **Pasta do projeto** para `/` (raiz do repositório) para resolver isso, a compilação rodará na raiz, falhando com o erro `MSB1003` se você não especificar qual projeto deve ser compilado.
*   **Solução (Ajustar caminhos de Build):**
    1. Acesse as configurações da aplicação da API no painel ICP.
    2. No campo **Pasta do projeto** (no topo), altere para a **raiz do repositório** (deixe em branco ou coloque `/` ou `.`). Isso fará o painel copiar o repositório completo para o container de build.
    3. No campo **Comando de build** (logo abaixo do switch de compilação), altere para especificar o caminho completo do `.csproj` a partir da raiz, garantindo que o MSBuild saiba o que compilar:
       ```bash
       dotnet publish src/backend/Versatus.ForcaVendas.Api/Versatus.ForcaVendas.Api.csproj -c Release -o /app
       ```
    4. Salve e force uma nova compilação no painel.

---

## Método Alternativo — Publicação via Arquivo ZIP (Recomendado para conexões instáveis)

Caso a VPS do ICP apresente instabilidade de rede ao baixar as imagens do SDK do .NET do Microsoft Container Registry (gerando erros de `connection reset by peer`), você pode compilar a aplicação localmente e enviar o pacote pronto.

Este método é **100% garantido** pois elimina a necessidade de compilação no servidor.

### A.1. Compilar e Gerar o ZIP Localmente

Execute os seguintes passos no terminal (PowerShell) do seu computador local:

1. Abra o PowerShell e navegue até a pasta do backend:
   ```powershell
   cd "c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\backend"
   ```

2. Execute o comando para limpar publicações anteriores, compilar e gerar a pasta de publicação:
   ```powershell
   Remove-Item -Path ./publish -Recurse -ErrorAction SilentlyContinue
   dotnet publish Versatus.ForcaVendas.Api/Versatus.ForcaVendas.Api.csproj -c Release -o ./publish
   ```

3. Compacte o conteúdo gerado em um arquivo ZIP chamado `publish.zip`:
   ```powershell
   Compress-Archive -Path ./publish/* -DestinationPath ./publish.zip -Force
   ```

O arquivo `publish.zip` (com cerca de 7.4 MB) será criado na pasta `src/backend/`.

### A.2. Configurar a Aplicação no Painel ICP

1. No Painel ICP, vá em **Aplicações** → **Nova Aplicação** (ou edite a existente).
2. Defina a versão do .NET como **8.0**.
3. Em **Arquivos do Projeto**, selecione a aba **Enviar arquivos** (primeira aba à esquerda).
4. Arraste ou selecione o arquivo `publish.zip` gerado localmente.
5. Preencha as seguintes configurações básicas:

| Campo no ICP | Valor |
|---|---|
| **Script de Execução** | `dotnet Versatus.ForcaVendas.Api.dll` |
| **Porta da aplicação** | `5000` |
| **Porta Externa** | `5000` |

6. Adicione as mesmas variáveis de ambiente em **CAIXA ALTA** especificadas na [Etapa 4.5](#45-configurar-as-variáveis-de-ambiente).
7. Clique em **Confirmar** ou **Salvar**.

O painel descompactará o ZIP e iniciará a API instantaneamente, sem precisar compilar ou baixar imagens pesadas na VPS.

### A.3. Como Atualizar ou Enviar Arquivos Grandes (Evitando limites do navegador)

Se o navegador bloquear o upload do arquivo `publish.zip` (geralmente devido a limites de tamanho de upload padrão no formulário do painel), utilize uma das opções abaixo para atualizar os arquivos diretamente no servidor:

#### Opção A: Gerenciador de Arquivos do Painel ICP

O ICP possui um gerenciador de arquivos integrado com limites muito maiores e ferramenta de descompactação interna:

1. Acesse o Painel ICP e vá na listagem de aplicações.
2. Na linha da aplicação `force-sales-api`, clique no **ícone de pasta azul** na coluna **Arquivos**.
3. No topo do Gerenciador de Arquivos, clique em **Enviar** (ou **Upload**) e selecione o arquivo `publish.zip`.
4. Após concluir o upload, clique com o botão direito sobre o arquivo `publish.zip` e escolha a opção **Extrair** (ou **Descompactar**).
5. Exclua o arquivo `publish.zip` para economizar espaço em disco.
6. Volte à listagem de aplicações e clique em **Reiniciar** a aplicação para aplicar as mudanças.

#### Opção B: Envio direto via SFTP (Recomendado e Sem Limites)

Esta é a forma mais ágil e profissional. Ela dispensa o uso do navegador e permite o envio direto das pastas e arquivos descompactados:

1. Abra um cliente SFTP (como **FileZilla** ou **WinSCP**) no seu computador.
2. Conecte-se ao servidor utilizando os dados de acesso SSH da VPS:
   * **Protocolo:** `SFTP` (SSH File Transfer Protocol)
   * **Host:** `vps9526.panel.icontainer.net` (ou o IP do servidor)
   * **Porta:** `22` (ou a sua porta SSH configurada)
   * **Usuário:** `root`
   * **Senha:** `(sua senha do usuário root do servidor)`
3. No painel direito (servidor remoto), acesse a pasta da aplicação:
   ```text
   /home/apps/force-sales-api/
   ```
4. No painel esquerdo (computador local), acesse a pasta onde a compilação foi gerada:
   ```text
   c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\backend\publish\
   ```
5. Selecione todos os arquivos e pastas dentro de `publish` e arraste-os para a pasta `/home/apps/force-sales-api/` no painel do servidor, autorizando a substituição dos arquivos existentes.
6. Após a transferência de todos os arquivos, vá ao Painel ICP e clique em **Reiniciar** a aplicação.
