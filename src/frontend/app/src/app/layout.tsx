import type { Metadata } from 'next'
import { Inter } from 'next/font/google'
import { ThemeProvider } from 'next-themes'
import './globals.css'

const inter = Inter({ subsets: ['latin'] })

export const metadata: Metadata = {
  title: 'Versatus Go',
  description: 'Plataforma de força de vendas mobile integrada ao ERP Versatus',
  manifest: '/manifest.json',
}

import { Providers } from './providers'

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="pt-BR" suppressHydrationWarning>
      <body className={`${inter.className} anti-aliased`}>
        <Providers>
          {children}
        </Providers>
      </body>
    </html>
  )
}
