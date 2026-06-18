import { db } from './offlineDb';
import api from './api';
import { useAuthStore } from '@/store/authStore';

let isSyncing = false;

export async function syncPendingOrders(): Promise<void> {
  const localDb = db;
  if (typeof window === 'undefined' || !localDb) return;

  // Evita concorrência e execuções paralelas simultâneas da fila
  if (isSyncing) {
    console.log('[Sync Queue] Sincronização já está em execução. Abortando nova chamada.');
    return;
  }

  // Verifica conexão ativa de internet
  if (!navigator.onLine) {
    console.log('[Sync Queue] Dispositivo offline. Sincronização automática pausada.');
    return;
  }

  // Verifica se o usuário está autenticado
  const token = useAuthStore.getState().accessToken;
  if (!token) {
    console.warn('[Sync Queue] Usuário não autenticado. Sincronização de pedidos abortada.');
    return;
  }

  // Se for modo demonstração, aborta a sincronização
  if (token === 'demo_token') {
    console.log('[Sync Queue] Modo demonstração ativo. Sincronização de pedidos abortada.');
    return;
  }

  try {
    isSyncing = true;
    console.log('[Sync Queue] Iniciando varredura de pedidos offline pendentes...');

    // 1. Obter todos os pedidos pendentes ou com erro de sync anterior
    const pendingOrders = await localDb.pedidos
      .filter((p) => p.status === 'pendente_sync' || p.status === 'erro_sync')
      .toArray();

    if (pendingOrders.length === 0) {
      console.log('[Sync Queue] Nenhum pedido offline pendente para sincronização.');
      return;
    }

    console.log(`[Sync Queue] Encontrados ${pendingOrders.length} pedido(s) aguardando envio.`);

    // 2. Iterar e tentar sincronizar cada pedido
    for (const order of pendingOrders) {
      try {
        console.log(`[Sync Queue] Enviando pedido local ${order.id} ao servidor...`);

        // Preparar payload conforme a API espera (sem campos extras do PWA)
        const payload = {
          clienteId: order.clienteId,
          itens: order.itens.map((i) => ({
            produtoId: i.produtoId,
            sku: i.sku,
            nome: i.nome,
            quantidade: i.quantidade,
            precoUnitario: i.precoUnitario,
            desconto: i.desconto,
          })),
          condicaoPagamento: {
            condicaoPagamentoId: order.condicaoPagamento.condicaoPagamentoId,
            primeiroVencimento: order.condicaoPagamento.primeiroVencimento,
            formaPagamento: order.condicaoPagamento.formaPagamento,
          },
          observacao: order.observacao,
        };

        // Envia para o endpoint de criação de pedidos
        const { data } = await api.post('/pedidos', payload);
        console.log(`[Sync Queue] Pedido local ${order.id} sincronizado com sucesso! ID no servidor: ${data.pedidoId}`);

        // Remove o pedido do banco de dados local após sincronização bem-sucedida,
        // para que a listagem passe a carregar a versão oficial da API do backend.
        await localDb.pedidos.delete(order.id);
      } catch (err: any) {
        console.error(`[Sync Queue] Falha ao sincronizar pedido local ${order.id}:`, err);

        // Se for erro de resposta do servidor (HTTP status 4xx ou 500 com regra de negócio)
        if (err.response) {
          const status = err.response.status;
          let errorMessage = 'Erro desconhecido na sincronização.';

          if (err.response.data && typeof err.response.data === 'object') {
            // Tenta obter os detalhes de validação ou a mensagem de erro principal
            errorMessage = err.response.data.detail || err.response.data.title || JSON.stringify(err.response.data);
          } else if (err.response.data && typeof err.response.data === 'string') {
            errorMessage = err.response.data;
          }

          // Se for erro de validação ou regra de negócio (ex: estoque indevido), marcamos com erro_sync
          // para que o vendedor tome conhecimento do erro na tela de listagem de pedidos
          await localDb.pedidos.update(order.id, {
            status: 'erro_sync',
            erroSyncDetail: `Erro ${status}: ${errorMessage}`
          });
        } else {
          // Se for erro de rede/conexão (Timeout ou CORS), mantemos como pendente_sync para retentar mais tarde
          console.warn(`[Sync Queue] Possível falha de rede/conexão física para o pedido ${order.id}. Ficará na fila.`);
          await localDb.pedidos.update(order.id, {
            status: 'pendente_sync',
            erroSyncDetail: 'Sem conexão de internet ou servidor indisponível'
          });
        }
      }
    }
  } finally {
    isSyncing = false;
    console.log('[Sync Queue] Finalizada execução da fila de sincronização.');
  }
}

// Configura os listeners de rede globais para reativação automática
export function setupSyncQueueListeners(): () => void {
  if (typeof window === 'undefined') return () => {};

  const handleOnline = () => {
    console.log('[Sync Queue Network Listener] Conexão detectada como online. Disparando fila...');
    syncPendingOrders().catch(console.error);
  };

  window.addEventListener('online', handleOnline);

  // Retorna uma função de limpeza para remover os listeners caso o componente desmonte
  return () => {
    window.removeEventListener('online', handleOnline);
  };
}
