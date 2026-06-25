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

1. Acesse: `https://vps6755.panel.icontainer.net`
2. Faça login com suas credenciais

### 2.2. Vincular o GitHub

1. No Painel ICP, vá em **Configurações** (ícone de engrenagem)
2. Clique na aba **Git / GitHub**
3. Insira o **Token de Acesso** gerado na Etapa 1
4. Clique em **Salvar** ou **Conectar**

O ICP irá validar o token e listar os repositórios disponíveis na sua conta.

---

## Etapa 3 — Configurar a Aplicação .NET no Painel ICP

### 3.1. Criar uma nova aplicação

1. No Painel ICP, vá em **Aplicações** → **Nova Aplicação**
2. Escolha o tipo: **.NET** (ou **ASP.NET Core**)

### 3.2. Configurar o repositório

Preencha os campos:

| Campo | Valor |
|---|---|
| **Repositório GitHub** | `josemaramorim/versatus-force-sales-mvp` |
| **Branch** | `develop` (para homologação) ou `main` (para produção estável) |
| **Diretório do projeto** | `src/backend/Versatus.ForcaVendas.Api` |
| **Arquivo .csproj** | `Versatus.ForcaVendas.Api.csproj` |
| **Versão do .NET** | `.NET 8` |

### 3.3. Configurar o comando de inicialização

O ICP compilará e publicará o projeto automaticamente. O comando de inicialização deve ser:

```bash
dotnet Versatus.ForcaVendas.Api.dll
```

> [!NOTE]
> Alguns painéis ICP executam `dotnet publish` automaticamente e depois iniciam o `.dll` gerado. Verifique se o campo de comando de inicialização já está pré-preenchido corretamente.

### 3.4. Configurar a porta

| Campo | Valor |
|---|---|
| **Porta da aplicação** | `5000` |

### 3.5. Configurar as variáveis de ambiente

> [!CAUTION]
> **Nunca deixe senhas ou chaves secretas no arquivo `appsettings.json` commitado no repositório.** Configure-as como variáveis de ambiente no painel ICP.

Adicione as seguintes variáveis de ambiente no painel:

```
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://0.0.0.0:5000

# Banco de dados PostgreSQL
ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;Database=forca_vendas;Username=fvs;Password=SUA_SENHA_FORTE

# Redis
ConnectionStrings__Redis=localhost:6379,abortConnect=false

# RabbitMQ
ConnectionStrings__RabbitMQ=amqp://fvs:SUA_SENHA_FORTE@localhost:5672/
Messaging__BrokerUrl=amqp://fvs:SUA_SENHA_FORTE@localhost:5672/
Messaging__PedidosExchange=pedidos
Messaging__RetornoQueue=pedidos.retorno.erp

# JWT — use uma chave com no mínimo 64 caracteres aleatórios
Auth__Jwt__Issuer=versatus-force-sales
Auth__Jwt__Audience=versatus-force-sales-clients
Auth__Jwt__SecretKey=GERE_UMA_CHAVE_ALEATORIA_LONGA_AQUI
Auth__Jwt__AccessTokenMinutes=60
Auth__Jwt__RefreshTokenDays=7
Auth__Jwt__SessionTimeoutMinutes=20

# Tenants (lista de UUIDs das empresas)
Auth__Tenants__0=00000000-0000-0000-0000-000000000001
```

> [!TIP]
> Para gerar uma chave JWT segura, execute no terminal:
> ```bash
> openssl rand -base64 64
> ```

**Mapeamento de nomes:** O ASP.NET Core converte automaticamente variáveis de ambiente com `__` (dois underscores) para a hierarquia de configuração JSON. Exemplo:
- `ConnectionStrings__DefaultConnection` equivale a `ConnectionStrings.DefaultConnection` no `appsettings.json`
- `Auth__Jwt__SecretKey` equivale a `Auth.Jwt.SecretKey`

### 3.6. Finalizar a criação

Clique em **Criar** ou **Salvar**. O ICP irá:
1. Clonar o repositório
2. Executar `dotnet restore` e `dotnet publish`
3. Iniciar a aplicação

---

## Etapa 4 — Configurar o Webhook do GitHub

O webhook notifica o ICP automaticamente quando um novo commit é enviado ao repositório.

### 4.1. Via Painel ICP (automático — recomendado)

Na maioria dos casos, o ICP configura o webhook automaticamente ao vincular o repositório. Verifique se o webhook foi criado:

1. No GitHub, vá no repositório → **Settings** → **Webhooks**
2. Você deve ver um webhook com a URL do ICP (algo como `https://vps6755.panel.icontainer.net/api/webhook/...`)
3. O status deve estar como **verde** (entregues com sucesso)

### 4.2. Via GitHub (manual — se necessário)

Se o webhook não foi criado automaticamente:

1. No GitHub, vá em **Settings** → **Webhooks** → **Add webhook**
2. Preencha:
   - **Payload URL:** cole a URL fornecida pelo Painel ICP
   - **Content type:** `application/json`
   - **Secret:** use o segredo fornecido pelo painel (se solicitado)
   - **Which events?** → selecione **Just the push event**
3. Clique em **Add webhook**

---

## Etapa 5 — Enviar Alterações e Acompanhar o Deploy Automático

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
GET https://vps6755.panel.icontainer.net/api/health/live
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
