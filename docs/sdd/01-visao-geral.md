# 1. Visão Geral e Escopo

## 1.1 Objetivo do Projeto
O sub-projeto **Versatus Force Sales MVP** visa modernizar o módulo legado "Small" do ERP Versatus (focado em força de vendas). Atualmente construído em WinForms de forma monolítica, offline-first e fortemente acoplado por `.NET Remoting`, o módulo atual será reescrito como uma aplicação Web independente, modular e cloud-native (SaaS), com interfaces web modernas (Airy/vibrante, focada em produtividade).

Este projeto é um pilar da estratégia de modernização geral do ecossistema Versatus.Net focado em uma abordagem de transição *Strangler Fig* onde o núcleo será extraído para um serviço próprio construído sobre .NET 8.

## 1.2 Contexto e Desafios Legados
O sistema "Small" atual (legado) opera no modelo:
1. **Desktop/WinForms**: Requer Windows instalado (não roda em aparelhos mobile / PWA).
2. **Sincronização Lenta e Complexa**: Funcional offline contínuo (Replicação de catálogo e sync de pedidos FTP/Compartilhamentos). Isso gera alta complexidade de integridade.
3. **Acoplamento**: Interações diretas por Remoting e métodos complexos como `VendaBase.GerarDocumentoVendaVersatus()`.

## 1.3 Escopo do Novo Force Sales MVP

O Novo Force Sales é uma aplicação "stand-alone" que resolve o problema de força de vendas isoladamente focada no representante de campo:

### O que FICA contido no MVP
* **Catálogo de Venda**: Clientes, Estoque e Tabelas de Preço.
* **Captura de Pedido (Order Management)**: Criação de cabeçalho, itens e parcelas com cálculo automático guiado de descontos/tabelas ativas de preço.
* **Envio para Faturamento**: Desacoplamento assíncrono pro-ERP. O pedido é emitido e aguarda conformidade (Documento Processado ou Erro) do backend legado via worker.
* **Multitenancy Básico**: Suporte a execução no formato SaaS (schema por tenant de banco).
* **Controle Simultâneo de Sessão**: Validação de número de seats simultâneos contratados pela empresa/tenant no Redis para garantir rentabilidade correta corporativa.

### O que FICA DE FORA (Responsabilidade do ERP Core)
* **Controle Físico Real de Estoque**: A redução e alocação exata ocorre no ERP após o processamento fiscal/documental. Erros de saldo estornado devem ser reportados ao front-end em um fluxo de recusa.
* **Cálculos Fiscais Extremamente Avançados**: A distribuição e tributação refinada continuará em `Base.Distribuicao` consumido pelo Worker Adapter para o legado local.
