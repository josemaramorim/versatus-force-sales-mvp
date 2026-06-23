import api from './api'
import { useAuthStore } from '@/store/authStore'

export interface LoginPayload {
  username: string
  password: string
}

export interface LoginResponse {
  accessToken: string
  refreshToken: string
  expiresAt: string
}

export async function login(payload: LoginPayload): Promise<LoginResponse> {
  const { data } = await api.post<LoginResponse>('/auth/login', {
    email: payload.username,
    password: payload.password
  })
  useAuthStore.getState().setSession(data.accessToken, data.refreshToken)
  return data
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
