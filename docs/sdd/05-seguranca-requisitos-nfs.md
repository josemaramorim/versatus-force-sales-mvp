# 5. Segurança, NFRs e Requisitos de Negócio

## 5.1 Requisitos de Segurança
* **Protocolo**: Todas as interfaces Web devem expor comunicações apenas em tráfego protegido `HTTPS`.
* **Identidade Principal**: Os endpoints API serão fechados com filtros (Autorization API) recebendo **Bearer Tokens (JWT)**.
* **Múltiples Sessões Limitadas**: Regra SaaS comercial validada no momento da login, verificando limite fixo do Tenant em Redis cache-control. Se ultrapassado o Limite + Permitted Overflow Margin, rejeita autenticação informando o fim do escopo do plano contratado ("Plan Upgrades").

## 5.2 Estética e Usabilidade Premium (Design System)
Uma premissa central é entregar aplicações com aparência estritamente refinada e responsiva. Aplicações Desktop antigas eram puramente voltadas aos "campos numéricos". A interface deste App web foca:
* Experiências Premium de App ("Airy", glassmorphism, cores refinadas HSL e animações fluidas Framer Motion / next micro-interactions).
* Layout fluído para interatividade de Tablet / Smartphone de Vendedor a Pronta-Entrega (Mobile-First approach, mas responsivo pra Desktop/Supervisão PWA).

## 5.3 Offline Parcial e Progresso Web App (PWA)
Para dar suporte às equipes que vão presencialmente a galpões frigoríficos sem sinal 4G, a UI Frontal React é construída sob Service Workers `next-pwa`.
* Armazena cópias das parcelas e itens no cache Client-side local `IndexedDB (Dexie.js)`.
* Criação offline funciona engavetando fluxos em rascunhos assíncronos a serem disparados na retoma de internet com API e reconciliação dos valores propostos de Tabelas de Preço. (Roadmap Phase 1)

## 5.4 Performance e Observabilidade
* **Tempo de Resposta em Consulta de Tabela de Preço**: O Redis deve manter hit-miss baixíssimo. Limiar alvo (SLA) de tempo de reposta para consultas ativas de cliente/produto < 200 ms. 
* **Worker Queue Tolerance**: Em caso de offline acidental do Servidor do Cliente/WorkerAdapter, a mensageria na nuvem (Rabbit/SQS) fará dead-letter queue / retry control para não perder os leads fechados naquele dia.
* **OpenTelemetry**: Os serviços .NET 8 e Next vão emitir rastreabilidade nativa para painéis de DevOps (Kibana, Seq, Grafana), unificando ID de Rastreio para visualizar o caminho de ponta-a-ponta (do clique na tela web até o banco SQL Server da nuvem do Cliente corporativo).
