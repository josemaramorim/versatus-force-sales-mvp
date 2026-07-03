'use client'

import React, { useState, useMemo, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { ClientSearch } from '@/components/vendas/ClientSearch'
import { OrderTable } from '@/components/vendas/OrderTable'
import { ItemModal } from '@/components/vendas/ItemModal'
import { ItemPedido, Cliente, CondicaoPagamento, TenantParameters } from '@/types/vendas'
import { criarPedidoApi, getCondicoesPagamento, getTenantParameters } from '@/lib/vendaApi'
import { 
  Plus, 
  ShoppingCart, 
  FileText, 
  ChevronRight, 
  ChevronUp,
  ChevronDown,
  Save, 
  Trash2,
  Tag,
  ArrowUpRight,
  Zap,
  CheckCircle2,
  Settings,
  CreditCard,
  AlertTriangle
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
  Avatar,
  Modal,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter
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
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [isSummaryExpanded, setIsSummaryExpanded] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isOnline, setIsOnline] = useState(true)
  const [condicoes, setCondicoes] = useState<CondicaoPagamento[]>([])
  const [tenantParameters, setTenantParameters] = useState<TenantParameters>({ tabelaPrecoIdDefault: 1, permiteAlterarTabelaPreco: true })
  const [isSaving, setIsSaving] = useState(false)
  const [isConfirmLeaveOpen, setIsConfirmLeaveOpen] = useState(false)
  const [pendingUrl, setPendingUrl] = useState<string | null>(null)
  const [isPopStateNavigation, setIsPopStateNavigation] = useState(false)

  // Verifica se o formulário/pedido está modificado para proteção de navegação
  const isDirty = useMemo(() => {
    if (isSaving) return false
    return selectedCliente !== null || items.length > 0 || observacoes.length > 0 || descontoGlobal !== 0 || acrescimoGlobal !== 0
  }, [selectedCliente, items, observacoes, descontoGlobal, acrescimoGlobal, isSaving])

  function handleConfirmLeave() {
    setIsConfirmLeaveOpen(false)
    setIsSaving(true)
    if (isPopStateNavigation) {
      router.back()
    } else if (pendingUrl) {
      router.push(pendingUrl)
    }
  }

  function handleCancelLeave() {
    setIsConfirmLeaveOpen(false)
    setPendingUrl(null)
    if (isPopStateNavigation) {
      window.history.pushState(null, '', window.location.href)
      setIsPopStateNavigation(false)
    }
  }

  // 1. Interceptar reload, F5 ou fechamento de aba do navegador
  useEffect(() => {
    const handleBeforeUnload = (e: BeforeUnloadEvent) => {
      if (isDirty) {
        e.preventDefault()
        e.returnValue = 'Você tem alterações não salvas no pedido. Deseja realmente sair?'
        return e.returnValue
      }
    }
    window.addEventListener('beforeunload', handleBeforeUnload)
    return () => window.removeEventListener('beforeunload', handleBeforeUnload)
  }, [isDirty])

  // 2. Interceptar navegação do botão Voltar / Avançar do navegador (popstate)
  useEffect(() => {
    if (!isDirty) return

    // Insere um estado dummy na pilha para consumir a ação de voltar
    window.history.pushState(null, '', window.location.href)

    const handlePopState = () => {
      setIsPopStateNavigation(true)
      setIsConfirmLeaveOpen(true)
    }

    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [isDirty])

  // 3. Interceptar cliques em links internos do Next.js (ex: sidebar)
  useEffect(() => {
    if (!isDirty) return

    const handleAnchorClick = (e: MouseEvent) => {
      let target = e.target as HTMLElement | null
      while (target && target.tagName !== 'A') {
        target = target.parentElement
      }

      if (target && target.tagName === 'A') {
        const href = target.getAttribute('href')
        if (href && href.startsWith('/') && href !== '/vendas/nova' && !href.startsWith('#')) {
          e.preventDefault()
          setPendingUrl(href)
          setIsPopStateNavigation(false)
          setIsConfirmLeaveOpen(true)
        }
      }
    }

    // Registra na fase de captura (true) para interceptar antes do roteador do Next.js processar o clique
    document.addEventListener('click', handleAnchorClick, true)
    return () => document.removeEventListener('click', handleAnchorClick, true)
  }, [isDirty])

  useEffect(() => {
    getCondicoesPagamento()
      .then((data) => {
        setCondicoes(data)
        if (data.length > 0) {
          setCondicaoPagamento(data[0].condicaoPagtoIdERP.toString())
        }
      })
      .catch(console.error)

    getTenantParameters()
      .then(setTenantParameters)
      .catch(console.error)
  }, [])

  React.useEffect(() => {
    setIsOnline(navigator.onLine)
    const handleOnline = () => setIsOnline(true)
    const handleOffline = () => setIsOnline(false)
    window.addEventListener('online', handleOnline)
    window.addEventListener('offline', handleOffline)
    return () => {
      window.removeEventListener('online', handleOnline)
      window.removeEventListener('offline', handleOffline)
    }
  }, [])

  const subtotal = useMemo(() => items.reduce((acc, item) => acc + item.total, 0), [items])
  const totalFinal = useMemo(() => subtotal - descontoGlobal + acrescimoGlobal, [subtotal, descontoGlobal, acrescimoGlobal])

  function handleAddItem(item: ItemPedido) {
    setItems(prev => [item, ...prev])
  }

  function handleRemoveItem(id: string) {
    setItems(prev => prev.filter(item => item.id !== id))
  }

  async function handleConfirmarPedido() {
    if (!selectedCliente) {
      setSubmitError('Selecione um cliente para continuar.')
      return
    }
    if (items.length === 0) {
      setSubmitError('Adicione ao menos um item ao pedido.')
      return
    }

    setSubmitError(null)
    setIsSubmitting(true)

    const selectedCond = condicoes.find(c => c.condicaoPagtoIdERP.toString() === condicaoPagamento)

    // First vencimento = today + diasParcelamento
    const primeiroVencimento = new Date()
    const dias = selectedCond?.diasParcelamento ?? 30
    primeiroVencimento.setDate(primeiroVencimento.getDate() + (dias > 0 ? dias : 30))

    try {
      setIsSaving(true)
      await criarPedidoApi({
        clienteId: selectedCliente.id,
        itens: items.map(i => ({
          produtoId: i.produtoId,
          sku: i.sku,
          nome: i.nome,
          quantidade: i.quantidade,
          precoUnitario: i.valorUnitario,
          desconto: i.valorDesconto,
          tabelaPrecoEstoqueIdERP: i.tabelaPrecoEstoqueIdERP,
        })),
        condicaoPagamento: {
          condicaoPagamentoId: selectedCond ? `cond-${selectedCond.condicaoPagtoIdERP}` : 'cond-1',
          primeiroVencimento: primeiroVencimento.toISOString(),
          formaPagamento: selectedCond ? selectedCond.formaCobrancaIdERP.toString() : '1',
        },
        observacao: observacoes || undefined,
      })
      router.push('/pedidos')
    } catch {
      setIsSaving(false)
      setSubmitError('Erro ao registrar pedido. Tente novamente.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="space-y-12 pb-32 lg:pb-0">
      
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
              <span className="text-slate-500 text-[10px] font-black uppercase tracking-widest italic">Sync: <span className={clsx("font-black font-mono tracking-tighter uppercase", isOnline ? "text-emerald-500" : "text-amber-500")}>{isOnline ? 'Online' : 'Offline'}</span></span>
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
                          <ClientSearch 
                            selectedId={selectedCliente?.id} 
                            onSelect={setSelectedCliente} 
                          />
                      </div>
                      <div className="w-full md:w-64 space-y-6 leading-none">  
                           <label className="premium-label tracking-[0.4em]">CPF / CNPJ Ativo</label>
                          <div className="h-16 px-8 flex items-center bg-slate-50 dark:bg-slate-950/40 rounded-3xl border border-slate-100 dark:border-slate-900 text-slate-400 font-bold text-lg font-mono tracking-[0.2em] italic leading-none">
                            {selectedCliente?.documento || '---'}
                          </div>
                      </div>
                  </div>
              </div>

               {/* Items List Card */}
              <div className="premium-card overflow-hidden">
                  <div className="p-6 lg:p-10 border-b border-slate-100 dark:border-slate-800 flex flex-col sm:flex-row items-center justify-between gap-6 leading-none bg-slate-50/50 dark:bg-slate-900/50">
                      <h3 className="text-xl premium-title flex items-center space-x-3 leading-none italic">
                          <span className="w-2 h-8 bg-blue-600 rounded-full"></span>
                          <span>Listagem de Itens</span>
                      </h3>
                      <Button 
                        onPress={onOpen}
                        className="w-full sm:w-auto px-8 py-5 bg-blue-600/10 border border-blue-500/20 text-blue-500 font-black rounded-2xl hover:bg-blue-600 hover:text-white transition-all transform active:scale-95 leading-none shadow-sm italic text-xs tracking-tight capitalize"
                      >
                        Nova Linha
                      </Button>
                  </div>

                  <div className="p-4 lg:p-8 overflow-x-auto">
                      <OrderTable items={items} onRemove={handleRemoveItem} /> 
                  </div>
              </div>

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

          {/* Checkout / Summary Sidebar (Desktop only) */}
          <div className="hidden lg:block space-y-10 lg:sticky lg:top-36 h-fit">
              
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
                        popoverProps={{
                          radius: "lg",
                          className: "p-2 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 shadow-xl z-[9999]",
                        }}
                      >
                        {condicoes.map((cond) => (
                          <SelectItem key={cond.condicaoPagtoIdERP.toString()} value={cond.condicaoPagtoIdERP.toString()} className="font-bold italic">
                            {cond.descricao}
                          </SelectItem>
                        ))}
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
                        isLoading={isSubmitting}
                        className="mt-8 py-8 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-3xl shadow-2xl shadow-blue-500/40 transition-all uppercase tracking-[0.2em] text-xs italic tracking-tighter transform active:scale-95"
                        onPress={handleConfirmarPedido}
                      >
                        Confirmar Pedido
                      </Button>
                      {submitError && (
                        <p className="text-xs text-red-400 font-bold text-center mt-2">{submitError}</p>
                      )}
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
        tenantParameters={tenantParameters}
      />

      {/* Sticky Bottom Summary Sheet (Mobile/Tablet only) */}
      <div className="block lg:hidden fixed bottom-0 left-0 right-0 z-50 bg-white/95 dark:bg-slate-900/95 backdrop-blur-md border-t border-slate-200 dark:border-slate-800 shadow-[0_-15px_40px_rgba(0,0,0,0.15)] transition-all duration-300">
        
        {/* Toggle Expand Handle */}
        <button 
          type="button"
          onClick={() => setIsSummaryExpanded(!isSummaryExpanded)}
          className="w-full py-3 flex items-center justify-center gap-2 text-slate-500 hover:text-blue-500 border-b border-slate-100 dark:border-slate-800/60 relative"
        >
          <div className="w-12 h-1 bg-slate-300 dark:bg-slate-700 rounded-full absolute top-2" />
          {isSummaryExpanded ? (
            <>
              <ChevronDown className="h-4 w-4 text-slate-500" />
              <span className="text-[9px] font-black uppercase tracking-widest italic">Ocultar Fechamento</span>
            </>
          ) : (
            <>
              <ChevronUp className="h-4 w-4 text-slate-500" />
              <span className="text-[9px] font-black uppercase tracking-widest italic">Configurar Fechamento & Descontos</span>
            </>
          )}
        </button>

        {/* Expandable Content Panel */}
        {isSummaryExpanded && (
          <div className="p-6 space-y-6 max-h-[60vh] overflow-y-auto">
            <div className="space-y-4">
              <label className="premium-label tracking-[0.4em]">Forma de Pagto</label>
              <Select 
                selectedKeys={[condicaoPagamento]}
                onChange={(e) => setCondicaoPagamento(e.target.value)}
                variant="flat"
                radius="lg"
                classNames={{
                  trigger: "h-14 px-6 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 italic font-black text-slate-500",
                }}
                popoverProps={{
                  radius: "lg",
                  className: "p-2 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 shadow-xl z-[9999]",
                }}
              >
                {condicoes.map((cond) => (
                  <SelectItem key={cond.condicaoPagtoIdERP.toString()} value={cond.condicaoPagtoIdERP.toString()} className="font-bold italic">
                    {cond.descricao}
                  </SelectItem>
                ))}
              </Select>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-3 text-center">
                <label className="premium-label tracking-widest opacity-50 block italic text-[9px]">Desc. Geral (R$)</label>
                <input 
                  type="number" 
                  value={descontoGlobal} 
                  onChange={(e) => setDescontoGlobal(Number(e.target.value))}
                  className="w-full h-12 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl outline-none focus:ring-2 focus:ring-emerald-500 font-mono text-emerald-500 font-black text-center shadow-inner text-sm italic"
                />
              </div>
              <div className="space-y-3 text-center">
                <label className="premium-label tracking-widest opacity-50 block italic text-[9px]">Acresc. Geral (R$)</label>
                <input 
                  type="number" 
                  value={acrescimoGlobal}
                  onChange={(e) => setAcrescimoGlobal(Number(e.target.value))}
                  className="w-full h-12 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl outline-none focus:ring-2 focus:ring-rose-500 font-mono text-rose-500 font-black text-center shadow-inner text-sm italic"
                />
              </div>
            </div>

            <div className="pt-4 border-t border-slate-100 dark:border-slate-800/80 space-y-4 font-mono text-[9px] font-black uppercase tracking-wider italic text-slate-400">
              <div className="flex items-center justify-between">
                <span>Subtotal Bruto</span>
                <span className="text-slate-600 dark:text-slate-500">R$ {subtotal.toFixed(2)}</span>
              </div>
              <div className="flex items-center justify-between text-amber-500/80">
                <span>Desconto Total</span>
                <span>- R$ {(descontoGlobal + (items.reduce((acc, i) => acc + i.valorDesconto, 0))).toFixed(2)}</span>
              </div>
            </div>
          </div>
        )}

        {/* Persistent Bottom Bar */}
        <div className="px-6 py-4 flex items-center justify-between gap-4 border-t border-slate-100 dark:border-slate-800/60 bg-white/50 dark:bg-slate-900/50">
          <div className="flex flex-col">
            <span className="text-[8px] font-black text-slate-400 uppercase tracking-widest leading-none mb-1">Total Pedido</span>
            <span className="text-2xl font-black font-mono text-blue-500 italic leading-none">
              R$ {totalFinal.toFixed(2)}
            </span>
            <span className="text-[8px] font-bold text-slate-400 mt-1 uppercase leading-none">
              {items.length} {items.length === 1 ? 'item' : 'itens'}
            </span>
          </div>
          <Button 
            size="lg"
            isLoading={isSubmitting}
            className="flex-1 max-w-[200px] py-6 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-2xl shadow-lg shadow-blue-500/20 transition-all uppercase tracking-wider text-xs italic transform active:scale-95"
            onClick={handleConfirmarPedido}
          >
            Confirmar
          </Button>
        </div>
      </div>

      {/* Modal de Confirmação de Abandono Estilizado */}
      <Modal 
        isOpen={isConfirmLeaveOpen} 
        onClose={handleCancelLeave}
        backdrop="blur"
        placement="center"
        classNames={{
          backdrop: "bg-slate-950/60 backdrop-blur-md",
          base: "border border-white/10 bg-slate-900/90 text-white rounded-3xl p-4 shadow-2xl z-[99999] mx-4",
        }}
      >
        <ModalContent>
          {(onClose) => (
            <>
              <ModalHeader className="flex flex-col gap-1 items-center pb-2 text-center">
                <div className="w-12 h-12 bg-rose-500/10 border border-rose-500/20 rounded-2xl flex items-center justify-center mb-2 animate-pulse">
                  <AlertTriangle className="h-6 w-6 text-rose-500" />
                </div>
                <h3 className="text-xl font-black italic tracking-tight premium-title">Alterações não Salvas!</h3>
              </ModalHeader>
              <ModalBody className="py-4 text-center leading-relaxed">
                <p className="text-slate-400 font-medium text-sm italic">
                  Você iniciou o preenchimento de um pedido. Se sair desta tela agora, todos os itens e dados informados serão perdidos permanentemente.
                </p>
                <p className="text-rose-500/80 font-bold text-xs uppercase tracking-wider mt-2">
                  Deseja realmente abandonar a operação?
                </p>
              </ModalBody>
              <ModalFooter className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 justify-center pt-2 w-full">
                <Button 
                  variant="flat" 
                  onPress={handleCancelLeave}
                  className="w-full sm:w-auto px-6 py-4 bg-slate-800/80 hover:bg-slate-800 text-slate-300 font-bold rounded-xl active:scale-95 transition-transform"
                >
                  Permanecer no Pedido
                </Button>
                <Button 
                  color="danger" 
                  onPress={handleConfirmLeave}
                  className="w-full sm:w-auto px-6 py-4 bg-rose-600 hover:bg-rose-500 text-white font-black rounded-xl active:scale-95 transition-transform uppercase tracking-wider text-xs"
                >
                  Abandonar Pedido
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
    </div>
  )
}
