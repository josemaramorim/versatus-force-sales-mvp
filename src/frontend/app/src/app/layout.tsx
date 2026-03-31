import type { Metadata } from 'next'
import { Inter } from 'next/font/google'
import { ThemeProvider } from 'next-themes'
import './globals.css'

const inter = Inter({ subsets: ['latin'] })

export const metadata: Metadata = {
  title: 'Versatus.Net - Login',
  description: 'Plataforma de força de vendas integrada ao ERP Versatus',
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
