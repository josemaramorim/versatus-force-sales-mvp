# Plano tecnico executavel - Piloto acesso.global

## Objetivo do piloto

Entregar o primeiro modulo em .NET 8 que substitui uma parte central do legado sem depender de .NET Remoting no fluxo novo.

Escopo do piloto: identidade, contexto de ambiente e autorizacao basica.

Resultado esperado:

1. API funcional para autenticacao e contexto (grupo/empresa/filial).
2. API funcional para usuarios e perfis (consulta e manutencao basica).
3. Consumidor novo usando HTTP/JSON para esses fluxos.
4. Reducao de chamadas remotas antigas nesse recorte.

## Base de evidencias do modulo

O modulo `acesso.global` concentra entidades e regras de acesso, com alto acoplamento no legado:

- interfaces de negocio muito amplas em `IAcessoGlobalFactory`;
- sessao e conexao pelo ambiente remoto em `IAmbienteServidor`;
- ponto de entrada cliente-servidor em `IClienteServidor`;
- entidades chave de identidade e autorizacao em `IUsuario`, `IPerfil`, `IEmpresa`, `IFilial`.

Arquivos de referencia:

- `projeto_tag_1906/servidor/servidor.interface/Acesso.Global/IAcessoGlobalFactory.cs`
- `projeto_tag_1906/servidor/servidor.interface/Ambiente/IAmbienteServidor.cs`
- `projeto_tag_1906/servidor/servidor.interface/framework/IClienteServidor.cs`
- `projeto_tag_1906/servidor/servidor.interface/Acesso.Global/IUsuario.cs`
- `projeto_tag_1906/servidor/servidor.interface/Acesso.Global/IPerfil.cs`

## Escopo funcional do piloto

## Dentro do escopo (MVP)

1. Login e renovacao de sessao.
2. Contexto do usuario logado (grupo, empresa, filial, perfis, modulos basicos).
3. CRUD basico de usuario (listar, detalhar, criar, editar, ativar/inativar).
4. Alteracao de senha e politica minima de senha.
5. CRUD basico de perfil (listar, detalhar, criar, editar).
6. Vínculo usuario x perfil.
7. Consulta de empresas e filiais liberadas para o usuario.
8. Auditoria minima de alteracoes criticas (usuario, perfil, senha).

## Fora do escopo inicial

1. Reescrita completa de `IAcessoGlobalFactory`.
2. Todo o pacote fiscal dentro de acesso.global (serie vigencia, certificados, etc.).
3. Lookups e consultas historicas de baixo valor no primeiro ciclo.
4. Regras de menu fino e todas as excecoes de autorizacao legadas.
5. Jobs, integracoes e operacoes adjacentes nao necessarias para o MVP.

## Contratos de API sugeridos (v1)

Prefixo: `/api/v1`

### Autenticacao e sessao

1. `POST /auth/login`
2. `POST /auth/refresh`
3. `POST /auth/logout`
4. `GET /auth/me`

### Contexto

1. `GET /context/groups`
2. `GET /context/companies?groupId=`
3. `GET /context/branches?groupId=&companyId=`
4. `POST /context/select`

### Usuarios

1. `GET /users`
2. `GET /users/{id}`
3. `POST /users`
4. `PUT /users/{id}`
5. `PATCH /users/{id}/status`
6. `POST /users/{id}/change-password`
7. `GET /users/{id}/profiles`
8. `PUT /users/{id}/profiles`

### Perfis

1. `GET /profiles`
2. `GET /profiles/{id}`
3. `POST /profiles`
4. `PUT /profiles/{id}`
5. `GET /profiles/{id}/permissions`
6. `PUT /profiles/{id}/permissions`

### Empresas e filiais

1. `GET /companies`
2. `GET /companies/{id}`
3. `GET /companies/{id}/branches`
4. `GET /branches/{id}`

## Modelo de camadas sugerido para o piloto

Estrutura em .NET 8:

1. `Versatus.Api` - controllers, auth, versionamento, swagger.
2. `Versatus.Application.Access` - casos de uso do piloto.
3. `Versatus.Domain.Access` - entidades e regras de dominio.
4. `Versatus.Infrastructure.Access` - repositorios, SQL, adaptadores externos.
5. `Versatus.Contracts.Access` - requests/responses da API.

## Estrategia de dados para o piloto

Abordagem recomendada:

1. leitura inicial via SQL parametrizado e Dapper;
2. comandos de escrita com transacao explicita;
3. migracoes versionadas (DbUp/FluentMigrator) apenas para objetos novos;
4. sem alterar tabelas legadas sensiveis no primeiro ciclo, salvo necessidade objetiva.

## Estrangulamento por fluxo

## Fluxos que vao para API nova

1. login e contexto do usuario;
2. manutencao basica de usuario;
3. manutencao basica de perfil.

## Fluxos que ficam no legado temporariamente

1. consultas e operacoes fiscais dentro de acesso.global;
2. configuracoes complexas de serie e certificado;
3. regras de menu mais especificas nao exigidas pelo piloto.

## Mecanismo de coexistencia

1. cliente novo chama API .NET 8 para fluxos migrados;
2. cliente legado continua chamando Remoting para fluxos nao migrados;
3. manter rastreabilidade de qual fluxo esta em qual stack;
4. bloquear novas features nos fluxos ja migrados para evitar regressao arquitetural.

## Backlog inicial por onda

## Onda 1 - Fundacao tecnica (2 a 3 semanas)

1. criar solucoes e projetos do modulo Access.
2. padronizar auth, tratamento global de erro, logs e correlacao.
3. configurar observabilidade minima (health, logs estruturados, metricas basicas).
4. criar contrato de contexto de tenant (grupo/empresa/filial).
5. subir pipeline CI com build e testes.

## Onda 2 - Autenticacao e contexto (2 a 4 semanas)

1. implementar `POST /auth/login` e `GET /auth/me`.
2. implementar selecao de contexto (`/context/select`).
3. implementar listagem de empresas e filiais liberadas para o usuario.
4. criar testes de integracao para login e contexto.

## Onda 3 - Usuarios (3 a 5 semanas)

1. implementar listagem e detalhe de usuario.
2. implementar criar/editar usuario.
3. implementar ativar/inativar usuario.
4. implementar alterar senha.
5. implementar vinculo usuario x perfil.

## Onda 4 - Perfis (2 a 4 semanas)

1. implementar CRUD basico de perfil.
2. implementar leitura/edicao de permissoes essenciais.
3. validar regras de perfil administrador.
4. fechar trilha de auditoria para alteracoes de perfil.

## Onda 5 - Go-live controlado (1 a 2 semanas)

1. liberar para grupo piloto de usuarios.
2. monitorar erros, tempos e rejeicoes.
3. corrigir regressao funcional.
4. ampliar rollout por ondas.

## Definicao de pronto do piloto

Considerar o piloto concluido somente quando:

1. endpoints do escopo MVP estiverem estaveis em homologacao e producao piloto;
2. sucesso de login e carga de contexto com SLO acordado;
3. trilha de auditoria ativa para alteracoes sensiveis;
4. testes automatizados cobrindo fluxos criticos;
5. consumidores alvo usando API nova nesses fluxos;
6. fluxo equivalente legado congelado para evolucao funcional.

## Riscos e mitigacoes

1. Risco: carregar regras demais no piloto.
   Mitigacao: manter recorte estrito em identidade/contexto/perfis.

2. Risco: acoplamento escondido com outras factories.
   Mitigacao: mapear dependencias por caso de uso antes de cada endpoint.

3. Risco: divergencia de autorizacao entre legado e API nova.
   Mitigacao: tabela de equivalencia de permissoes e testes de regressao.

4. Risco: performance ruim no primeiro desenho.
   Mitigacao: observabilidade desde o inicio e tuning em consultas quentes.

5. Risco: bridge temporaria virar permanente.
   Mitigacao: todo adaptador nasce com prazo e tarefa de remocao.

## Metricas de sucesso do piloto

1. `% de logins` feitos via API nova no grupo piloto.
2. `tempo p95` de login e carga de contexto.
3. `taxa de erro` em endpoints de usuario/perfil.
4. `lead time` para entregar novo endpoint apos fundacao.
5. `% de chamados` relacionados a acesso apos migracao inicial.

## Proximo passo recomendado

Com esse plano aprovado, o passo imediato e iniciar a criacao da estrutura .NET 8 do modulo Access e abrir o backlog tecnico da Onda 1.