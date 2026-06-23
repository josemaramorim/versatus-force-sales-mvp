# Ciclo de Vida e Integração de Pedidos

Este documento descreve o fluxo completo de sincronização de pedidos de venda no sistema **Versatus Force Sales**, explicando o significado de cada status (Rascunho, Enviado, Processado e Rejeitado) e os responsáveis técnicos/operacionais por cada transição.

---

## 1. Mapeamento Visual do Fluxo

```
[ App de Vendas (Vendedor) ]
             │
             ▼ (Salvo localmente)
    [ Rascunho / Offline ]
             │
             ▼ (Detecta internet / Envia)
  [ API Gateway (PostgreSQL) ]  ───(Define status)───> [ Enviado ]
             │
             ▼ (Upload de arquivo JSON)
     [ Servidor FTP ]
             │
             ▼ (Download e inserção no banco)
   [ ERP Local (SQL Server) ]   ───(Tabela MOBVENDA: PROCESSADA = 0)
             │
             ▼ (Faturamento físico da Nota Fiscal / Faturista)
   [ ERP Fatura a Venda ]       ───(Tabela MOBVENDA: PROCESSADA = 1, IDVENDOCUMENTO = XXX)
             │
             ▼ (Leitura e upload do resultado)
     [ Servidor FTP ]
             │
             ▼ (Leitura do resultado)
  [ API Gateway (PostgreSQL) ]  ───(Define status)───> [ Processado ]
```

---

## 2. Descrição Detalhada dos Status

### 2.1. Rascunho (ou Aguardando Rede)
*   **Onde fica:** Apenas no dispositivo físico do vendedor (IndexedDB do navegador/celular).
*   **O que significa:** O vendedor criou e salvou o pedido, mas o dispositivo está offline ou ainda não transmitiu a informação para a nuvem.
*   **Responsabilidade:** Aplicativo móvel (Frontend PWA) e fila de sincronização local (`syncQueue.ts`).

### 2.2. Enviado
*   **Onde fica:** Banco de dados PostgreSQL da nuvem e arquivos JSON temporários no FTP.
*   **O que significa:** O pedido foi transmitido com sucesso à API central da nuvem e colocado na fila do FTP. O integrador **ErpAdapter** já baixou o arquivo e inseriu o cabeçalho, itens e parcelas nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA` do banco de dados do ERP.
*   **Responsabilidade:** API da nuvem (recebimento) e `ErpAdapter` (gravação nas tabelas do ERP legado).
*   **Nota Importante:** O pedido permanece como **"Enviado"** até que o faturamento físico seja realizado pela empresa na retaguarda.

### 2.3. Processado (Faturado)
*   **Onde fica:** Gravado permanentemente no banco PostgreSQL e atualizado no app do vendedor.
*   **O que significa:** O setor de faturamento (ou sistema automatizado) do ERP Versatus faturou a venda, emitiu a Nota Fiscal, gravou o número do documento fiscal (`IDVENDOCUMENTO`) e atualizou a flag de controle no banco de dados (`PROCESSADA = 1`). O `ErpAdapter` identificou essa finalização e notificou o sistema principal.
*   **Responsabilidade:** Setor de Faturamento/Expedição da Empresa (dentro do ERP Versatus legado) e posterior sincronização automática pelo `ErpAdapter`.

### 2.4. Rejeitado ERP (Erro de Integração/Estoque)
*   **O que significa:** Ocorreu alguma rejeição física ou lógica no ERP ao tentar inserir o pedido (como falta de estoque real no momento da importação estrita, cliente bloqueado por análise de crédito ou erros de regras tributárias).
*   **Responsabilidade:** ERP Versatus/ErpAdapter. O erro retorna com o detalhe do motivo do bloqueio para o vendedor.

---

## 3. Matriz de Responsabilidades

| Etapa / Transição | Responsável | Ação Realizada |
|---|---|---|
| **Rascunho ➔ Enviado** | **Aplicativo & API** | Envia o JSON do pedido do celular do vendedor para a API na nuvem e o grava na fila do FTP. |
| **Enviado ➔ Inserção no ERP** | **ErpAdapter** (Serviço Local) | Baixa o JSON do FTP e faz o insert nas tabelas `MOBVENDA`, `MOBVENDAITEM` e `MOBVENDAPARCELA`. |
| **Faturamento/Processamento** | **Faturista da Empresa / ERP** | Analisa o pedido importado, emite a NF-e e atualiza a linha para `PROCESSADA = 1`. |
| **Enviado ➔ Processado** | **ErpAdapter & Worker** | O adaptador lê `PROCESSADA = 1`, envia arquivo de resultado para o FTP e o Worker atualiza o banco de dados PostgreSQL. |

---

## 4. Como Simular/Testar o Processamento Manualmente (Ambiente Dev)

Para simular o faturamento de um pedido em ambiente de desenvolvimento sem precisar abrir a tela oficial do ERP legado:

1.  Acesse a instância do SQL Server do ERP (`DESKTOP-PA7RCSD\SQLEXPRESS2008` ou conforme `ConnectionStrings:ErpDatabase`).
2.  Abra o banco de dados `versatus`.
3.  Localize a tabela `MOBVENDA` e filtre pelo ID do pedido (gravado na coluna `IDMOBVENDA` ou `CHAVEINTEGRACAO`):
    ```sql
    SELECT * FROM MOBVENDA WHERE IDMOBVENDA = '7AC2525E-XXXX-XXXX-XXXX-XXXXXXXXXXXX';
    ```
4.  Execute um comando `UPDATE` para simular que o faturamento do ERP concluiu a emissão da Nota Fiscal:
    ```sql
    UPDATE MOBVENDA 
    SET PROCESSADA = 1, 
        IDVENDOCUMENTO = 987654 -- Número fictício da NF-e gerada
    WHERE IDMOBVENDA = '7AC2525E-XXXX-XXXX-XXXX-XXXXXXXXXXXX';
    ```
5.  Em até **10 segundos** (tempo do intervalo padrão do `OrderImportIntervalSeconds`), o **`ErpAdapter`** detectará a linha atualizada, gerará a confirmação no FTP e o **`Worker`** atualizará o status do pedido na API. 
6.  Ao recarregar o aplicativo Force Sales, o vendedor verá o pedido atualizado com a etiqueta **"Processado"**.
