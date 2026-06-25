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
* [7. Verificação Pós-Deploy](#etapa-5--verificação-pós-deploy) — Testes de funcionamento e integração.
* [8. Resolução de Erros Comuns](#solução-de-problemas) — O que fazer se a tela ficar em branco ou não conectar à API.

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
6.  Marque **Just the push event** e clique em **Add webhook**.

---

## Etapa 5 — Verificação Pós-Deploy

Após a conclusão da compilação e inicialização no painel, certifique-se de que a aplicação está acessível:

1.  Acesse a URL gerada pelo painel ICP (ex: `http://vps9526.panel.icontainer.net:3000` ou o seu subdomínio configurado com SSL).
2.  A tela de login do sistema deverá ser exibida.
3.  Tente realizar o login com as credenciais padrão de produção:
    *   **E-mail:** `admin@demo1.versatus.com`
    *   **Senha:** `123456`
4.  Abra o Console do Desenvolvedor no navegador (F12 -> aba *Console*) e verifique se há erros de rede (CORS ou conexão recusada).

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
