# Arquitetura alvo recomendada

## Recomendacao principal

A melhor estrategia para esse ERP e:

**ASP.NET Core em .NET 8 LTS + monolito modular + estrangulamento por dominio + retirada progressiva do Remoting.**

## Por que essa e a melhor escolha

### ASP.NET Core em .NET 8

- e a plataforma suportada e atual do ecossistema Microsoft;
- substitui bem a necessidade de exposicao HTTP/JSON;
- tem bom suporte para observabilidade, seguranca, versionamento e testes;
- permite evoluir depois para modularizacao maior sem reescrever a borda da aplicacao.

### Monolito modular antes de microservicos

Esse sistema e grande e fortemente acoplado. Ir direto para microservicos aumentaria risco de:

- fronteiras ruins de dominio;
- transacoes distribuidas prematuras;
- custo operacional desnecessario;
- dependencia excessiva de mensageria antes da hora.

Para esse caso, monolito modular e a opcao mais pragmatica:

- um deploy;
- uma observabilidade;
- uma seguranca centralizada;
- isolamento por modulo no codigo e no banco, quando possivel;
- capacidade de extrair servicos depois, apenas se houver justificativa real.

## Stack sugerida

### Camada de API

- ASP.NET Core Web API em .NET 8
- OpenAPI / Swagger
- versionamento de API
- autenticacao JWT ou token interno, dependendo do consumidor

### Camada de aplicacao

- servicos orientados a caso de uso
- DTOs proprios por endpoint
- validacao com FluentValidation
- pipeline de mediacao opcional com MediatR somente se a equipe gostar desse estilo

### Camada de persistencia

Recomendacao pragmatica:

- Dapper para consultas e comandos no inicio da migracao;
- EF Core apenas para modulos novos ou trechos em que o mapeamento traga ganho real;
- DbUp ou FluentMigrator para versionamento de banco.

Motivo: o legado ja parece trabalhar com SQL e ORM proprio. Dapper reduz atrito na transicao e evita reescrever tudo para EF Core sem retorno claro.

### Jobs e processos em background

- `BackgroundService` no .NET 8 para rotinas simples
- Hangfire ou Quartz.NET se houver agendamento e painel operacional mais forte

### Observabilidade

- Serilog
- OpenTelemetry
- health checks
- correlacao por request / usuario / filial / empresa

### Seguranca

- autenticacao centralizada
- autorizacao por policy/perfil
- trilha de auditoria por operacao critica

## Como substituir o .NET Remoting

## Decisao objetiva

Nao use .NET Remoting no futuro. Ele deve ser eliminado.

Substituicoes recomendadas por contexto:

- **HTTP/JSON com ASP.NET Core** para consumo por frontends, integracoes e clientes externos.
- **gRPC** apenas para chamadas internas entre servicos novos, quando realmente houver necessidade de alta performance e contratos fortes.
- **Fila/eventos** apenas quando houver cenarios assincronos claros, como integracoes, processamento fiscal ou jobs desacoplados.

## Arquitetura de transicao

Durante o estrangulamento, existem dois caminhos possiveis.

### Caminho A: migracao real por dominio

Para cada dominio priorizado:

1. recriar contratos de aplicacao em .NET 8;
2. portar regras para o novo modulo;
3. criar endpoints HTTP proprios;
4. deixar o legado atendendo apenas o que ainda nao foi migrado.

Esse e o melhor caminho de medio prazo.

### Caminho B: bridge temporaria para o legado

Quando uma regra ainda nao puder ser portada rapido:

1. a API .NET 8 chama um adaptador legado temporario;
2. esse adaptador roda em .NET Framework e conhece o remoting / objetos atuais;
3. o adaptador vira uma camada descartavel e some quando o modulo for reescrito.

Use isso com disciplina. A bridge deve ser temporaria e restrita aos modulos ainda nao extraidos.

## Desenho de solucao proposto

Estrutura conceitual:

- `Versatus.Api`: borda HTTP
- `Versatus.Application`: casos de uso
- `Versatus.Domain.<Modulo>`: regras e entidades do modulo
- `Versatus.Infrastructure`: acesso a dados, integracoes, mensageria, auth
- `Versatus.Contracts`: DTOs e contratos da API
- `Versatus.LegacyBridge` (temporario): adaptador para legado .NET Framework

## Principios para nao repetir os problemas do legado

1. Nao expor factories genericas remotas.
2. Nao compartilhar entidades de persistencia diretamente na API.
3. Nao misturar contrato de integracao com componentes de UI.
4. Nao portar dependencias desktop para a nova camada.
5. Nao criar microservicos sem fronteira de negocio validada.
6. Nao reescrever tudo antes de publicar o primeiro fluxo de valor.

## Resultado esperado

No estado alvo, a empresa passa a ter:

- uma API moderna e suportada;
- dominios migrados gradualmente;
- menos dependencia do servidor remoto legado;
- possibilidade real de aposentar o .NET Framework no ritmo dos modulos migrados.