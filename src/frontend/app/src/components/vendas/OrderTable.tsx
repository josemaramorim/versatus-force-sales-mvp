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
  onEdit?: (item: ItemPedido) => void
}

export function OrderTable({ items, onRemove, onEdit }: OrderTableProps) {
  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-20 bg-slate-50 dark:bg-slate-950/20 rounded-[2.5rem] border-2 border-dashed border-slate-200 dark:border-slate-800">
        <div className="flex h-20 w-20 items-center justify-center rounded-[2rem] bg-slate-100 dark:bg-slate-900/40 text-slate-400 dark:text-slate-700 shadow-inner">
          <ShoppingBag className="h-10 w-10" />
        </div>
        <p className="mt-6 text-[10px] font-black text-slate-500 uppercase tracking-[0.4em] italic">Nenhum item adicionado</p>
        <p className="text-[9px] text-slate-400 font-bold mt-2 uppercase tracking-widest leading-none">Aguardando entrada de produtos...</p>
      </div>
    )
  }

  return (
    <div>
      {/* Layout Desktop/Tablet: Tabela Corporativa */}
      <div className="hidden md:block overflow-x-auto">
        <table className="premium-table">
          <thead className="premium-label opacity-40">
            <tr>
              <th className="px-4 pb-3 italic">Produtos / Identificação</th>
              <th className="px-4 pb-3 text-center uppercase">Qtd.</th>
              <th className="px-4 pb-3 text-right uppercase">Unit. (R$)</th>
              <th className="px-4 pb-3 text-right uppercase">Desc. (R$)</th>
              <th className="px-4 pb-3 text-right uppercase">Total (R$)</th>
              <th className="px-4 pb-3 text-center uppercase">Ações</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => (
              <tr key={item.id} className="group italic">
                <td className="px-4 py-3.5 first:rounded-l-2xl">
                  <div className="flex items-center gap-3">
                      <Avatar 
                          src={item.imagemUrl} 
                          name={item.nome.charAt(0)}
                          radius="md" 
                          size="sm"
                          isBordered
                          className="bg-slate-100 dark:bg-slate-900 border-slate-200 dark:border-slate-800 shrink-0"
                      />
                      <div className="min-w-0">
                          <div className="font-black text-slate-900 dark:text-slate-200 text-sm leading-snug truncate max-w-[240px]">{item.nome}</div>
                          <div className="text-[9px] text-slate-500 font-black tracking-wider mt-1 uppercase leading-none">SKU: {item.sku} • {item.naturezaOperacao}</div>
                      </div>
                  </div>
                </td>
                <td className="px-4 py-3.5 text-center font-mono font-black text-slate-500 dark:text-slate-400 text-sm uppercase tracking-tight">
                  {item.quantidade} x
                </td>
                <td className="px-4 py-3.5 text-right font-mono text-slate-800 dark:text-slate-200 text-sm">
                  {item.valorUnitario.toFixed(2)}
                </td>
                <td className={`px-4 py-3.5 text-right font-mono text-xs font-black ${item.valorDesconto > 0 ? 'text-amber-500' : 'text-slate-400 dark:text-slate-600'}`}>
                  {item.valorDesconto > 0 ? `-${item.valorDesconto.toFixed(2)}` : '0.00'}
                </td>
                <td className="px-4 py-3.5 text-right font-mono text-blue-600 dark:text-blue-400 font-black text-sm tracking-tight pr-4">
                  {item.total.toFixed(2)}
                </td>
                <td className="px-4 py-3.5 text-center last:rounded-r-2xl">
                  <div className="flex items-center justify-center gap-2">
                      <Button 
                          isIconOnly 
                          variant="light" 
                          size="sm" 
                          onPress={() => onEdit?.(item)}
                          className="h-9 w-9 text-slate-400 hover:text-blue-500 hover:bg-blue-500/10 rounded-xl transition-all"
                      >
                          <Edit2 className="h-4.5 w-4.5" />
                      </Button>
                      <Button 
                          isIconOnly 
                          variant="light" 
                          size="sm" 
                          color="danger"
                          onPress={() => onRemove(item.id)}
                          className="h-9 w-9 text-slate-400 hover:text-rose-500 hover:bg-rose-500/10 rounded-xl transition-all"
                      >
                          <Trash2 className="h-4.5 w-4.5" />
                      </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {/* Layout Mobile: Cards Táteis */}
      <div className="block md:hidden space-y-4">
        {items.map((item) => (
          <div key={item.id} className="p-5 bg-white dark:bg-slate-900 border border-slate-150 dark:border-slate-800/80 rounded-[2rem] shadow-sm flex flex-col gap-4">
            <div className="flex items-start gap-4">
              <Avatar 
                src={item.imagemUrl} 
                name={item.nome.charAt(0)}
                radius="lg" 
                size="md"
                isBordered
                className="bg-slate-100 dark:bg-slate-900 border-slate-200 dark:border-slate-800 shrink-0"
              />
              <div className="min-w-0 flex-1">
                <div className="font-black text-slate-900 dark:text-slate-200 text-base leading-tight break-words italic">{item.nome}</div>
                <div className="text-[9px] text-slate-500 font-black tracking-[0.2em] mt-2 uppercase leading-none">SKU: {item.sku}</div>
                <div className="text-[9px] text-blue-500 font-black tracking-wider mt-1 uppercase leading-none">{item.naturezaOperacao}</div>
              </div>
              <div className="flex items-center gap-1 shrink-0">
                <Button 
                  isIconOnly 
                  variant="light" 
                  size="sm" 
                  onPress={() => onEdit?.(item)}
                  className="h-10 w-10 text-slate-400 hover:text-blue-500 hover:bg-blue-500/10 rounded-xl transition-all"
                >
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
            </div>

            <div className="border-t border-slate-100 dark:border-slate-800/60 pt-4 flex items-center justify-between">
              <div className="flex flex-col">
                <span className="text-[8px] font-black text-slate-400 uppercase tracking-widest leading-none mb-1">Preço & Qtd</span>
                <span className="text-xs font-bold text-slate-600 dark:text-slate-400 font-mono">
                  {item.quantidade} x R$ {item.valorUnitario.toFixed(2)}
                </span>
                {item.valorDesconto > 0 && (
                  <span className="text-[10px] text-amber-500 font-mono font-black mt-1">
                    Desc: - R$ {item.valorDesconto.toFixed(2)}
                  </span>
                )}
              </div>
              <div className="text-right flex flex-col items-end">
                <span className="text-[8px] font-black text-blue-500 uppercase tracking-widest leading-none mb-1">Total Linha</span>
                <span className="text-base font-black font-mono text-blue-600 dark:text-blue-400 italic">
                  R$ {item.total.toFixed(2)}
                </span>
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
