'use client'

import React, { useState } from 'react'
import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { 
  LayoutDashboard, 
  ShoppingCart, 
  ClipboardList, 
  Users, 
  Package, 
  Settings,
  Menu,
  ChevronLeft
} from 'lucide-react'
import { clsx } from 'clsx'
import { Tooltip, Button, Drawer, DrawerContent, DrawerBody, Modal, ModalContent, ModalBody } from '@nextui-org/react'
import { useUIStore } from '@/store/uiStore'

const menuItems = [
  { icon: LayoutDashboard, label: 'Dashboard', href: '/dashboard', color: 'text-blue-500' },
  { icon: ShoppingCart, label: 'Nova Venda', href: '/vendas/nova', color: 'text-indigo-500' },
  { icon: ClipboardList, label: 'Pedidos', href: '/pedidos', color: 'text-emerald-500' },
  { icon: Users, label: 'Clientes', href: '/clientes', color: 'text-purple-500' },
  { icon: Package, label: 'Produtos', href: '/produtos', color: 'text-amber-500' },
  { icon: Settings, label: 'Configurações', href: '/configuracoes', color: 'text-slate-500' },
]

export function Sidebar() {
  const [isHovered, setIsHovered] = useState(false)
  const pathname = usePathname()
  const { isMobileMenuOpen, setMobileMenuOpen } = useUIStore()

  const SidebarContent = ({ mobile = false }) => (
    <div className={clsx("flex flex-col h-full py-8", mobile ? "px-2" : "")}>
      {/* Logo Section */}
      <div className="px-6 mb-12 flex items-center gap-4 group">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-blue-600 to-indigo-700 text-white shadow-xl shadow-blue-500/20">
          <span className="text-xl font-black italic">V.</span>
        </div>
        <div className={clsx(
          "transition-opacity duration-300 overflow-hidden whitespace-nowrap",
          (isHovered || mobile) ? "opacity-100" : "opacity-0"
        )}>
          <p className="text-xl font-black italic tracking-tighter premium-title">Versatus</p>
          <p className="text-[8px] font-black uppercase tracking-[0.4em] text-slate-500 leading-none mt-1">Force Sales</p>
        </div>
      </div>

      {/* Navigation Items */}
      <nav className="flex-1 px-4 space-y-3">
        {menuItems.map((item) => {
          const isActive = pathname === item.href
          return (
            <Tooltip 
              key={item.href}
              content={item.label} 
              placement="right" 
              isDisabled={isHovered || mobile}
              color="primary"
              closeDelay={0}
            >
              <Link
                href={item.href}
                onClick={() => mobile && setMobileMenuOpen(false)}
                className={clsx(
                  "flex items-center gap-4 px-4 py-4 rounded-[2rem] transition-all duration-300 group relative",
                  isActive 
                    ? "bg-blue-600 text-white shadow-xl shadow-blue-600/20" 
                    : "text-slate-500 hover:bg-slate-50 dark:hover:bg-slate-800/50 hover:text-slate-900 dark:hover:text-white"
                )}
              >
                <div className={clsx(
                  "flex h-6 w-6 shrink-0 items-center justify-center transition-transform group-hover:scale-110",
                  isActive ? "text-white" : item.color
                )}>
                  <item.icon size={24} strokeWidth={isActive ? 2.5 : 2} />
                </div>
                
                <span className={clsx(
                  "font-black uppercase tracking-[0.2em] text-[10px] whitespace-nowrap transition-all duration-500 ease-in-out italic",
                  (isHovered || mobile) ? "opacity-100 translate-x-0" : "opacity-0 -translate-x-4 h-0 w-0"
                )}>
                  {item.label}
                </span>

                {isActive && !isHovered && !mobile && (
                  <div className="absolute -left-1 top-1/2 -translate-y-1/2 w-1.5 h-6 bg-white rounded-full" />
                )}
              </Link>
            </Tooltip>
          )
        })}
      </nav>

      {/* Bottom Section */}
      <div className="px-4">
        <div className={clsx(
          "p-6 rounded-[2.5rem] bg-slate-950/40 border border-slate-900 transition-all duration-500",
          (isHovered || mobile) ? "opacity-100 scale-100" : "opacity-0 scale-90"
        )}>
          <p className="text-[9px] font-black text-slate-600 uppercase tracking-widest leading-none mb-3 italic">Ajuda & Suporte</p>
          <Button 
              fullWidth 
              size="sm" 
              variant="flat" 
              className="bg-blue-600/10 text-blue-500 font-bold text-[10px] uppercase tracking-widest rounded-2xl h-10"
          >
              Abrir Ticket
          </Button>
        </div>
      </div>
    </div>
  )

  return (
    <>
      {/* Mobile Drawer */}
      <Drawer 
        isOpen={isMobileMenuOpen} 
        onOpenChange={setMobileMenuOpen}
        placement="left"
        size="xs"
        classNames={{
          wrapper: "bg-slate-950/20 backdrop-blur-sm",
          base: "bg-slate-950 border-r border-slate-900 max-w-[280px]",
          closeButton: "top-8 right-6 text-white bg-slate-900 hover:bg-slate-800 z-50"
        }}
      >
        <DrawerContent>
          <DrawerBody className="p-0 overflow-hidden">
            <SidebarContent mobile />
          </DrawerBody>
        </DrawerContent>
      </Drawer>

      {/* Desktop Sidebar */}
      <aside 
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        className={clsx(
          "fixed left-6 top-6 bottom-6 z-50 transition-all duration-500 ease-in-out",
          "premium-card overflow-hidden hidden lg:block",
          isHovered ? "w-72" : "w-24"
        )}
      >
        <SidebarContent />
      </aside>
    </>
  )
}
