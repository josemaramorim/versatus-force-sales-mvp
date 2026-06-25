# Deploy da API no Painel ICP (icontainer) via GitHub

Este documento descreve como publicar o serviço **`Versatus.ForcaVendas.Api`** utilizando a integração nativa do **Painel ICP** com o GitHub. Sempre que um novo commit for enviado para a branch configurada, o ICP buscará automaticamente a nova versão e publicará no servidor.

> [!NOTE]
> Este guia é baseado na documentação oficial da icontainer: https://wiki.icontainer.run/pt-br/deploy-exemplo-github-dotnet

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

# Tenants (lista de UUIDs das empresas)
AUTH__TENANTS__0=00000000-0000-0000-0000-000000000001
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
| `admin@demo1.versatus.com` | `admin` | `Mudar@!123` | `admin` | `00000000-...-0001` |
| `gestor@demo2.versatus.com` | `gestor` | `Mudar@!123` | `gestor` | `00000000-...-0002` |

#### Dados estruturais criados automaticamente

- Status de pedido: `rascunho`, `enviado`, `processado`, `erro`
- 2 tenants demo ativos (IDs `...0001` e `...0002`)
- Todas as tabelas do sistema

> [!CAUTION]
> **Troque a senha `Mudar@!123` imediatamente após o primeiro acesso em produção!** Essa senha é padrão do repositório e conhecida publicamente. Use a API ou acesso direto ao banco para atualizá-la com um BCrypt hash seguro.

> [!NOTE]
> Para verificar se as migrations foram aplicadas com sucesso, acesse o endpoint de saúde:
> ```
> GET https://vps9526.panel.icontainer.net/api/health/ready
> ```
> O status `Healthy` confirma que a API está conectada ao PostgreSQL e Redis.


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
GET https://vps9526.panel.icontainer.net/api/health/live
```
Resposta esperada (HTTP 200):
```json
{ "status": "Alive" }
```

### Endpoint de prontidão (Readiness)
```
GET https://vps6755.panel.icontainer.net/api/health/ready
```
Resposta esperada (HTTP 200):
```json
{
  "status": "Healthy",
  "checks": [
    { "name": "postgres", "status": "Healthy" },
    { "name": "redis", "status": "Healthy" },
    { "name": "rabbitmq", "status": "Healthy" }
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
