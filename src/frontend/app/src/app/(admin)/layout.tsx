'use client'

import React, { useEffect } from 'react'
import { Sidebar } from '@/components/layout/Sidebar'
import { Topbar } from '@/components/layout/Topbar'
import { useAuthStore } from '@/store/authStore'
import { useRouter } from 'next/navigation'

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode
}) {
  const isAuthenticated = useAuthStore((s) => s.isAuthenticated)
  const router = useRouter()

  useEffect(() => {
    if (!isAuthenticated) {
      router.push('/login')
    }
  }, [isAuthenticated, router])

  if (!isAuthenticated) {
    return null
  }

  return (
    <div className="min-h-screen bg-[#f8fafc] dark:bg-[#020617] transition-colors duration-500">
      <Sidebar />
      
      {/* Dynamic Margin to accommodate the large Sidebar */}
      <div className="pl-4 pr-4 lg:pl-36 lg:pr-6 pt-36 pb-12 transition-all duration-500 ease-in-out min-h-screen">
        <Topbar />
        
        <main className="max-w-full lg:max-w-7xl mx-auto mt-6">
          {children}
        </main>
      </div>

      {/* Decorative Blur Elements for the 'Airy/Premium' Feel */}
      <div className="fixed -z-10 top-0 right-0 h-[600px] w-[600px] bg-blue-600/5 blur-[150px] pointer-events-none" />
      <div className="fixed -z-10 bottom-0 left-0 h-[400px] w-[400px] bg-indigo-600/5 blur-[120px] pointer-events-none" />
    </div>
  )
}
