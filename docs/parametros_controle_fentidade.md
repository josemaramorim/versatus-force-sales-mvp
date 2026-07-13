# Parâmetros de Controle Dinâmico - FEntidade (.cs ➡️ Frontend)

Este documento mapeia os parâmetros de configuração lidos no arquivo C# `FEntidade.cs` que controlam o comportamento dinâmico de visibilidade, bloqueios e validações de campos e abas do cadastro de Entidade. 

Estes parâmetros deverão ser integrados às chamadas de API de configurações de sistema no futuro para parametrizar o comportamento dinâmico das telas React/HTML (`FEntidade.tsx` e `FEntidade.html`).

---

## 📋 Tabela de Parâmetros Mapeados

| Parâmetro C# | Tipo | Comportamento no Frontend / Regra de Negócio | Componente Relacionado |
| :--- | :--- | :--- | :--- |
| `Parametros.UsaCentroCusto` | `Bool` | Habilita/Desabilita e oculta seletores de **Centro de Custo**. | Aba *Fornecedor* e Aba *Obra*. |
| `Parametros.UsaProjeto` | `Bool` | Habilita/Desabilita e oculta seletores de **Projeto**. | Aba *Fornecedor* e Aba *Obra*. |
| `Parametros.UsaClasse` | `Bool` | Habilita/Desabilita campos de **Classe de Operação**. | Aba *Obra* (Atendimento/Devolução). |
| `Parametros.DisponibilizaEntidadeEmpresa` | `Bool` | Se `false`, desabilita a edição da grade **7. Empresa** na aba *Informações Gerais*. | Aba *Informações Gerais* ➡️ Aba *Empresa*. |
| `Parametros.BloquearUsuarioTrocarConceito` | `Bool` | Se `true`, impede que o usuário altere o campo **Conceito** do Cliente (classificação de crédito). | Aba *Cliente* ➡️ Aba *Venda*. |
| `Parametros.ObraControlada` | `Bool` | Se `true`, impede alteração manual do campo **Situação** e **Data da Situação** da obra. | Aba *Obra*. |
| `Parametros.ClienteConsumidor` | `Int` | ID do cadastro do Consumidor Padrão. Se for este ID, bloqueia exclusão e alteração de dados fundamentais. | Cabeçalho / Geral. |
| `Parametros.TipoValidacaoTipoEspecificoEntidade` | `Enum` | Controla validações de tipo específico (Ex: Produtor Rural exige Inscrição Rural). Pode ser: Bloquear, Avisar ou Não Validar. | Geral (validação no botão Salvar). |

---

## 🛠️ Plano de Implementação Futura (Frontend)

Para aplicar estes parâmetros dinamicamente no **React (TSX)** e no **HTML**:

1. **Chamada de API:**
   Consumir um endpoint de configurações do sistema ao carregar a tela (ex: `/api/configuracoes-globais`) e armazenar os parâmetros no estado da aplicação (ou em um Contexto do React).
   ```typescript
   const [configuracoes, setConfiguracoes] = useState({
     usaCentroCusto: true,
     usaProjeto: true,
     disponibilizaEntidadeEmpresa: true,
     bloquearUsuarioTrocarConceito: false,
     // ... outros parâmetros
   });
   ```

2. **Condicionalização de Campos e Abas:**
   * **Exemplo de desabilitar aba no Ant Design:**
     ```typescript
     <Tabs.TabPane 
       tab="7. Empresa" 
       key="empresa" 
       disabled={!configuracoes.disponibilizaEntidadeEmpresa}
     >
     ```
   * **Exemplo de ocultar campo Centro de Custo:**
     ```typescript
     {configuracoes.usaCentroCusto && (
       <Form.Item label="Centro de Custo" name="idCentroCusto">
         <Select ... />
       </Form.Item>
     )}
     ```
