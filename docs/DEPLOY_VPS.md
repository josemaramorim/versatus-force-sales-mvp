# Guia de Deploy em VPS (Produção) — Versatus Force Sales

Este documento descreve **passo a passo** como instalar e rodar toda a aplicação **Versatus Force Sales** num servidor VPS (Linux Ubuntu/Debian) do zero, usando Docker para a infraestrutura e rodando os serviços .NET e o frontend Next.js diretamente no servidor.

---

## 1. Visão Geral da Arquitetura em Produção

```
Internet
    │
    ▼
[ Nginx ] :80 / :443  (proxy reverso + SSL)
    │
    ├──▶ /          → Next.js Frontend (porta 3000)
    ├──▶ /api/      → API .NET Gateway (porta 5000)
    │
[ Docker Compose ] — infraestrutura isolada
    ├── fvs-postgres  :5432
    ├── fvs-redis     :6379
    ├── fvs-rabbitmq  :5672 / :15672
    └── fvs-ftp       :21
    │
[ Serviços .NET rodando como systemd ]
    ├── Versatus.ForcaVendas.Api      (porta 5000)
    ├── Versatus.ForcaVendas.Worker   (background)
    └── Versatus.ForcaVendas.ErpAdapter (background)
```

---

## 2. Pré-requisitos da VPS

- **Sistema operacional:** Ubuntu 24.04 LTS (recomendado) ou Debian 12
- **RAM mínima:** 2 GB (recomendado 4 GB para conforto)
- **Disco mínimo:** 20 GB livres
- **Acesso:** SSH com usuário `root` ou usuário com `sudo`
- **Domínio** (opcional, mas recomendado para SSL): ex. `vendas.suaempresa.com.br`

---

## 3. Passo 1 — Atualizar o Servidor

Acesse o servidor via SSH e execute:

```bash
sudo apt update && sudo apt upgrade -y
sudo apt install -y curl wget git unzip
```

---

## 4. Passo 2 — Instalar o Docker

### 4.1. Instalar o Docker Engine

```bash
# Instalar dependências
sudo apt install -y ca-certificates curl gnupg

# Adicionar a chave GPG oficial do Docker
sudo install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg
sudo chmod a+r /etc/apt/keyrings/docker.gpg

# Adicionar o repositório do Docker
echo \
  "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu \
  noble stable" | \
  sudo tee /etc/apt/sources.list.d/docker.list > /dev/null

# Instalar o Docker
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

### 4.2. Habilitar o Docker para iniciar automaticamente

```bash
sudo systemctl enable docker
sudo systemctl start docker
```

### 4.3. Verificar a instalação

```bash
docker --version
docker compose version
```

> [!NOTE]
> Em servidores Linux modernos, o comando é `docker compose` (sem hífen), que é o plugin V2. O `docker-compose` (com hífen) é a versão legada.

---

## 5. Passo 3 — Instalar o .NET 8 SDK

Os serviços de backend foram desenvolvidos em .NET 8.

```bash
# Instalar via script da Microsoft
wget https://dot.net/v1/dotnet-install.sh -O dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0

# Adicionar ao PATH permanentemente
echo 'export DOTNET_ROOT=$HOME/.dotnet' >> ~/.bashrc
echo 'export PATH=$PATH:$HOME/.dotnet:$HOME/.dotnet/tools' >> ~/.bashrc
source ~/.bashrc

# Verificar instalação
dotnet --version
```

---

## 6. Passo 4 — Instalar o Node.js 20 (para o Frontend)

```bash
# Instalar via nvm (Node Version Manager)
curl -o- https://raw.githubusercontent.com/nvm-sh/nvm/v0.39.7/install.sh | bash
source ~/.bashrc

# Instalar o Node.js 20
nvm install 20
nvm use 20
nvm alias default 20

# Verificar
node --version
npm --version
```

---

## 7. Passo 5 — Clonar o Repositório

```bash
# Criar pasta de trabalho
mkdir -p /opt/versatus
cd /opt/versatus

# Clonar o projeto (substitua pela URL correta)
git clone https://github.com/josemaramorim/versatus-force-sales-mvp.git
cd versatus-force-sales-mvp
```

---

## 8. Passo 6 — Configurar as Variáveis de Ambiente

### 8.1. Criar o arquivo `.env` a partir do exemplo

```bash
cp .env.example .env
nano .env
```

### 8.2. Editar as variáveis com os valores de produção

Ajuste **obrigatoriamente** os seguintes valores no `.env`:

```env
# PostgreSQL
POSTGRES_USER=fvs
POSTGRES_PASSWORD=SUA_SENHA_FORTE_AQUI          # ⚠️ Troque!
POSTGRES_DB=forca_vendas
DATABASE_URL=Host=localhost;Port=5432;Database=forca_vendas;Username=fvs;Password=SUA_SENHA_FORTE_AQUI

# Redis
REDIS_URL=localhost:6379

# RabbitMQ
RABBITMQ_USER=fvs
RABBITMQ_PASS=OUTRA_SENHA_FORTE_AQUI             # ⚠️ Troque!
RABBITMQ_URL=amqp://fvs:OUTRA_SENHA_FORTE_AQUI@localhost:5672

# JWT — mínimo 64 caracteres aleatórios
JWT_SECRET=GERE_UMA_CHAVE_ALEATORIA_DE_64_CHARS  # ⚠️ Troque!
JWT_ISSUER=versatus-forca-vendas
JWT_AUDIENCE=versatus-forca-vendas-client
JWT_EXPIRY_MINUTES=60
JWT_REFRESH_EXPIRY_DAYS=7

# API
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://localhost:5000

# Frontend
NEXT_PUBLIC_API_URL=https://SEU_DOMINIO.com/api  # ⚠️ Ajuste para seu domínio
```

> [!CAUTION]
> Nunca commite o arquivo `.env` com senhas de produção no repositório. O `.gitignore` já o exclui, mas confirme antes de dar push.

**Para gerar uma chave JWT segura:**
```bash
openssl rand -base64 64
```

### 8.3. Criar o arquivo `.env` do frontend

```bash
cat > src/frontend/app/.env.local << EOF
NEXT_PUBLIC_API_URL=https://SEU_DOMINIO.com/api
EOF
```

---

## 9. Passo 7 — Subir a Infraestrutura com Docker

A infraestrutura (banco de dados, cache, fila de mensagens e FTP) roda em Docker.

```bash
cd /opt/versatus/versatus-force-sales-mvp

# Iniciar toda a infraestrutura em background
docker compose up -d

# Verificar se todos os contêineres subiram corretamente
docker compose ps
```

A saída esperada deve mostrar todos com status `Up` (ou `healthy`):

```
NAME            STATUS
fvs-postgres    Up (healthy)
fvs-redis       Up
fvs-rabbitmq    Up (healthy)
fvs-ftp         Up
```

> [!NOTE]
> Na primeira execução, o Docker vai baixar as imagens (PostgreSQL, Redis, etc.) da internet. Isso pode levar alguns minutos dependendo da velocidade da VPS.

---

## 10. Passo 8 — Compilar e Publicar os Serviços .NET

### 10.1. Publicar a API

```bash
cd /opt/versatus/versatus-force-sales-mvp

dotnet publish src/backend/Versatus.ForcaVendas.Api \
  --configuration Release \
  --output /opt/versatus/publish/api

echo "✅ API publicada em /opt/versatus/publish/api"
```

### 10.2. Publicar o Worker

```bash
dotnet publish src/worker/Versatus.ForcaVendas.Worker \
  --configuration Release \
  --output /opt/versatus/publish/worker

echo "✅ Worker publicado em /opt/versatus/publish/worker"
```

### 10.3. Publicar o ERP Adapter

```bash
dotnet publish src/erp-adapter/Versatus.ForcaVendas.ErpAdapter \
  --configuration Release \
  --output /opt/versatus/publish/erp-adapter

echo "✅ ERP Adapter publicado em /opt/versatus/publish/erp-adapter"
```

---

## 11. Passo 9 — Registrar os Serviços .NET no systemd

O `systemd` garante que os serviços .NET iniciem automaticamente com o servidor e sejam reiniciados em caso de falha.

### 11.1. Criar o serviço da API

```bash
sudo tee /etc/systemd/system/fvs-api.service > /dev/null << EOF
[Unit]
Description=Versatus Force Sales - API Gateway
After=network.target docker.service
Requires=docker.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/versatus/publish/api
EnvironmentFile=/opt/versatus/versatus-force-sales-mvp/.env
ExecStart=/root/.dotnet/dotnet Versatus.ForcaVendas.Api.dll
Restart=always
RestartSec=10
SyslogIdentifier=fvs-api

[Install]
WantedBy=multi-user.target
EOF
```

### 11.2. Criar o serviço do Worker

```bash
sudo tee /etc/systemd/system/fvs-worker.service > /dev/null << EOF
[Unit]
Description=Versatus Force Sales - Worker
After=network.target fvs-api.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/versatus/publish/worker
EnvironmentFile=/opt/versatus/versatus-force-sales-mvp/.env
ExecStart=/root/.dotnet/dotnet Versatus.ForcaVendas.Worker.dll
Restart=always
RestartSec=10
SyslogIdentifier=fvs-worker

[Install]
WantedBy=multi-user.target
EOF
```

### 11.3. Criar o serviço do ERP Adapter

```bash
sudo tee /etc/systemd/system/fvs-erp-adapter.service > /dev/null << EOF
[Unit]
Description=Versatus Force Sales - ERP Adapter
After=network.target fvs-api.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/versatus/publish/erp-adapter
EnvironmentFile=/opt/versatus/versatus-force-sales-mvp/.env
ExecStart=/root/.dotnet/dotnet Versatus.ForcaVendas.ErpAdapter.dll
Restart=always
RestartSec=10
SyslogIdentifier=fvs-erp-adapter

[Install]
WantedBy=multi-user.target
EOF
```

### 11.4. Ativar e iniciar os serviços

```bash
sudo systemctl daemon-reload

# Habilitar (inicializar junto com o servidor)
sudo systemctl enable fvs-api fvs-worker fvs-erp-adapter

# Iniciar agora
sudo systemctl start fvs-api fvs-worker fvs-erp-adapter

# Verificar o status
sudo systemctl status fvs-api fvs-worker fvs-erp-adapter
```

---

## 12. Passo 10 — Build e Deploy do Frontend (Next.js)

### 12.1. Instalar dependências e fazer o build de produção

```bash
cd /opt/versatus/versatus-force-sales-mvp/src/frontend/app

npm install
npm run build
```

### 12.2. Registrar o Frontend no systemd

```bash
sudo tee /etc/systemd/system/fvs-frontend.service > /dev/null << EOF
[Unit]
Description=Versatus Force Sales - Frontend Next.js
After=network.target fvs-api.service

[Service]
Type=simple
User=root
WorkingDirectory=/opt/versatus/versatus-force-sales-mvp/src/frontend/app
Environment=NODE_ENV=production
Environment=PORT=3000
ExecStart=$(which node) node_modules/.bin/next start --port 3000
Restart=always
RestartSec=5
SyslogIdentifier=fvs-frontend

[Install]
WantedBy=multi-user.target
EOF

sudo systemctl daemon-reload
sudo systemctl enable fvs-frontend
sudo systemctl start fvs-frontend
```

---

## 13. Passo 11 — Configurar o Nginx como Proxy Reverso

O Nginx recebe as requisições externas (porta 80/443) e as distribui entre o frontend e a API.

### 13.1. Instalar o Nginx

```bash
sudo apt install -y nginx
```

### 13.2. Criar a configuração do site

```bash
sudo tee /etc/nginx/sites-available/versatus-fvs > /dev/null << 'EOF'
server {
    listen 80;
    server_name SEU_DOMINIO.com.br www.SEU_DOMINIO.com.br;  # ⚠️ Troque pelo seu domínio

    # Frontend Next.js
    location / {
        proxy_pass http://localhost:3000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }

    # API .NET Gateway
    location /api/ {
        proxy_pass http://localhost:5000/api/;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }

    # Aumentar timeout para uploads (sincronização de catálogo)
    proxy_connect_timeout 120s;
    proxy_read_timeout    120s;
    client_max_body_size  50M;
}
EOF
```

### 13.3. Ativar o site

```bash
sudo ln -s /etc/nginx/sites-available/versatus-fvs /etc/nginx/sites-enabled/
sudo nginx -t   # Verificar se a configuração está correta
sudo systemctl reload nginx
```

---

## 14. Passo 12 — Instalar o SSL com Let's Encrypt (HTTPS gratuito)

> [!IMPORTANT]
> Este passo só funciona se o domínio já estiver apontando para o IP da VPS. Verifique com `ping SEU_DOMINIO.com.br` antes de continuar.

```bash
# Instalar o Certbot
sudo apt install -y certbot python3-certbot-nginx

# Gerar e instalar o certificado automaticamente
sudo certbot --nginx -d SEU_DOMINIO.com.br -d www.SEU_DOMINIO.com.br

# O Certbot vai perguntar seu e-mail e confirmar os termos.
# Ao final, ele configura o HTTPS automaticamente no Nginx.

# Verificar renovação automática
sudo certbot renew --dry-run
```

---

## 15. Verificação Final

Após todos os passos, verifique se tudo está funcionando:

```bash
# Verificar todos os serviços systemd
sudo systemctl status fvs-api fvs-worker fvs-erp-adapter fvs-frontend

# Verificar todos os contêineres Docker
docker compose -f /opt/versatus/versatus-force-sales-mvp/docker-compose.yml ps

# Testar se a API responde
curl http://localhost:5000/health

# Testar se o frontend responde
curl http://localhost:3000
```

Abra o navegador e acesse `https://SEU_DOMINIO.com.br`. O sistema deve carregar a tela de login.

---

## 16. Comandos Úteis de Gerenciamento no Servidor

### Ver logs dos serviços em tempo real

```bash
# Logs da API
sudo journalctl -u fvs-api -f

# Logs do Worker
sudo journalctl -u fvs-worker -f

# Logs do Frontend
sudo journalctl -u fvs-frontend -f

# Logs de toda a infraestrutura Docker
docker compose -f /opt/versatus/versatus-force-sales-mvp/docker-compose.yml logs -f
```

### Reiniciar serviços

```bash
sudo systemctl restart fvs-api fvs-worker fvs-erp-adapter fvs-frontend
```

### Atualizar a aplicação após novos commits

```bash
cd /opt/versatus/versatus-force-sales-mvp

# Baixar atualizações do repositório
git pull origin develop   # ou 'main' em produção estável

# Recompilar os serviços .NET
dotnet publish src/backend/Versatus.ForcaVendas.Api --configuration Release --output /opt/versatus/publish/api
dotnet publish src/worker/Versatus.ForcaVendas.Worker --configuration Release --output /opt/versatus/publish/worker
dotnet publish src/erp-adapter/Versatus.ForcaVendas.ErpAdapter --configuration Release --output /opt/versatus/publish/erp-adapter

# Rebuild do frontend
cd src/frontend/app
npm install
npm run build
cd ../../..

# Reiniciar todos os serviços
sudo systemctl restart fvs-api fvs-worker fvs-erp-adapter fvs-frontend
```

---

## 17. Solução de Problemas Comuns na VPS

### O site não carrega / ERR_CONNECTION_REFUSED
- Verifique se o Nginx está rodando: `sudo systemctl status nginx`
- Verifique se as portas estão abertas no firewall da VPS (ver abaixo)

### Firewall — Abrir as portas necessárias

```bash
sudo ufw allow 22     # SSH
sudo ufw allow 80     # HTTP
sudo ufw allow 443    # HTTPS
sudo ufw enable
```

> [!WARNING]
> Não exponha as portas internas (5432, 6379, 5672, 3000, 5000) diretamente na internet. Elas devem ficar acessíveis **apenas internamente** no servidor (via `localhost`). O Nginx age como a única porta de entrada pública.

### O serviço .NET trava e não inicia
- Veja os logs detalhados: `sudo journalctl -u fvs-api --since "10 minutes ago" --no-pager`
- Causa comum: banco de dados ainda não subiu. Aguarde 30 segundos após `docker compose up -d` antes de iniciar os serviços .NET.

### "Migrations not applied" — tabelas não existem
- Os serviços .NET aplicam as migrations automaticamente ao iniciar. Se o banco foi apagado (via `docker compose down -v`), pare todos os serviços e reinicie-os:

```bash
sudo systemctl stop fvs-api fvs-worker fvs-erp-adapter
docker compose -f /opt/versatus/versatus-force-sales-mvp/docker-compose.yml down -v
docker compose -f /opt/versatus/versatus-force-sales-mvp/docker-compose.yml up -d
sleep 15   # Aguardar o banco inicializar
sudo systemctl start fvs-api fvs-worker fvs-erp-adapter
```

### Erro de memória insuficiente durante o build do Next.js
```bash
# Aumentar a memória disponível para o Node.js durante o build
NODE_OPTIONS="--max-old-space-size=1536" npm run build
```

---

## 18. Portas e Serviços — Mapa Completo

| Serviço | Porta | Acesso | Descrição |
|---|---|---|---|
| Nginx | 80, 443 | **Público** | Proxy reverso e SSL |
| Frontend Next.js | 3000 | Apenas interno | Interface web |
| API .NET | 5000 | Apenas interno | Backend principal |
| PostgreSQL | 5432 | Apenas interno | Banco de dados |
| Redis | 6379 | Apenas interno | Cache de catálogo |
| RabbitMQ (AMQP) | 5672 | Apenas interno | Fila de mensagens |
| RabbitMQ (Painel) | 15672 | Apenas interno | Painel web do RabbitMQ |
| FTP | 21 | Conforme necessidade | Integração ERP |
