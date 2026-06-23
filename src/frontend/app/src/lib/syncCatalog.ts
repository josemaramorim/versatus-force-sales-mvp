import { db } from './offlineDb';
import { searchClientes, searchProdutos, getTabelasPreco, getCondicoesPagamento } from './vendaApi';

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

  // Se estiver offline, não tenta fazer o download
  if (!navigator.onLine) {
    console.warn('[Offline Sync] Dispositivo offline. Sincronização do catálogo abortada.');
    return false;
  }

  try {
    console.log('[Offline Sync] Iniciando sincronização do catálogo local...');
    
    // 1. Buscar dados atualizados da API em paralelo
    // Chamamos com limit de 100000 para trazer a listagem completa
    const [clientes, produtos, tabelasPreco, condicoes] = await Promise.all([
      searchClientes(undefined, 100000),
      searchProdutos(undefined, 100000),
      getTabelasPreco(),
      getCondicoesPagamento()
    ]);

    console.log(`[Offline Sync] Dados baixados. Clientes: ${clientes.length}, Produtos: ${produtos.length}, Tabelas Preço: ${tabelasPreco.length}, Condições Pagto: ${condicoes.length}`);

    // 2. Persistir no IndexedDB usando transação do Dexie para consistência
    await localDb.transaction('rw', [localDb.clientes, localDb.produtos, localDb.tabelasPreco, localDb.condicoesPagamento], async () => {
      // Limpa as tabelas antes de repopular para evitar lixo acumulado
      await localDb.clientes.clear();
      await localDb.produtos.clear();
      await localDb.tabelasPreco.clear();
      await localDb.condicoesPagamento.clear();

      if (clientes.length > 0) {
        await localDb.clientes.bulkPut(clientes);
      }
      if (produtos.length > 0) {
        await localDb.produtos.bulkPut(produtos);
      }
      if (tabelasPreco.length > 0) {
        await localDb.tabelasPreco.bulkPut(tabelasPreco);
      }
      if (condicoes.length > 0) {
        await localDb.condicoesPagamento.bulkPut(condicoes);
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
