import { db } from './offlineDb';
import { searchClientes, searchProdutos } from './vendaApi';
import { useAuthStore } from '@/store/authStore';

export interface SyncStatus {
  lastSyncedAt: string | null;
  status: 'idle' | 'syncing' | 'success' | 'error';
  errorMessage: string | null;
}

export async function syncCatalogLocal(): Promise<boolean> {
  const localDb = db;
  if (typeof window === 'undefined' || !localDb) {
    return false;
  }

  // Se for modo demonstração, não sincroniza com a API
  const token = useAuthStore.getState().accessToken;
  if (token === 'demo_token') {
    console.log('[Offline Sync] Modo demonstração ativo. Ignorando sincronização com a API.');
    return true;
  }

  // Se estiver offline, não tenta fazer o download
  if (!navigator.onLine) {
    console.warn('[Offline Sync] Dispositivo offline. Sincronização do catálogo abortada.');
    return false;
  }

  try {
    console.log('[Offline Sync] Iniciando sincronização do catálogo local...');
    
    // 1. Buscar dados atualizados da API
    // Chamamos sem filtro de busca para trazer a listagem completa
    const [clientes, produtos] = await Promise.all([
      searchClientes(),
      searchProdutos(),
    ]);

    console.log(`[Offline Sync] Dados baixados. Clientes: ${clientes.length}, Produtos: ${produtos.length}`);

    // 2. Persistir no IndexedDB usando transação do Dexie para consistência
    await localDb.transaction('rw', [localDb.clientes, localDb.produtos], async () => {
      // Limpa as tabelas antes de repopular para evitar lixo acumulado
      await localDb.clientes.clear();
      await localDb.produtos.clear();

      if (clientes.length > 0) {
        await localDb.clientes.bulkPut(clientes);
      }
      if (produtos.length > 0) {
        await localDb.produtos.bulkPut(produtos);
      }
    });

    // Guardar carimbo da última sincronização
    const now = new Date().toISOString();
    localStorage.setItem('versatus_last_sync_catalog', now);
    console.log('[Offline Sync] Sincronização do catálogo concluída com sucesso!');
    return true;
  } catch (error) {
    console.error('[Offline Sync] Erro durante a sincronização do catálogo:', error);
    return false;
  }
}

export function getLastSyncTime(): string | null {
  if (typeof window === 'undefined') return null;
  return localStorage.getItem('versatus_last_sync_catalog');
}
