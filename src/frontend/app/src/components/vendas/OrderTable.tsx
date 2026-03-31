'use client'

import React from 'react'
import { ItemPedido } from '@/types/vendas'
import { Trash2, Edit2, ShoppingBag } from 'lucide-react'
import { 
  Button,
  Tooltip,
  Avatar
} from '@nextui-org/react'

interface OrderTableProps {
  items: ItemPedido[]
  onRemove: (id: string) => void
}

export function OrderTable({ items, onRemove }: OrderTableProps) {
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 bg-slate-950/20 rounded-[2.5rem] border-2 border-dashed border-slate-100 dark:border-slate-800">
        <div className="flex h-20 w-20 items-center justify-center rounded-[2rem] bg-slate-900/40 text-slate-700 shadow-inner">
          <ShoppingBag className="h-10 w-10" />
        </div>
        <p className="mt-6 text-[10px] font-black text-slate-500 uppercase tracking-[0.4em] italic">Nenhum item adicionado</p>
        <p className="text-[9px] text-slate-400 font-bold mt-2 uppercase tracking-widest leading-none">Aguardando entrada de produtos...</p>
      </div>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="premium-table">
        <thead className="premium-label opacity-40">
          <tr>
            <th className="px-8 pb-4 italic">Produtos / Identificação</th>
            <th className="px-8 pb-4 text-center uppercase">Qtd.</th>
            <th className="px-8 pb-4 text-right uppercase">Unit.</th>
            <th className="px-8 pb-4 text-right uppercase">Desc.</th>
            <th className="px-8 pb-4 text-right uppercase">Total</th>
            <th className="px-8 pb-4 text-center uppercase">Ações</th>
          </tr>
        </thead>
        <tbody>
          {items.map((item) => (
            <tr key={item.id} className="group italic">
              <td className="px-8 py-8 first:rounded-l-[2rem]">
                <div className="flex items-center gap-4">
                    <Avatar 
                        src={item.imagemUrl} 
                        name={item.nome.charAt(0)}
                        radius="lg" 
                        size="md"
                        isBordered
                        className="bg-slate-900 border-slate-800 shrink-0"
                    />
                    <div>
                        <div className="font-black text-slate-900 dark:text-slate-200 text-base leading-none">{item.nome}</div>
                        <div className="text-[9px] text-slate-500 font-black tracking-[0.3em] mt-2 uppercase leading-none">SKU: {item.sku} • {item.naturezaOperacao}</div>
                    </div>
                </div>
              </td>
              <td className="px-8 py-8 text-center font-mono font-black text-slate-400 text-base uppercase tracking-tight">
                {item.quantidade} x
              </td>
              <td className="px-8 py-8 text-right font-mono text-slate-500 text-sm opacity-60">
                R$ {item.valorUnitario.toFixed(2)}
              </td>
              <td className="px-8 py-8 text-right font-mono text-amber-500/70 text-xs font-black">
                - R$ {item.valorDesconto.toFixed(2)}
              </td>
              <td className="px-8 py-8 text-right font-mono text-blue-500 font-black text-2xl tracking-tighter leading-none pr-8">
                R$ {item.total.toFixed(2)}
              </td>
              <td className="px-8 py-8 text-center last:rounded-r-[2rem]">
                <div className="flex items-center justify-center gap-2">
                    <Button isIconOnly variant="light" size="sm" className="h-10 w-10 text-slate-400 hover:text-blue-500 hover:bg-blue-500/10 rounded-xl transition-all">
                        <Edit2 className="h-5 w-5" />
                    </Button>
                    <Button 
                        isIconOnly 
                        variant="light" 
                        size="sm" 
                        color="danger"
                        onPress={() => onRemove(item.id)}
                        className="h-10 w-10 text-slate-400 hover:text-rose-500 hover:bg-rose-500/10 rounded-xl transition-all"
                    >
                        <Trash2 className="h-5 w-5" />
                    </Button>
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
