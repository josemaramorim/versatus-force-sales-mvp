'use client'

import React from 'react'
import { useAuthStore } from '@/store/authStore'
import {
  ShoppingCart,
  TrendingUp,
  Users,
  RefreshCw,
  ArrowUpRight,
  ChevronRight,
  TrendingDown,
  Calendar,
  Zap,
  Power
} from 'lucide-react'
import { 
  Button, 
  Chip, 
  Progress,
  Avatar,
  Card,
  CardBody,
  Divider,
  Tooltip
} from '@nextui-org/react'
import { clsx } from 'clsx'

const kpis = [
  {
    label: 'Pedidos Hoje',
    value: '12',
    delta: '+3 vs ontem',
    trend: 'up',
    icon: ShoppingCart,
    color: 'bg-blue-600',
    shadow: 'shadow-blue-500/20'
  },
  {
    label: 'Ticket Médio',
    value: 'R$ 847,50',
    delta: '+12% semana',
    trend: 'up',
    icon: TrendingUp,
    color: 'bg-emerald-600',
    shadow: 'shadow-emerald-500/20'
  },
  {
    label: 'Clientes Ativos',
    value: '34',
    delta: '2 novos hoje',
    trend: 'up',
    icon: Users,
    color: 'bg-indigo-600',
    shadow: 'shadow-indigo-500/20'
  },
  {
    label: 'Status ERP',
    value: 'Sincronizado',
    delta: 'Última sync: 14:30',
    trend: 'neutral',
    icon: RefreshCw,
    color: 'bg-amber-600',
    shadow: 'shadow-amber-500/20'
  },
]

const recentOrders = [
  { id: '#00124', client: 'Supermercado Bom Preço', value: 'R$ 1.240,00', status: 'Enviado', type: 'success' },
  { id: '#00123', client: 'Atacado Expresso Ltda', value: 'R$ 3.870,50', status: 'Processado', type: 'primary' },
  { id: '#00122', client: 'Mercearia São João', value: 'R$ 560,00', status: 'Rascunho', type: 'default' },
  { id: '#00121', client: 'Distribuidora Norte Sul', value: 'R$ 8.200,00', status: 'Processado', type: 'primary' },
]

export default function DashboardPage() {
  const user = useAuthStore((s) => s.user)

  return (
    <div className="space-y-12">
      
      {/* Page Header */}
      <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-center gap-6">
          <Avatar 
             src={undefined}
             name={user?.username?.charAt(0) ?? 'V'}
             className="w-16 h-16 text-2xl font-black bg-gradient-to-br from-blue-600 to-indigo-700 text-white shadow-2xl border-4 border-white dark:border-slate-800"
             isBordered
          />
          <div>
            <div className="flex items-center gap-3">
              <h1 className="text-4xl premium-title sm:text-5xl">Olá, {user?.username ?? 'Vendedor'}</h1>
              <span className="text-4xl">👋</span>
            </div>
            <div className="flex items-center gap-2 mt-4">
              <Calendar className="h-4 w-4 text-blue-500" />
              <p className="premium-label tracking-[0.4em]">
                Hoje: {new Date().toLocaleDateString('pt-BR', { day: 'numeric', month: 'long' })}
              </p>
            </div>
          </div>
        </div>
        
        <Button
          as="a"
          href="/vendas/nova"
          color="primary"
          radius="full"
          className="h-16 px-10 font-black uppercase tracking-[0.2em] text-xs shadow-2xl shadow-blue-500/40 bg-blue-600 transition-all hover:scale-105 active:scale-95 italic"
          startContent={<Plus className="h-5 w-5" />}
        >
          Iniciar Nova Venda
        </Button>
      </div>

      {/* KPI Section */}
      <div className="grid grid-cols-1 gap-8 sm:grid-cols-2 lg:grid-cols-4">
        {kpis.map((kpi) => (
          <Card key={kpi.label} className="premium-card p-2" radius="none" shadow="none">
            <CardBody className="p-8 space-y-6">
              <div className="flex justify-between items-start">
                <div className={clsx("p-4 rounded-[1.5rem] text-white shadow-xl", kpi.color, kpi.shadow)}>
                   <kpi.icon className="h-6 w-6" />
                </div>
                {kpi.trend === 'up' && (
                  <Chip color="success" variant="flat" size="sm" startContent={<ArrowUpRight className="h-4 w-4" />} className="font-black italic">ALTA</Chip>
                )}
              </div>
              <div className="space-y-1">
                <p className="premium-label">{kpi.label}</p>
                <h2 className="text-3xl font-black tracking-tighter sm:text-4xl">{kpi.value}</h2>
              </div>
              <p className="text-xs font-bold text-slate-400 dark:text-slate-500 italic mt-4">{kpi.delta}</p>
            </CardBody>
          </Card>
        ))}
      </div>

      {/* Dashboard Main Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-10">
        
        {/* Table Column */}
        <div className="lg:col-span-2 space-y-8">
          <div className="flex items-center justify-between px-4">
               <h3 className="text-xl premium-title flex items-center gap-3">
                  <span className="w-1.5 h-8 bg-blue-600 rounded-full" />
                  Listagem de Pedidos
               </h3>
               <Button as="a" href="/pedidos" variant="light" color="primary" size="sm" className="font-black uppercase tracking-widest text-[10px] bg-slate-950/20 px-4 h-10 rounded-2xl">
                 Ver Histórico <ChevronRight className="h-4 w-4" />
               </Button>
          </div>

          <table className="premium-table">
             <thead className="premium-label opacity-60">
                <tr>
                   <th className="px-8 pb-4">ID / Referência</th>
                   <th className="px-8 pb-4">Cliente</th>
                   <th className="px-8 pb-4 text-right">Valor</th>
                   <th className="px-8 pb-4 text-center">Status</th>
                </tr>
             </thead>
             <tbody>
                {recentOrders.map((order) => (
                  <tr key={order.id} className="cursor-pointer group">
                     <td className="px-8 py-8">
                        <span className="text-sm font-black font-mono text-blue-500">{order.id}</span>
                        <p className="text-[9px] font-black uppercase text-slate-500 mt-1 tracking-widest">Pedido Interno</p>
                     </td>
                     <td className="px-8 py-8">
                        <span className="text-base font-black text-slate-900 dark:text-slate-300 italic">{order.client}</span>
                     </td>
                     <td className="px-8 py-8 text-right">
                        <span className="text-xl font-black font-mono text-slate-900 dark:text-white tracking-tighter">{order.value}</span>
                     </td>
                     <td className="px-8 py-8 text-center">
                        <Chip className="capitalize font-black text-[9px] tracking-widest px-4 h-6" color={order.type as any} size="sm" variant="shadow">
                          {order.status}
                        </Chip>
                     </td>
                  </tr>
                ))}
             </tbody>
          </table>
        </div>

        {/* Sidebar Info Column */}
        <div className="space-y-10">
          
          {/* Sync Card */}
          <div className="bg-[#020617] border border-blue-900/30 p-10 rounded-[3rem] shadow-2xl relative overflow-hidden group">
            <div className="absolute -right-10 -top-10 h-40 w-40 bg-blue-600/10 blur-[60px] group-hover:bg-blue-600/20 transition-all" />
            
            <div className="relative space-y-8">
              <div className="flex justify-between items-center">
                 <div className="p-4 bg-blue-600/10 rounded-2xl border border-blue-500/20">
                    <RefreshCw className="h-6 w-6 text-blue-500 animate-[spin_5s_linear_infinite]" />
                 </div>
                 <div className="px-4 py-2 bg-emerald-500/10 border border-emerald-500/20 rounded-full flex items-center gap-2">
                    <div className="h-2 w-2 bg-emerald-500 rounded-full shadow-[0_0_8px_rgba(16,185,129,0.8)]" />
                    <span className="text-[10px] font-black uppercase tracking-widest text-emerald-500">Sincronizado</span>
                 </div>
              </div>
              <div className="space-y-3">
                 <h4 className="text-2xl font-black italic tracking-tighter text-white">ERP Versatus.Net</h4>
                 <p className="text-xs font-bold text-slate-500 leading-relaxed italic">
                   Status em tempo real ativo. Última atualização de estoque e preços realizada às 14:30.
                 </p>
              </div>
              
              <div className="space-y-4">
                 <div className="flex justify-between text-[10px] font-black uppercase tracking-widest text-slate-600 italic">
                    <span>Performance Sinc</span>
                    <span className="text-blue-500">98%</span>
                 </div>
                 <Progress 
                    size="sm" 
                    value={98} 
                    radius="full" 
                    classNames={{
                      indicator: "bg-blue-600 shadow-[0_0_12px_rgba(37,99,235,0.5)]",
                      track: "bg-slate-900 border border-slate-800"
                    }}
                 />
              </div>

              <Button fullWidth variant="flat" className="h-14 font-black uppercase tracking-widest text-[10px] bg-blue-600/10 border border-blue-500/10 rounded-[1.5rem] group-hover:bg-blue-600 transition-colors group-hover:text-white">
                 Forçar Recarga Completa
              </Button>
            </div>
          </div>

          <div className="premium-card-inner p-10 space-y-6">
             <div className="flex items-center gap-3">
               <div className="h-10 w-10 bg-indigo-600/10 text-indigo-500 rounded-xl flex items-center justify-center border border-indigo-500/20">
                  <TrendingUp className="h-5 w-5" />
               </div>
               <h5 className="text-[10px] font-black uppercase tracking-[0.3em] text-slate-500 italic">Meta Mensal (Outub)</h5>
             </div>
             <div className="space-y-2">
               <h2 className="text-4xl premium-title tracking-tighter">R$ 48.2K</h2>
               <p className="text-[10px] font-black text-emerald-500 uppercase italic tracking-widest">+ R$ 12.4K Faltantes</p>
             </div>
             <Progress value={75} radius="full" size="md" color="secondary" className="shadow-lg shadow-indigo-600/10" />
          </div>

        </div>

      </div>
    </div>
  )
}

function Plus({ className }: { className?: string }) {
  return (
    <svg xmlns="http://www.w3.org/2000/svg" className={className} fill="none" viewBox="0 0 24 24" stroke="currentColor">
       <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" d="M12 4v16m8-8H4" />
    </svg>
  )
}
