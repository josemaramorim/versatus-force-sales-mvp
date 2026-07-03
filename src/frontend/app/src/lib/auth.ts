import api from './api'
import { useAuthStore } from '@/store/authStore'
import { db } from './offlineDb'

export interface LoginPayload {
  username: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export async function clearOfflineDatabase(): Promise<void> {
  const localDb = db
  if (typeof window !== 'undefined' && localDb) {
    try {
      console.log('[Offline DB] Limpando dados locais para segurança multi-tenant...');
      await localDb.transaction('rw', [
        localDb.clientes,
        localDb.produtos,
        localDb.tabelasPreco,
        localDb.tabelasPrecoMetadata,
        localDb.tenantParameters,
        localDb.condicoesPagamento,
        localDb.pedidos
      ], async () => {
        await localDb.clientes.clear();
        await localDb.produtos.clear();
        await localDb.tabelasPreco.clear();
        await localDb.tabelasPrecoMetadata.clear();
        await localDb.tenantParameters.clear();
        await localDb.condicoesPagamento.clear();
        await localDb.pedidos.clear();
      });
      console.log('[Offline DB] Limpeza concluída.');
    } catch (err) {
      console.error('[Offline DB] Erro ao limpar base local:', err);
    }
  }
  if (typeof window !== 'undefined') {
    localStorage.removeItem('versatus_last_sync_catalog');
  }
}

export async function login(payload: LoginPayload): Promise<LoginResponse> {
  // Limpa banco local antes de efetuar novo login
  await clearOfflineDatabase()

  const { data } = await api.post<LoginResponse>('/auth/login', {
    email: payload.username,
    password: payload.password
  })
  useAuthStore.getState().setSession(data.accessToken, data.refreshToken)
  return data
}

export async function logout(): Promise<void> {
  // Limpa banco local ao efetuar logout manual
  await clearOfflineDatabase()
  useAuthStore.getState().logout()
  window.location.href = '/login'
}

export function getAccessToken(): string | null {
  return useAuthStore.getState().accessToken
}

export function isAuthenticated(): boolean {
  return useAuthStore.getState().isAuthenticated
}
