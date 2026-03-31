'use client'

import React, { useState, useMemo } from 'react'
import { useRouter } from 'next/navigation'
import { ClientSearch } from '@/components/vendas/ClientSearch'
import { OrderTable } from '@/components/vendas/OrderTable'
import { ItemModal } from '@/components/vendas/ItemModal'
import { ItemPedido, Cliente } from '@/types/vendas'
import { 
  Plus, 
  ShoppingCart, 
  FileText, 
  ChevronRight, 
  Save, 
  Trash2,
  Tag,
  ArrowUpRight,
  Zap,
  CheckCircle2,
  Settings,
  CreditCard
} from 'lucide-react'
import { 
  Button, 
  Card, 
  CardBody, 
  Divider, 
  Input, 
  Select, 
  SelectItem, 
  Textarea,
  Tooltip,
  useDisclosure,
  Avatar
} from '@nextui-org/react'
import { clsx } from 'clsx'

export default function NovaVendaPage() {
  const router = useRouter()
  const { isOpen, onOpen, onClose } = useDisclosure()
  const [selectedCliente, setSelectedCliente] = useState<Cliente | null>(null)
  const [items, setItems] = useState<ItemPedido[]>([])
  const [observacoes, setObservacoes] = useState('')
  const [condicaoPagamento, setCondicaoPagamento] = useState('avento')
  const [descontoGlobal, setDescontoGlobal] = useState(0)
  const [acrescimoGlobal, setAcrescimoGlobal] = useState(0)

  const subtotal = useMemo(() => items.reduce((acc, item) => acc + item.total, 0), [items])
  const totalFinal = useMemo(() => subtotal - descontoGlobal + acrescimoGlobal, [subtotal, descontoGlobal, acrescimoGlobal])

  function handleAddItem(item: ItemPedido) {
    setItems(prev => [item, ...prev])
  }

  function handleRemoveItem(id: string) {
    setItems(prev => prev.filter(item => item.id !== id))
  }

  return (
    <div className="space-y-12">
      
      {/* High-Fidelity Header */}
      <header className="flex flex-col sm:flex-row items-center sm:justify-between gap-6 pb-8 border-b border-slate-100 dark:border-slate-900 leading-none">
          <div className="flex flex-col sm:flex-row items-center gap-4 lg:gap-6 text-center sm:text-left">
              <div className="w-14 h-14 bg-gradient-to-br from-blue-600 to-indigo-700 rounded-[1.5rem] flex items-center justify-center shadow-blue-500/20 shadow-2xl leading-none shrink-0">
                  <ShoppingCart className="h-6 w-6 text-white" />
              </div>
              <div className="min-w-0">
                <h1 className="text-2xl lg:text-4xl premium-title leading-none truncate pr-2">Nova Operação</h1>
                <p className="text-[10px] font-black uppercase tracking-[0.2em] lg:tracking-[0.4em] text-slate-500 mt-2 italic">Versatus Force Sales v2.0</p>
              </div>
          </div>
          <div className="flex items-center space-x-4 shrink-0">
              <span className="text-slate-500 text-[10px] font-black uppercase tracking-widest italic">Sync: <span className="text-blue-500 font-black font-mono tracking-tighter uppercase">DEMO-OFFLINE</span></span>
              <Avatar isBordered radius="full" size="sm" className="bg-slate-800 border-slate-700" />
          </div>
      </header>

      <section className="grid grid-cols-1 lg:grid-cols-4 gap-12 items-start">
          
          {/* Main Context Area */}
          <div className="lg:col-span-3 space-y-12">
              
              {/* Cliente Identification Card */}
              <div className="premium-card p-10">
                  <div className="flex flex-col md:flex-row md:items-end gap-10">
                      <div className="flex-1 space-y-6">
                          <label className="premium-label tracking-[0.4em]">Busca de Cliente Solicitante</label>
                          <Button 
                            fullWidth 
                            size="lg"
                            className="mt-8 py-8 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-3xl shadow-2xl shadow-blue-500/40 transition-all uppercase tracking-[0.2em] text-xs italic tracking-tighter transform active:scale-95"
                            onPress={async () => {
                              const draft = {
                                clienteId: selectedCliente?.id ?? null,
                                itens: items,
                                observacoes,
                                condicaoPagamentoId: condicaoPagamento,
                                descontoGlobal,
                                acrescimoGlobal,
                                subtotal,
                                totalFinal,
                              }
                              try {
                                const { createPedido } = await import('@/lib/pedidosMock')
                                const created = await createPedido(draft)
                                router.push('/pedidos')
                                console.log('Pedido criado (mock):', created)
                              } catch (err) {
                                console.error('Erro ao criar pedido (mock)', err)
                                alert('Erro ao criar pedido (mock)')
                              }
                            }}
                          >
                            Confirmar Pedido
                          </Button>

              {/* Observations Card */}
              <div className="premium-card-inner p-10 space-y-6 shadow-inner">
                  <h3 className="text-xl premium-title flex items-center space-x-3 leading-none italic">
                      <span className="w-1.5 h-6 bg-amber-600 rounded-full"></span>
                      <span>Observações Finais</span>
                  </h3>
                  <Textarea 
                    rows={4} 
                    className="w-full"
                    placeholder="Ponto de referência, observações de logística ou observações fiscais..."
                    value={observacoes}
                    onValueChange={setObservacoes}
                    variant="flat"
                    radius="lg"
                    classNames={{
                      input: "min-h-[140px] font-black text-sm italic p-6 leading-relaxed bg-slate-950/20",
                      inputWrapper: "bg-slate-950/20 dark:bg-slate-950/40"
                    }}
                  />
              </div>
          </div>

          {/* Checkout / Summary Siderbar */}
          <div className="space-y-10 lg:sticky lg:top-36 h-fit">
              
              <div className="premium-card p-8 space-y-10 leading-none shadow-2xl relative overflow-hidden">
                  {/* Subtle Glow */}
                  <div className="absolute -right-20 -top-20 h-40 w-40 bg-blue-600/5 blur-[80px]" />

                   <div className="space-y-6 leading-none">
                      <label className="premium-label tracking-[0.4em]">Forma de Pagto</label>
                      <Select 
                        selectedKeys={[condicaoPagamento]}
                        onChange={(e) => setCondicaoPagamento(e.target.value)}
                        variant="flat"
                        radius="lg"
                        classNames={{
                          trigger: "h-16 px-8 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 italic font-black text-slate-500",
                        }}
                      >
                        <SelectItem key="avento" value="avento" className="font-bold italic">Dinheiro A Vista (3%)</SelectItem>
                        <SelectItem key="30_60" value="30_60" className="font-bold italic">Boleto 30/60 Dias</SelectItem>
                      </Select>
                  </div>

                  <div className="grid grid-cols-2 gap-4 border-t border-slate-50 dark:border-slate-800 pt-10 leading-none">
                      <div className="space-y-4 leading-none text-center">
                          <label className="premium-label tracking-widest opacity-50 block italic text-[9px]">Desc. Geral</label>
                          <input 
                            type="number" 
                            value={descontoGlobal} 
                            onChange={(e) => setDescontoGlobal(Number(e.target.value))}
                            className="w-full h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl outline-none focus:ring-2 focus:ring-emerald-500 font-mono text-emerald-500 font-black text-center shadow-inner text-sm italic"
                          />
                      </div>
                      <div className="space-y-4 leading-none text-center">
                          <label className="premium-label tracking-widest opacity-50 block italic text-[9px]">Acresc. Geral</label>
                          <input 
                            type="number" 
                            value={acrescimoGlobal}
                            onChange={(e) => setAcrescimoGlobal(Number(e.target.value))}
                            className="w-full h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl outline-none focus:ring-2 focus:ring-rose-500 font-mono text-rose-500 font-black text-center shadow-inner text-sm italic"
                          />
                      </div>
                  </div>

                  <div className="pt-10 border-t border-slate-50 dark:border-slate-800 space-y-6 leading-none">
                      <div className="flex items-center justify-between font-mono text-slate-400 text-[10px] font-black uppercase tracking-widest leading-none italic">
                          <span>Subtotal Bruto</span>
                          <span className="text-slate-600 dark:text-slate-500">R$ {subtotal.toFixed(2)}</span>
                      </div>
                      <div className="flex items-center justify-between font-mono text-amber-500/60 text-[10px] font-black uppercase tracking-widest leading-none italic">
                          <span>Desconto Total</span>
                          <span className="text-amber-500">- R$ {(descontoGlobal + (items.reduce((acc, i) => acc + i.valorDesconto, 0))).toFixed(2)}</span>
                      </div>
                      <div className="pt-10 border-t border-slate-50 dark:border-slate-800 leading-none">
                           <div className="flex flex-col space-y-6 leading-none">
                              <span className="text-4xl lg:text-6xl font-black font-mono tracking-tighter text-blue-500 italic text-center leading-none">R$ {totalFinal.toFixed(2)}</span>
                              <span className="text-[8px] font-black text-slate-400 dark:text-slate-700 uppercase tracking-[0.4em] text-center leading-none italic">Versatus.Net • MVP-06</span>
                          </div>
                      </div>

                      <Button 
                        fullWidth 
                        size="lg"
                        className="mt-8 py-8 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-3xl shadow-2xl shadow-blue-500/40 transition-all uppercase tracking-[0.2em] text-xs italic tracking-tighter transform active:scale-95"
                        onPress={() => alert('Pedido faturado com sucesso no Versatus ERP!')}
                      >
                        Confirmar Pedido
                      </Button>
                  </div>
              </div>

               {/* Sync Status Badge */}
              <div className="flex items-center justify-center gap-3 px-6 py-4 bg-slate-50 dark:bg-slate-900/50 rounded-[1.5rem] border border-slate-100 dark:border-slate-800">
                <Zap className="h-4 w-4 text-emerald-500 animate-pulse" />
                <p className="text-[9px] font-black uppercase tracking-[0.3em] text-slate-400 italic">Sincronização Ativa</p>
              </div>
          </div>

      </section>

      {/* Item Modal (NextUI refactored) */}
      <ItemModal 
        isOpen={isOpen} 
        onClose={onClose} 
        onAdd={handleAddItem} 
      />
    </div>
  )
}
