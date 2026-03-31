import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthUser {
  userId: string
  username: string
  tenantId: string
}

interface AuthState {
  accessToken: string | null
  refreshToken: string | null
  user: AuthUser | null
  isAuthenticated: boolean
  setSession: (accessToken: string, refreshToken: string) => void
  setDemoSession: () => void
  logout: () => void
}

function parseJwt(token: string): AuthUser | null {
  try {
    const payload = JSON.parse(atob(token.split('.')[1]))
    return {
      userId: payload.sub ?? '',
      username: payload.unique_name ?? payload.name ?? '',
      tenantId: payload.tenant_id ?? '',
    }
  } catch {
    return null
  }
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      isAuthenticated: false,

      setSession: (accessToken, refreshToken) => {
        const user = parseJwt(accessToken)
        set({ accessToken, refreshToken, user, isAuthenticated: true })
      },

      setDemoSession: () => {
        set({ 
          accessToken: 'demo_token', 
          refreshToken: 'demo_refresh', 
          user: { userId: '1', username: 'Vendedor Demo', tenantId: 'versatus-demo' }, 
          isAuthenticated: true 
        })
      },

      logout: () => {
        set({ accessToken: null, refreshToken: null, user: null, isAuthenticated: false })
      },
    }),
    {
      name: 'versatus-auth',
      partialize: (state) => ({
        accessToken: state.accessToken,
        refreshToken: state.refreshToken,
        user: state.user,
        isAuthenticated: state.isAuthenticated,
      }),
    }
  )
)
