'use client'

import { useEffect } from 'react'
import { NextUIProvider } from '@nextui-org/react'
import { ThemeProvider as NextThemesProvider } from 'next-themes'
import { useRouter } from 'next/navigation'
import { setupSyncQueueListeners, syncPendingOrders } from '@/lib/syncQueue'

export function Providers({ children }: { children: React.ReactNode }) {
  const router = useRouter()

  useEffect(() => {
    if (typeof window !== 'undefined' && 'serviceWorker' in navigator) {
      navigator.serviceWorker.register('/sw.js')
        .then(reg => console.log('Service Worker registrado:', reg.scope))
        .catch(err => console.error('Erro ao registrar Service Worker:', err));
    }

    const cleanUp = setupSyncQueueListeners()
    syncPendingOrders().catch((err) => console.error('Erro ao sincronizar pedidos pendentes:', err))

    return () => {
      cleanUp()
    }
  }, []);

  return (
    <NextUIProvider navigate={router.push}>
      <NextThemesProvider 
        attribute="class" 
        defaultTheme="system" 
        enableSystem
        disableTransitionOnChange
      >
        {children}
      </NextThemesProvider>
    </NextUIProvider>
  )
}
