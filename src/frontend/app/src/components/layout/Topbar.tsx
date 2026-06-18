'use client'

import React from 'react'
import { 
  Bell, 
  Search, 
  User as UserIcon, 
  ChevronDown, 
  Moon, 
  Sun,
  ShieldCheck,
  Zap,
  Power,
  Settings,
  Menu
} from 'lucide-react'
import { useUIStore } from '@/store/uiStore'
import { 
  Dropdown, 
  DropdownTrigger, 
  DropdownMenu, 
  DropdownItem, 
  Button, 
  User, 
  Badge,
  Input
} from '@nextui-org/react'
import { useAuthStore } from '@/store/authStore'
import { logout } from '@/lib/auth'
import { useTheme } from 'next-themes'
import { db } from '@/lib/offlineDb'

export function Topbar() {
  const user = useAuthStore((s) => s.user)
  const { theme, setTheme, resolvedTheme } = useTheme()
  const { toggleMobileMenu } = useUIStore()
  const [mounted, setMounted] = React.useState(false)
  const [isOnline, setIsOnline] = React.useState(true)
  const [pendingCount, setPendingCount] = React.useState(0)

  React.useEffect(() => {
    setMounted(true)
    setIsOnline(navigator.onLine)

    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)

    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)

    const checkPending = async () => {
      if (typeof window !== 'undefined' && db) {
        try {
          const count = await db.pedidos
            .filter((p) => p.status === 'pendente_sync' || p.status === 'erro_sync')
            .count()
          setPendingCount(count)
        } catch (err) {
          console.error('[Topbar] Erro ao contar pedidos pendentes:', err)
        }
      }
    }

    checkPending()
    const interval = setInterval(checkPending, 3000)

    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
      clearInterval(interval)
    }
  }, [])

  const toggleTheme = () => {
    setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')
  }

  return (
    <header className="fixed top-6 right-6 left-4 lg:left-36 z-40 flex items-center justify-between px-4 lg:px-10 py-6 premium-card h-24 transition-all duration-500">
      
      <div className="flex items-center gap-4 lg:gap-10 flex-1">
        {/* Mobile Hamburger */}
        <Button 
          isIconOnly
          variant="flat"
          radius="lg"
          onPress={() => toggleMobileMenu()}
          className="lg:hidden h-12 w-12 bg-slate-950/40 border border-slate-900 text-slate-400 hover:text-blue-500 transition-all flex shrink-0"
        >
          <Menu size={24} />
        </Button>

        <div className="flex items-center gap-3 lg:gap-6">
          <div className="hidden sm:flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-950/40 border border-slate-900 shadow-inner group cursor-pointer hover:border-blue-500 transition-all shrink-0">
            <Zap className="h-5 w-5 text-blue-500 group-hover:scale-110 transition-transform" />
          </div>
          <div className="min-w-0">
            <h2 className="text-sm lg:text-xl font-black italic tracking-tighter leading-none truncate pr-2">Módulo de Venda</h2>
            <div className="hidden xs:flex items-center gap-2 mt-2">
              {isOnline ? (
                <>
                  <span className="relative flex h-1.5 w-1.5 shrink-0">
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                    <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-emerald-500"></span>
                  </span>
                  <span className="text-[8px] font-black uppercase tracking-[0.4em] text-emerald-500 leading-none">Online</span>
                </>
              ) : (
                <>
                  <span className="relative flex h-1.5 w-1.5 shrink-0">
                    <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75"></span>
                    <span className="relative inline-flex rounded-full h-1.5 w-1.5 bg-amber-500"></span>
                  </span>
                  <span className="text-[8px] font-black uppercase tracking-[0.4em] text-amber-500 leading-none">Offline</span>
                </>
              )}

              {pendingCount > 0 && (
                <>
                  <span className="w-1 h-1 rounded-full bg-slate-700" />
                  <span className="text-[8px] font-black uppercase tracking-wider text-amber-500 bg-amber-500/10 px-1.5 py-0.5 rounded-full animate-pulse">
                    {pendingCount} Pendente{pendingCount > 1 ? 's' : ''}
                  </span>
                </>
              )}

              <span className="w-1 h-1 rounded-full bg-slate-700" />
              <span className="text-[8px] font-black uppercase tracking-[0.4em] text-slate-500 leading-none truncate">Filial: São Paulo</span>
            </div>
          </div>
        </div>

        <div className="hidden md:flex max-w-md w-full ml-4 lg:ml-10">
          <Input
            isClearable
            radius="lg"
            placeholder="Pesquisa rápida..."
            startContent={<Search className="text-slate-400 h-4 w-4" />}
            variant="flat"
            classNames={{
              input: "text-xs font-bold",
              inputWrapper: "h-12 bg-slate-950/40 border border-slate-900 group-hover:border-blue-500 transition-all shadow-inner",
            }}
          />
        </div>
      </div>

      <div className="flex items-center gap-2 lg:gap-6 shrink-0">
        
        {/* Theme Toggle Toggle */}
        <Button 
          isIconOnly
          variant="flat"
          radius="lg"
          onPress={toggleTheme}
          className="h-10 w-10 lg:h-12 lg:w-12 bg-slate-950/40 border border-slate-900 text-slate-400 hover:text-blue-500 hover:border-blue-500 transition-all"
        >
          {mounted && (resolvedTheme === 'dark' ? <Sun size={20} className="text-amber-500" /> : <Moon size={20} className="text-blue-500" />)}
        </Button>

        {/* Notifications */}
        <Badge content="3" color="danger" shape="circle" size="sm" className="font-bold border-2 border-slate-900 hidden sm:flex">
           <Button 
            isIconOnly
            variant="flat"
            radius="lg"
            className="h-10 w-10 lg:h-12 lg:w-12 bg-slate-950/40 border border-slate-900 text-slate-400 hover:text-blue-500 hover:border-blue-500 transition-all"
           >
            <Bell size={20} />
          </Button>
        </Badge>
        
        <div className="hidden lg:block h-8 w-px bg-slate-800 mx-2" />

        {/* User Profile */}
        <Dropdown placement="bottom-end">
          <DropdownTrigger>
            <div className="flex items-center gap-2 lg:gap-4 cursor-pointer p-1 lg:p-2 rounded-[2rem] hover:bg-slate-950/50 transition-all group overflow-hidden">
              <User
                name={user?.username ?? 'Vendedor'}
                description={
                  <span className="hidden sm:block text-[9px] font-black text-blue-500 uppercase tracking-widest italic group-hover:text-blue-400">
                    Master Admin
                  </span>
                }
                avatarProps={{
                  className: "bg-gradient-to-br from-blue-600 to-indigo-700 text-white font-black shrink-0",
                  radius: "full",
                  size: "sm",
                  name: user?.username?.charAt(0) ?? 'V',
                  isBordered: true,
                }}
                classNames={{
                  name: "hidden lg:block text-sm font-black italic tracking-tighter truncate",
                }}
              />
              <ChevronDown className="h-4 w-4 text-slate-500 group-hover:text-blue-500 transition-colors shrink-0" />
            </div>
          </DropdownTrigger>
          <DropdownMenu 
            aria-label="Ações do perfil" 
            className="p-3 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 rounded-[2rem] shadow-2xl" 
            variant="flat"
          >
            <DropdownItem 
                key="profile" 
                className="h-14 gap-2 rounded-2xl font-bold dark:text-white" 
                startContent={<ShieldCheck className="text-blue-500 h-5 w-5" />}
            >
              Meu Perfil
            </DropdownItem>
            <DropdownItem 
                key="settings" 
                className="h-14 gap-2 rounded-2xl font-bold dark:text-white" 
                startContent={<Settings size={20} className="text-blue-500" />}
            >
              Configurações
            </DropdownItem>
            <DropdownItem 
                key="logout" 
                color="danger" 
                className="h-14 gap-2 text-danger rounded-2xl font-black italic uppercase tracking-wider" 
                startContent={<Power size={20} />}
                onPress={() => logout()}
            >
              Sair do Sistema
            </DropdownItem>
          </DropdownMenu>
        </Dropdown>
      </div>
    </header>
  )
}
