import api from './api'
import { useAuthStore } from '@/store/authStore'

export interface LoginPayload {
  tenantId: string
  username: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export async function login(payload: LoginPayload): Promise<LoginResponse> {
  const { data } = await api.post<LoginResponse>('/auth/login', payload)
  useAuthStore.getState().setSession(data.accessToken, data.refreshToken)
  return data
}

export async function loginDemo() {
  // Mock login for UI preview
  useAuthStore.getState().setDemoSession()
  return true
}

export function logout() {
  useAuthStore.getState().logout()
  window.location.href = '/login'
}

export function getAccessToken(): string | null {
  return useAuthStore.getState().accessToken
}

export function isAuthenticated(): boolean {
  return useAuthStore.getState().isAuthenticated
}
