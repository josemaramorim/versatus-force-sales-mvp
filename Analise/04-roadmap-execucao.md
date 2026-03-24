# Roadmap de execucao

## Objetivo

Sair do modelo atual em .NET Framework + Remoting para uma plataforma em .NET 8 com API moderna, sem big bang.

## Fase 0 - Descoberta e cercamento

Duracao sugerida: 2 a 4 semanas.

Entregas:

1. mapa de dependencias entre modulos;
2. inventario de queries e operacoes mais usadas;
3. definicao de dono por dominio;
4. definicao de padrao de API, auth, logs e observabilidade;
5. lista dos 20 a 30 casos de uso com maior valor.

Saida esperada:

- backlog tecnicamente ordenado;
- criterio de pronto para cada modulo migrado.

## Fase 1 - Fundacao da nova plataforma

Duracao sugerida: 3 a 6 semanas.

Entregas:

1. nova solucao .NET 8;
2. API base com health checks, auth, swagger, logs e metrics;
3. estrutura de camadas e convencoes de modulo;
4. pipeline CI/CD;
5. padrao de migracao de banco e configuracao;
6. mecanismo de contexto de empresa, filial, usuario e permissao.

Saida esperada:

- plataforma pronta para receber o primeiro dominio.

## Fase 2 - Primeiro modulo piloto

Duracao sugerida: 4 a 8 semanas.

Escolha recomendada:

- um subconjunto de `acesso.global` com alto valor e baixa ambiguidade funcional.

Exemplos:

- usuarios;
- perfis;
- empresas;
- filiais;
- parametros basicos;
- autenticacao / autorizacao.

Entregas:

1. endpoints novos em .NET 8;
2. testes automatizados do modulo;
3. monitoracao de uso e erros;
4. rollout controlado.

Saida esperada:

- primeira API real em producao;
- padrao repetivel para os proximos modulos.

## Fase 3 - Estrangulamento progressivo

Duracao sugerida: continua.

Para cada dominio:

1. escolher 5 a 15 casos de uso de maior valor;
2. mapear dependencias tecnicas e dados;
3. portar regra de negocio para `Domain` e `Application`;
4. implementar persistencia nova;
5. publicar endpoints;
6. redirecionar consumidores;
7. congelar manutencao evolutiva no trecho legado equivalente.

Regra operacional:

- nenhuma feature nova relevante deve nascer no legado se o dominio ja estiver em migracao ativa.

## Fase 4 - Bridge temporaria onde necessario

Use apenas quando um fluxo precisar sair rapido e a regra ainda estiver presa ao legado.

Regras para a bridge:

1. prazo de validade definido;
2. escopo pequeno;
3. contrato HTTP explicito;
4. ownership claro;
5. backlog de remocao criado no momento em que ela nascer.

## Fase 5 - Aposentadoria do legado por fatia

Quando um modulo estiver estavel na nova plataforma:

1. bloquear novas alteracoes no modulo legado;
2. remover chamadas do cliente antigo para aquele fluxo;
3. desligar registros de factory e objetos remotos equivalentes;
4. retirar dependencias do modulo do build principal.

## Definicao de pronto por modulo

Considere um dominio migrado apenas quando houver:

1. API publicada;
2. regras portadas sem dependencia de Remoting;
3. telemetria minima;
4. testes automatizados criticos;
5. rollback conhecido;
6. consumidor principal redirecionado.

## Riscos principais

1. Tentar migrar pelo nome dos projetos e nao pelos casos de uso.
2. Reaproveitar contratos legados contaminados por UI.
3. Criar uma bridge permanente sem data de remocao.
4. Fazer microservicos cedo demais.
5. Subestimar a migracao de seguranca, sessao e contexto de ambiente.
6. Migrar fiscal sem ter estabilizado a fundacao transacional.

## Primeiros 90 dias sugeridos

### Dias 1 a 30

- fechar arquitetura alvo;
- montar a nova solucao .NET 8;
- catalogar casos de uso prioritarios;
- definir o piloto.

### Dias 31 a 60

- implementar fundacao tecnica;
- criar contratos e endpoints do piloto;
- subir ambiente de homologacao;
- testar carga e observabilidade.

### Dias 61 a 90

- publicar o piloto;
- medir uso, falhas e gaps;
- ajustar o padrao;
- iniciar o segundo modulo.

## Decisao final recomendada

Se a pergunta for "faço uma API .NET Core?", a resposta e:

**Sim, mas como frente de uma nova plataforma em .NET 8 e nao como simples embalagem HTTP do desenho remoto atual.**

Se a pergunta for "mantenho .NET Remoting?", a resposta e:

**Nao. Trate Remoting como passivo tecnico a ser eliminado.**