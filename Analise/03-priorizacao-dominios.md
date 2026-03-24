# Priorizacao sugerida dos dominios

## Criterios usados

Para definir a ordem do estrangulamento, combinei quatro fatores:

1. centralidade para o ERP;
2. impacto em outros modulos;
3. valor para a primeira API;
4. tamanho / risco aproximado do legado.

## Ordem recomendada

### Faixa 1: fundacao obrigatoria

1. **acesso.global**
   Motivo: concentracao de dados base, ambiente, seguranca, parametros e cadastros compartilhados. Sem esse modulo limpo, os demais ficam dependentes do legado.

2. **gestao.financeira**
   Motivo: modulo central do ERP, com alto valor de negocio e forte dependencia cruzada.

3. **faturamento**
   Motivo: costuma ser a principal origem de eventos financeiros, fiscais e operacionais.

4. **gestao.tributo**
   Motivo: forte relacao com faturamento e compliance.

### Faixa 2: operacao principal

5. **gestao.material**
6. **gestao.compra**
7. **Gestao.Transporte**
8. **Gestao.RH**

Motivo: esses modulos sustentam a operacao principal do ERP e normalmente dependem da base transacional central.

### Faixa 3: fiscal e documentos eletronicos

9. **NFe**
10. **NFSe**
11. **MDFe**
12. **SPED.Fiscal**
13. **SPED.PisCofins**

Motivo: sao modulos de alta criticidade regulatoria, mas eu recomendo migrar depois da fundacao de ambiente, seguranca, financeiro e faturamento, a menos que haja pressao imediata de integracao fiscal.

### Faixa 4: dominios complementares

14. **Base.Distribuicao**
15. **Gestao.Educacional**
16. **Gestao.Frota**
17. **Gestao.Obra**
18. **Gestao.OS**
19. **Gestao.Small**
20. **Gestao.Contrato**
21. Demais modulos especializados

## Primeira onda recomendada para entrega real

Se o objetivo e publicar a primeira API util sem travar anos em migracao, eu faria a primeira onda assim:

1. autenticacao, sessao e contexto de ambiente;
2. empresas, filiais, usuarios, perfis e parametros;
3. cadastros base mais usados;
4. consultas financeiras principais;
5. operacoes centrais de faturamento;
6. trilha de auditoria e observabilidade.

Essa primeira onda cria fundacao reutilizavel para o restante do ERP.

## O que eu nao faria na primeira onda

- SPED completo;
- modulos muito especializados;
- relatorios complexos herdados;
- jobs historicos sem demanda imediata;
- tentativas de substituir todo o framework de persistencia antes da primeira entrega.

## Observacao importante

Se a necessidade imediata de negocio for integracao externa fiscal, a ordem pode mudar para:

1. acesso.global
2. faturamento
3. gestao.tributo
4. NFe / NFSe / MDFe
5. gestao.financeira

Ou seja: a ordem final deve combinar dependencia tecnica com dor real do negocio.