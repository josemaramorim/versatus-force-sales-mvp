# Deploy do Frontend no Painel ICP (icontainer) via GitHub

Este documento descreve como publicar a aplicação **`Versatus.ForcaVendas.Frontend`** utilizando o **Painel ICP** da icontainer. A aplicação é construída em **Next.js 16** com **TypeScript** e **React 19**, rodando como uma aplicação Node.js (SSR).

> [!NOTE]
> Este guia é baseado na documentação oficial da icontainer: https://wiki.icontainer.run/pt-br/home

---

## 🗺️ Índice do Tutorial de Deploy

Selecione a etapa que deseja consultar ou siga o passo a passo na ordem sugerida:

### 🏁 Preparação e Acessos
* [1. Visão Geral](#visão-geral) — Estrutura e portas padrão do projeto.
* [2. Pré-requisitos](#pré-requisitos) — Versão do Node.js e validação local.
* [3. Conexão com o GitHub](#etapa-1--conectar-o-github-no-painel-icp) — Como vincular o repositório no painel.

### 🚀 Deploy Nível 1 — Fluxo Contínuo (Git + Auto-compilação)
* [4. Criar App Node.js no ICP](#etapa-2--configurar-a-aplicação-nodejs-no-painel-icp) — Configuração do assistente Standalone.
* [5. Variáveis de Ambiente e build-time](#etapa-3--variáveis-de-ambiente-e-detalhe-crítico-de-build-time) — Explicação sobre o comportamento das variáveis `NEXT_PUBLIC_`.
* [6. Webhooks de Auto-deploy](#etapa-4--configurar-o-webhook-do-github) — Atualização automática com `git push`.

### 🛡️ Diagnóstico e Solução de Problemas
* [7. Verificação Pós-Deploy e Acesso no Navegador](#etapa-5--verificação-pós-deploy-e-acesso-no-navegador) — Como chamar e testar a aplicação no browser.
* [8. Resolução de Erros Comuns](#solução-de-problemas) — O que fazer se a tela ficar em branco ou não conectar à API.
  * [8.1. Erro ao Descompactar o ZIP](#erro-ao-descompactar-o-arquivo-zip-no-painel-mkdir--not-a-directory-ou-falha-no-progresso) — Solução para falhas de descompactação, arquivos gigantes e erros com parênteses (ex: route groups).
  * [8.2. Erro sh: next: Permission denied](#erro-ao-iniciar-a-aplicação-sh-next-permission-denied) — Correção para falta de permissão de execução nos binários após transferir o ZIP.

### 🔌 Deploy Nível 2 — Método ZIP Local (Solução para conexões instáveis)
* [9. Por que usar o ZIP?](#método-alternativo--publicação-via-arquivo-zip-recomendado-para-conexões-instáveis) — Vantagens do deploy local.
* [10. Compilar e Zipar Localmente](#b1-compilar-e-gerar-o-zip-localmente) — Comandos rápidos em PowerShell.
* [11. Configurar a Aplicação ZIP no ICP](#b2-configurar-a-aplicação-zip-no-painel-icp) — Passos do formulário utilizando o ZIP.
* [12. Atualizar arquivos via SFTP](#b3-envio-e-atualização-via-sftp-sem-limites-do-navegador) — Transferência direta e segura.

---

## Visão Geral

O projeto do frontend está localizado em:
```
src/frontend/app/
```

*   **Framework:** Next.js 16 (React 19)
*   **Gerenciador de Pacotes:** npm
*   **Porta padrão:** `3000`
*   **Tipo de Renderização:** Server-Side Rendering (SSR) / Client-Side híbrido
*   **Comunicação com a API:** Utiliza a variável `NEXT_PUBLIC_API_URL` para realizar as chamadas ao backend.

---

## Pré-requisitos

Antes de iniciar, certifique-se de possuir:
*   [ ] Conta no GitHub vinculada ao repositório `josemaramorim/versatus-force-sales-mvp`.
*   [ ] Acesso de administrador ao **Painel ICP** (VPS `vps9526.panel.icontainer.net`).
*   [ ] Node.js v20.x ou superior instalado na máquina local.
*   [ ] A aplicação instalando dependências e compilando localmente através dos comandos:

```bash
cd src/frontend/app
npm install
npm run build
```

---

## Etapa 1 — Conectar o GitHub no Painel ICP

Se você já realizou o deploy da API utilizando a integração com o GitHub, o seu token do GitHub já estará configurado globalmente no painel. Caso contrário:

1.  Acesse o Painel ICP (`https://vps9526.panel.icontainer.net`).
2.  Vá em **Configurações** (ícone de engrenagem) -> aba **Git / GitHub**.
3.  Insira seu **Personal Access Token (classic)** do GitHub com escopo `repo`.
4.  Clique em **Salvar**.

---

## Etapa 2 — Configurar a Aplicação Node.js no Painel ICP

O painel ICP oferece suporte nativo a aplicações Node.js através do modelo de **Aplicação Standalone**.

### 2.1. Criar a aplicação no painel
1.  No painel ICP, acesse **Aplicações** -> **Nova Aplicação**.
2.  Selecione o tipo: **Node.js** (Standalone).
3.  Preencha as configurações do repositório:

| Campo | Valor |
|---|---|
| **Repositório GitHub** | `josemaramorim/versatus-force-sales-mvp` |
| **Branch** | `develop` (homologação) ou `main` (produção) |
| **Diretório do projeto** | `src/frontend/app` |

### 2.2. Configurar os comandos e portas
O Next.js exige uma etapa de compilação antes de iniciar o servidor de produção.

*   **Comando de Instalação:** `npm install`
*   **Comando de Compilação (Build):** `npm run build`
*   **Comando de Inicialização (Start):** `npm run start`
*   **Porta da Aplicação:** `3000`
*   **Porta Externa (Porta Web):** `3000` (ou a porta pública desejada para o domínio)

---

## Etapa 3 — Variáveis de Ambiente e Detalhe Crítico de "Build-Time"

> [!WARNING]
> **COMPORTAMENTO DAS VARIÁVEIS `NEXT_PUBLIC_`:**
> No Next.js, todas as variáveis de ambiente que começam com o prefixo `NEXT_PUBLIC_` são injetadas diretamente no código JavaScript que roda no navegador do cliente. Isso significa que **elas precisam estar disponíveis no momento da compilação (`npm run build`)**.
> 
> Se você tentar configurar a variável após a compilação ou apenas no momento da execução, o navegador não conseguirá ler o endereço da API e a aplicação falhará ao tentar se conectar ao backend.

### 3.1. Cadastrar as variáveis no ICP antes do build
No formulário de criação do aplicativo no painel ICP, adicione as seguintes variáveis de ambiente:

```env
NODE_ENV=production
PORT=3000
ASPNETCORE_ENVIRONMENT=Production

# URL pública da API backend que o frontend vai acessar
# ⚠️ Substitua pelo endereço real onde a sua API está rodando
NEXT_PUBLIC_API_URL=https://force-sales-api.vps9526.panel.icontainer.net
```

### 3.2. Finalizar a criação
Clique em **Confirmar** ou **Criar**. O ICP irá:
1.  Clonar a branch selecionada do repositório.
2.  Executar `npm install` no diretório `src/frontend/app`.
3.  Executar `npm run build` (neste momento, a variável `NEXT_PUBLIC_API_URL` será gravada nos arquivos estáticos).
4.  Iniciar o servidor de produção com `npm run start`.

---

## Etapa 4 — Configurar o Webhook do GitHub

Para automatizar as atualizações sempre que um novo commit for enviado:

1.  No Painel ICP, vá nas configurações da aplicação do Frontend.
2.  Copie a **URL do Webhook** fornecida.
3.  Acesse o repositório no GitHub -> **Settings** -> **Webhooks** -> **Add webhook**.
4.  Cole a URL do webhook no campo **Payload URL**.
5.  Selecione o Content type como `application/json`.
6.  Marque **Just the push event** e clique e **Add webhook**.

---

## Etapa 5 — Verificação Pós-Deploy e Acesso no Navegador

Após a conclusão da compilação e inicialização no painel ICP, você poderá acessar a aplicação de duas maneiras no seu navegador, trabalhando sempre com segurança (HTTPS/SSL):

### 5.1. Acesso Rápido via Porta (IP/Domínio da VPS com SSL)
Ideal para validações rápidas e testes logo após a instalação utilizando o protocolo seguro.

1. Abra o navegador de sua preferência.
2. Digite o endereço da VPS com **`https://`** seguido da porta configurada para o frontend (porta padrão `3000`). Exemplo:
   * **Via Hostname do ICP:** `https://vps9526.panel.icontainer.net:3000`
   * **Via IP Direto:** `https://23.80.91.77:3000` *(Substitua pelo IP real da sua VPS)*
3. **Alerta de Certificado em Testes:** Como você está acessando a porta `3000` diretamente via IP ou hostname genérico do ICP, o navegador exibirá um alerta de privacidade (ex: `Sua conexão não é privada` ou `ERR_CERT_COMMON_NAME_INVALID`). **Para testes rápidos, isso é perfeitamente normal:** basta clicar em **Avançado** e depois em **Prosseguir/Ir para o site (não seguro)**. O navegador aceitará a exceção e abrirá a tela de login com segurança.
4. **Nota sobre Firewall:** Caso a página não carregue de forma alguma, certifique-se de que a porta `3000` está liberada nas regras de segurança/firewall do painel da sua VPS ou da icontainer.

---

### 5.2. Acesso Definitivo via Domínio/Subdomínio com SSL (Recomendado para Produção)
Para ambiente de produção ou homologação formal, o ideal é acessar o sistema por um endereço amigável (ex: `https://vendas.suaempresa.com.br`) e seguro (HTTPS).

#### Passo 1 — Apontamento de DNS
No painel do seu provedor de domínio (Registro.br, Cloudflare, GoDaddy, etc.), crie um apontamento DNS para que o domínio vá para o seu servidor:
* **Tipo:** `A`
* **Nome/Host:** `vendas` (ou o subdomínio desejado) ou `@` (para o domínio principal)
* **Destino/Aponta para:** IP público da sua VPS (ex: `23.80.91.77`)

#### Passo 2 — Vincular o Domínio no Painel ICP
1. Acesse o **Painel ICP** (`https://vps9526.panel.icontainer.net`).
2. Vá em **Aplicações** e selecione a aplicação do seu Frontend.
3. Acesse a aba **Domínios** ou **Configuração Web** (dependendo da versão do painel).
4. Insira o seu domínio completo (ex: `vendas.suaempresa.com.br`) e salve. O painel ICP criará automaticamente a configuração do proxy reverso no Nginx interno do servidor.

#### Passo 3 — Ativar o SSL (HTTPS Grátis)
1. Ainda na configuração de domínios no painel ICP, localize a opção de **SSL** ou **Let's Encrypt**.
2. Clique em **Ativar SSL** ou **Gerar Certificado**.
3. O painel emitirá e instalará o certificado de segurança de forma automatizada.
4. A partir deste momento, a aplicação estará acessível com segurança no navegador através de:
   ```text
   https://vendas.suaempresa.com.br
   ```

---

### 5.3. Credenciais de Acesso Inicial
Ao carregar a tela de login no navegador, utilize as seguintes credenciais padrão criadas no seed do banco de dados para o primeiro acesso:

* **E-mail:** `admin@demo1.versatus.com`
* **Senha:** `123456`

---

### 5.4. Verificação de Saúde e Erros Comuns no Console
Ao abrir a aplicação no browser pela primeira vez, realize o seguinte teste básico de funcionamento:

1. Preencha as credenciais e clique em **Entrar**.
2. Se o login funcionar e você for redirecionado ao painel, a integração está 100% operacional.
3. Se o login falhar silenciosamente ou apresentar erro de conexão:
   * Pressione **F12** no seu teclado para abrir as *Ferramentas do Desenvolvedor*.
   * Vá até a aba **Console**.
   * Verifique se existem erros vermelhos de rede como `ERR_CONNECTION_REFUSED` ou `Blocked by CORS policy`.
   * Caso ocorra, confirme se a API está de fato rodando na URL configurada na variável `NEXT_PUBLIC_API_URL` e que as políticas de CORS na API permitem conexões vindas do domínio do seu frontend.

---

## Solução de Problemas

### Erro: Conexão com a API falha ou aponta para `undefined` ou `localhost`
*   **Causa:** A variável `NEXT_PUBLIC_API_URL` não estava presente no momento em que o comando `npm run build` foi executado.
*   **Solução:** 
    1. Acesse as configurações da aplicação no painel ICP.
    2. Garanta que a variável `NEXT_PUBLIC_API_URL` esteja preenchida corretamente com a URL da API (incluindo `https://`).
    3. Clique em salvar e force uma nova compilação (**Rebuild** ou **Redeploy**) para reconstruir os arquivos estáticos injetando a URL correta.

### Limite de memória da VPS atingido durante o build (`JavaScript heap out of memory`)
*   **Causa:** O processo de compilação do Next.js pode exigir muita memória RAM em servidores com recursos limitados.
*   **Solução:** Adicione a seguinte variável de ambiente no painel ICP para limitar o consumo de memória do Node.js:
    ```env
    NODE_OPTIONS=--max-old-space-size=1024
    ```
    Se a VPS possuir apenas 1GB ou 2GB de RAM total e continuar falhando por falta de memória, utilize o **Método ZIP Local** descrito abaixo.

### Erro ao descompactar o arquivo ZIP no painel (mkdir ...: not a directory ou falha no progresso)

*   **Causa 1 (Caminho incorreto):** No formulário de descompactação do Painel ICP, o campo **Caminho de Descompactação** foi preenchido apontando para a pasta `/.next` (ex: `/home/apps/forca-venda-web/.next`). Como o arquivo ZIP já contém a pasta `.next` internamente, isso tenta aninhar `.next` dentro de `.next` e gera conflito de escrita.
*   **Causa 2 (Arquivo ZIP gigantesco com cache de dev):** O arquivo `.zip` local foi gerado sem antes limpar o cache do modo de desenvolvimento (Turbopack). O cache do Turbopack (`.next/dev/cache/turbopack`) contém milhares de pequenos arquivos temporários, o que infla o tamanho do ZIP (podendo passar de 1 GB) e faz com que o descompactador do painel estoure limites de escrita ou de tempo de execução.
*   **Causa 3 (Bug de Caracteres Especiais / Parênteses no Painel Web - Ex: `(admin)`):** O Next.js (utilizando o App Router) agrupa rotas utilizando parênteses nas pastas (ex: `.next/server/app/(admin)`). Alguns painéis web de VPS possuem bugs ou limitações na biblioteca de descompactação que não conseguem criar ou manipular diretórios que contêm parênteses no nome, abortando a extração com o erro `not a directory`.

*   **Solução Geral & Limpeza:**
    1. **Corrigir o Caminho de Descompactação no Painel ICP:**
       Ao extrair o arquivo pelo gerenciador de arquivos do painel, certifique-se de preencher o **Caminho de Descompactação** apontando para a **raiz da aplicação**, e não para a pasta `.next`.
       * **Incorreto:** `/home/apps/forca-venda-web/.next`
       * **Correto:** `/home/apps/forca-venda-web`
    2. **Gerar um arquivo ZIP de produção limpo (Localmente):**
       Execute os seguintes passos no PowerShell local antes de compactar e enviar o arquivo novamente para diminuir o tamanho e limpar o cache de dev:
       ```powershell
       cd "c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\frontend\app"

       # 1. Apague a pasta .next antiga para limpar o cache de desenvolvimento local
       Remove-Item -Path ./.next -Recurse -Force -ErrorAction SilentlyContinue

       # 2. Gere a compilação de produção novamente (criará uma pasta .next limpa de apenas ~140MB)
       npm run build

       # 3. Apague o zip antigo
       Remove-Item -Path ./publish-frontend.zip -ErrorAction SilentlyContinue

       # 4. Gere o novo arquivo compactado
       Compress-Archive -Path .next, node_modules, public, package.json, next.config.ts, postcss.config.mjs, tsconfig.json -DestinationPath ./publish-frontend.zip -Force
       ```
    3. **Limpar o destino no Servidor:** Antes de tentar descompactar novamente, acesse o gerenciador de arquivos do painel, selecione a pasta `.next` antiga (e a pasta `node_modules` se existir) e **Exclua-as** para evitar conflitos de arquivos antigos.

*   **Solução Definitiva (Descompactação via Terminal/SSH):**
    Caso o erro de parênteses (Causa 3) persista ao tentar extrair pela interface web do painel, faça a descompactação usando o terminal nativo do Linux, que é robusto e não possui limitações de caracteres:
    
    1. **Acessar o Terminal da VPS:**
       * **Pelo Painel:** No menu lateral esquerdo do painel ICP, localize e clique em **Terminal** (logo abaixo de *Monitor*). Faça login com as credenciais do servidor (`root` e a senha da VPS).
       * **Pelo seu computador:** Abra o PowerShell ou Command Prompt e digite:
         ```powershell
         ssh root@vps9526.panel.icontainer.net
         ```
    2. **Executar os comandos de extração:**
       Uma vez conectado ao terminal da VPS, execute os comandos abaixo para navegar até a pasta, limpar os resíduos e extrair o ZIP de forma nativa e segura:
       ```bash
       # 1. Entrar na pasta do frontend
       cd /home/apps/forca-venda-web

       # 2. Remover resquícios antigos de pastas
       rm -rf .next node_modules

       # 3. Descompactar o arquivo usando o utilitário nativo do Linux
       unzip publish-frontend.zip
       ```
       *(Nota: Caso o sistema retorne que o comando `unzip` não existe, instale-o rodando `apt-get update && apt-get install -y unzip` e repita o comando de descompactação).*

### Erro ao iniciar a aplicação (`sh: next: Permission denied`)

*   **Causa:** Ao gerar o arquivo `.zip` no sistema operacional Windows e descompactá-lo no Linux, as permissões originais de execução do sistema Unix/Linux são perdidas. Com isso, os scripts binários da pasta `node_modules/.bin/` (incluindo o comando `next` que inicia o servidor) perdem o bit de execução (`+x`), e o Linux recusa a execução por segurança.
*   **Solução:**
    Você precisa dar a permissão de execução novamente para os binários do Next.js via terminal/SSH:
    1. Acesse o terminal da VPS (pelo painel ou via SSH).
    2. Execute os seguintes comandos para navegar até a pasta da aplicação e restaurar as permissões:
       ```bash
       cd /home/apps/forca-venda-web
       
       # Concede permissão de execução a todos os binários da pasta .bin
       chmod -R +x node_modules/.bin/
       
       # Concede permissão de execução diretamente ao executável interno do Next.js
       chmod +x node_modules/next/dist/bin/next
       ```
    3. Após executar os comandos acima, acesse a área de aplicações do painel ICP e clique em **Reiniciar** (ou **Iniciar**) o frontend. O erro de permissão será resolvido e o servidor iniciará perfeitamente.

---

## Método Alternativo — Publicação via Arquivo ZIP (Recomendado para conexões instáveis)

Se o servidor da VPS apresentar lentidão ao baixar pacotes de dependências via npm ou se a compilação falhar por falta de recursos de hardware na VPS, você pode realizar a instalação de dependências e a compilação de produção no seu computador local, enviando apenas o pacote pronto compactado em `.zip`.

### B.1. Compilar e Gerar o ZIP Localmente

Execute os seguintes passos no terminal (PowerShell) do seu computador Windows local:

1.  Abra o PowerShell e navegue até a pasta do frontend:
    ```powershell
    cd "c:\Pasta de Trabalho\Projetos\Analises\Versatus.Net\versatus-force-sales-mvp\src\frontend\app"
    ```

2.  Crie ou edite um arquivo `.env.local` na pasta `src/frontend/app` para definir a URL de produção da API para a compilação local:
    ```text
    NEXT_PUBLIC_API_URL=https://force-sales-api.vps9526.panel.icontainer.net
    ```

3.  Execute a instalação de dependências e gere o build de produção localmente:
    ```powershell
    npm install
    npm run build
    ```

4.  Crie o arquivo compactado contendo a pasta `.next` (que possui os arquivos compilados), a pasta `node_modules` (dependências necessárias para rodar o servidor), os arquivos de configuração e o `package.json`. No PowerShell, execute:
    ```powershell
    # Garante que não há arquivos zip antigos na pasta
    Remove-Item -Path ./publish-frontend.zip -ErrorAction SilentlyContinue

    # Compacta as pastas e arquivos cruciais para a execução
    Compress-Archive -Path .next, node_modules, public, package.json, next.config.ts, postcss.config.mjs, tsconfig.json -DestinationPath ./publish-frontend.zip -Force
    ```

O arquivo `publish-frontend.zip` será gerado. Ele contém a aplicação Next.js totalmente pronta para rodar em produção no servidor.

### B.2. Configurar a Aplicação ZIP no Painel ICP

1.  No Painel ICP, vá em **Aplicações** -> **Nova Aplicação** (ou edite a existente).
2.  Selecione o tipo **Node.js** (Standalone).
3.  Em **Arquivos do Projeto**, selecione a aba **Enviar arquivos** (primeira aba à esquerda).
4.  Faça o upload do arquivo `publish-frontend.zip` gerado localmente.
5.  Preencha as configurações do formulário de inicialização:

| Campo no ICP | Valor |
|---|---|
| **Comando de Instalação** | *(deixe em branco, pois as dependências já estão no ZIP)* |
| **Comando de Compilação** | *(deixe em branco, pois a pasta .next já está compilada)* |
| **Comando de Inicialização** | `npm run start` ou `node_modules/next/dist/bin/next start` |
| **Porta da aplicação** | `3000` |

6.  Garanta que as variáveis de ambiente necessárias estejam cadastradas na aba de variáveis do painel (conforme a [Etapa 3](#etapa-3--variáveis-de-ambiente-e-detalhe-crítico-de-build-time)).
7.  Confirme a criação ou salvamento. O painel extrairá o ZIP e iniciará o servidor Next.js de produção imediatamente.

### B.3. Envio e Atualização via SFTP (Sem limites do navegador)

Caso o arquivo `.zip` seja muito grande para o upload convencional pelo navegador, utilize um cliente SFTP (como FileZilla ou WinSCP) para enviar os arquivos diretamente:

1.  Conecte-se à VPS utilizando os dados de acesso SSH:
    *   **Host:** `vps9526.panel.icontainer.net` (ou o IP do servidor)
    *   **Porta:** `22`
    *   **Usuário:** `root`
    *   **Senha:** `(sua senha SSH)`
2.  Acesse a pasta correspondente à aplicação do frontend na VPS (geralmente criada sob `/home/apps/` ou o caminho correspondente gerado pelo painel):
    ```text
    /home/apps/force-sales-frontend/
    ```
3.  Transfira o conteúdo das pastas locais `.next`, `node_modules`, `public`, além do arquivo `package.json`, diretamente para a pasta do servidor.
4.  No Painel ICP, clique em **Reiniciar** a aplicação para que ela carregue as novas alterações compiladas.
