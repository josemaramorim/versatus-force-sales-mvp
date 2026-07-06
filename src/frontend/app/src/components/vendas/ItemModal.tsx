'use client'

import React, { useState, useEffect, useMemo } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { MOCK_NATUREZAS } from '@/lib/mocks'
import { searchProdutos } from '@/lib/vendaApi'
import { ItemPedido, Produto, TenantParameters } from '@/types/vendas'
import { 
  Plus, 
  Calculator, 
  Package, 
  Search, 
  BadgePercent, 
  ArrowUpRight,
  X
} from 'lucide-react'
import { 
  Modal, 
  ModalContent, 
  ModalHeader, 
  ModalBody, 
  ModalFooter, 
  Button, 
  Autocomplete, 
  AutocompleteItem, 
  Input, 
  Select, 
  SelectItem,
  Avatar,
  Card,
  Divider
} from '@nextui-org/react'

const schema = z.object({
  produtoId: z.string().min(1, 'Selecione um produto'),
  quantidade: z.number().min(0.01, 'Min: 0.01'),
  valorUnitario: z.number().min(0, 'Min: 0'),
  valorDesconto: z.number().min(0),
  valorAcrescimo: z.number().min(0),
  naturezaOperacao: z.string().min(1, 'Selecione a natureza'),
})

type FormValues = z.infer<typeof schema>

interface ItemModalProps {
  isOpen: boolean
  onClose: () => void
  onAdd: (item: ItemPedido) => void
  onEdit?: (item: ItemPedido) => void
  editingItem?: ItemPedido | null
  tenantParameters: TenantParameters
}

export function ItemModal({ isOpen, onClose, onAdd, onEdit, editingItem, tenantParameters }: ItemModalProps) {
  const [selectedProduto, setSelectedProduto] = useState<Produto | null>(null)
  const [produtos, setProdutos] = useState<Produto[]>([])
  const [productInputValue, setProductInputValue] = useState('')
  const [selectedTabelaId, setSelectedTabelaId] = useState<number>(tenantParameters?.tabelaPrecoIdDefault || 1)

  const [valorUnitarioInput, setValorUnitarioInput] = useState("0.00")
  const [valorDescontoInput, setValorDescontoInput] = useState("0.00")

  const handleDecimalChange = (value: string, setter: (val: string) => void) => {
    let val = value.replace(/,/g, '.')
    val = val.replace(/[^0-9.]/g, '')
    const parts = val.split('.')
    if (parts.length > 2) {
      val = parts[0] + '.' + parts.slice(1).join('')
    }
    setter(val)
  }

  const handleDecimalBlur = (value: string, setter: (val: string) => void, fieldSetter: (val: number) => void) => {
    const parsed = parseFloat(value)
    if (isNaN(parsed)) {
      setter("0.00")
      fieldSetter(0)
    } else {
      setter(parsed.toFixed(2))
      fieldSetter(parsed)
    }
  }

  useEffect(() => {
    searchProdutos(undefined, 100000)
      .then(setProdutos)
      .catch(() => {
        // keep mock fallback on network error
      })
  }, [])

  const filteredProdutos = useMemo(() => {
    const clean = (str: string) => str.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[.\-\/]/g, "")
    const cleanedInput = clean(productInputValue)

    const isShowingSelected = selectedProduto && selectedProduto.nome === productInputValue

    if (!cleanedInput || isShowingSelected) {
      // No search active: show first 30 to avoid DOM overload, user can type to search
      const sliced = produtos.slice(0, 30)
      if (selectedProduto?.id && !sliced.some((p) => p.id === selectedProduto.id)) {
        sliced.push(selectedProduto)
      }
      return sliced
    }

    // Search active: show ALL matching results (no limit) so no product is hidden
    return produtos.filter((p) => {
      const cleanedNome = clean(p.nome)
      const cleanedSku = clean(p.sku)
      return cleanedNome.includes(cleanedInput) || cleanedSku.includes(cleanedInput)
    })
  }, [produtos, productInputValue, selectedProduto])

  const {
    handleSubmit,
    control,
    setValue,
    watch,
    reset,
    formState: { errors },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      quantidade: 1,
      valorDesconto: 0,
      valorAcrescimo: 0,
      naturezaOperacao: '5102',
    },
  })

  const qty = watch('quantidade') || 0
  const price = watch('valorUnitario') || 0
  const disc = watch('valorDesconto') || 0
  const incr = watch('valorAcrescimo') || 0
  
  const subtotalItem = qty * price
  const totalItem = subtotalItem - disc + incr

  const selectedTabelaEntry = useMemo(() => {
    return selectedProduto?.precosPorTabela?.find(p => p.tabelaPrecoIdERP === selectedTabelaId)
  }, [selectedProduto, selectedTabelaId])

  const isSelectedTabelaPromocional = useMemo(() => {
    if (!selectedTabelaEntry) return false
    const hoje = new Date()
    const inicio = selectedTabelaEntry.vigenciaInicio ? new Date(selectedTabelaEntry.vigenciaInicio) : null
    const fim = selectedTabelaEntry.vigenciaFim ? new Date(selectedTabelaEntry.vigenciaFim) : null
    const checkInicio = !inicio || hoje >= inicio
    const checkFim = !fim || hoje <= fim
    return selectedTabelaEntry.isPromocional && checkInicio && checkFim
  }, [selectedTabelaEntry])

  useEffect(() => {
    if (isOpen) {
      if (editingItem) {
        const prod = produtos.find(p => p.id === editingItem.produtoId)
        setSelectedProduto(prod || null)
        setProductInputValue(editingItem.nome)
        
        const tabEntry = prod?.precosPorTabela?.find(p => p.tabelaPrecoEstoqueIdERP === editingItem.tabelaPrecoEstoqueIdERP)
        setSelectedTabelaId(tabEntry?.tabelaPrecoIdERP || tenantParameters?.tabelaPrecoIdDefault || 1)

        setValorUnitarioInput(editingItem.valorUnitario.toFixed(2))
        setValorDescontoInput(editingItem.valorDesconto.toFixed(2))

        reset({
          produtoId: editingItem.produtoId,
          quantidade: editingItem.quantidade,
          valorUnitario: editingItem.valorUnitario,
          valorDesconto: editingItem.valorDesconto,
          valorAcrescimo: editingItem.valorAcrescimo || 0,
          naturezaOperacao: editingItem.naturezaOperacao,
        })
      } else {
        setValorUnitarioInput("0.00")
        setValorDescontoInput("0.00")
        reset({
          produtoId: '',
          quantidade: 1,
          valorUnitario: 0,
          valorDesconto: 0,
          valorAcrescimo: 0,
          naturezaOperacao: '5102',
        })
        setSelectedProduto(null)
        setProductInputValue('')
        setSelectedTabelaId(tenantParameters?.tabelaPrecoIdDefault || 1)
      }
    }
  }, [isOpen, reset, tenantParameters, editingItem, produtos])

  function handleProductChange(id: React.Key | null) {
    const produto = produtos.find((p) => p.id === id)
    if (produto) {
      setSelectedProduto(produto)
      setValue('produtoId', produto.id)
      
      const hoje = new Date()
      const promoVigente = produto.precosPorTabela?.find(p => {
        if (!p.isPromocional) return false
        const inicio = p.vigenciaInicio ? new Date(p.vigenciaInicio) : null
        const fim = p.vigenciaFim ? new Date(p.vigenciaFim) : null
        const checkInicio = !inicio || hoje >= inicio
        const checkFim = !fim || hoje <= fim
        return checkInicio && checkFim
      })

      let precoFinal = produto.precoBase
      let tabelaId = tenantParameters?.tabelaPrecoIdDefault || 1

      if (promoVigente) {
        precoFinal = promoVigente.valorUnitario
        tabelaId = promoVigente.tabelaPrecoIdERP
      } else {
        const precoPadrao = produto.precosPorTabela?.find(p => p.tabelaPrecoIdERP === (tenantParameters?.tabelaPrecoIdDefault || 1))
        if (precoPadrao) {
          precoFinal = precoPadrao.valorUnitario
          tabelaId = precoPadrao.tabelaPrecoIdERP
        }
      }

      setSelectedTabelaId(tabelaId)
      setValue('valorUnitario', precoFinal)
      setValorUnitarioInput(precoFinal.toFixed(2))
      setProductInputValue(produto.nome)
    } else {
      setSelectedProduto(null)
      setValue('produtoId', '')
      setProductInputValue('')
      setValorUnitarioInput("0.00")
    }
  }

  function handleTabelaChange(tabelaId: number) {
    setSelectedTabelaId(tabelaId)
    if (selectedProduto) {
      const precoEncontrado = selectedProduto.precosPorTabela?.find(p => p.tabelaPrecoIdERP === tabelaId)
      if (precoEncontrado) {
        setValue('valorUnitario', precoEncontrado.valorUnitario)
        setValorUnitarioInput(precoEncontrado.valorUnitario.toFixed(2))
      }
    }
  }

  function onSubmit(values: FormValues) {
    if (!selectedProduto) return

    const newItem: ItemPedido = {
      id: editingItem ? editingItem.id : Math.random().toString(36).substring(2, 9),
      produtoId: selectedProduto.id,
      sku: selectedProduto.sku,
      nome: selectedProduto.nome,
      quantidade: values.quantidade,
      valorUnitario: values.valorUnitario,
      valorDesconto: values.valorDesconto,
      valorAcrescimo: values.valorAcrescimo,
      naturezaOperacao: values.naturezaOperacao,
      total: totalItem,
      imagemUrl: selectedProduto.imagemUrl,
      tabelaPrecoEstoqueIdERP: selectedTabelaEntry?.tabelaPrecoEstoqueIdERP || (editingItem?.produtoId === selectedProduto.id ? editingItem.tabelaPrecoEstoqueIdERP : undefined),
    }

    if (editingItem && onEdit) {
      onEdit(newItem)
    } else {
      onAdd(newItem)
    }
    onClose()
  }


  return (
    <Modal 
      isOpen={isOpen} 
      onClose={onClose} 
      size="full"
      radius="none"
      backdrop="blur"
      className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 md:rounded-[3rem] p-0 md:p-2 shadow-2xl text-slate-900 dark:text-slate-100 md:max-w-2xl md:my-auto md:mx-auto"
      scrollBehavior="inside"
      hideCloseButton
    >
      <ModalContent>
        {(onClose) => (
          <>
            <ModalHeader className="p-8 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <div className="h-12 w-12 bg-blue-600 rounded-2xl flex items-center justify-center shadow-xl shadow-blue-500/20">
                         <Package className="h-6 w-6 text-white" />
                    </div>
                    <h2 className="text-2xl font-black italic tracking-tighter text-slate-900 dark:text-white">
                      {editingItem ? 'Editar Item' : 'Gerenciar Item'}
                    </h2>
                </div>
                <Button isIconOnly variant="flat" radius="full" onPress={onClose} className="bg-slate-100 dark:bg-slate-800 text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-700">
                    <X size={24} />
                </Button>
            </ModalHeader>

            <ModalBody className="p-4 md:p-10 space-y-6 md:space-y-8 overflow-y-auto">
              <form id="add-item-form" onSubmit={handleSubmit(onSubmit)} className="space-y-8">
                
                {/* Product Search Selection */}
                <div className="space-y-4">
                  <label className="premium-label tracking-[0.4em]">Produto / Pesquisa</label>
                  <Autocomplete
                      label={null}
                      placeholder="Pesquise por nome ou SKU..."
                      variant="flat"
                      radius="lg"
                      labelPlacement="outside"
                      className="max-w-full"
                      items={filteredProdutos}
                      inputValue={productInputValue}
                      onInputChange={setProductInputValue}
                      selectedKey={selectedProduto?.id || undefined}
                      onSelectionChange={handleProductChange}
                      startContent={<Search className="text-slate-600 h-6 w-6 ml-2" />}
                      inputProps={{
                        classNames: {
                          inputWrapper: "h-20 bg-slate-100 dark:bg-slate-950/80 border border-slate-200 dark:border-slate-800 px-6 shadow-inner focus-within:ring-2 focus-within:ring-blue-500",
                          input: "text-lg font-bold italic tracking-tight text-slate-800 dark:text-slate-200"
                        }
                      }}
                      popoverProps={{
                        radius: "lg",
                        style: { minWidth: 'min(calc(100vw - 2rem), 520px)', width: 'auto' },
                        className: "p-2 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 shadow-[0_20px_50px_rgba(0,0,0,0.15)] dark:shadow-[0_20px_50px_rgba(0,0,0,0.5)] z-[9999]",
                      }}
                    >
                      {(produto) => (
                        <AutocompleteItem 
                          key={produto.id} 
                          textValue={`${produto.nome} ${produto.sku}`} 
                          className="min-h-[5rem] py-3 px-4 rounded-2xl hover:bg-slate-50 dark:hover:bg-slate-800/80 flex items-center shrink-0"
                        >
                          <div className="flex gap-4 items-center w-full">
                            {produto.imagemUrl ? (
                              <Avatar src={produto.imagemUrl} radius="lg" size="md" isBordered className="bg-slate-100 dark:bg-slate-800 border-slate-200 dark:border-slate-700 shrink-0" />
                            ) : (
                              <div className="h-10 w-10 bg-slate-100 dark:bg-slate-800 rounded-lg flex items-center justify-center border border-slate-200 dark:border-slate-700 shrink-0">
                                <Package className="h-5 w-5 text-slate-500 dark:text-slate-400" />
                              </div>
                            )}
                            <div className="flex flex-col gap-1 min-w-0">
                              <span className="text-sm font-black italic text-slate-900 dark:text-slate-200 leading-tight break-words">{produto.nome}</span>
                              <span className="text-xs text-slate-500 font-bold uppercase tracking-widest italic leading-tight break-words">SKU: {produto.sku} • R$ {produto.precoBase.toFixed(2)}</span>
                            </div>
                          </div>
                        </AutocompleteItem>
                      )}
                    </Autocomplete>
                </div>

                {/* Seleção de Tabela de Preço */}
                {selectedProduto && (
                  <div className="space-y-4">
                    <label className="premium-label tracking-[0.4em]">Tabela de Preço</label>
                    {tenantParameters.permiteAlterarTabelaPreco ? (
                      <Select
                        label={null}
                        variant="flat"
                        radius="lg"
                        selectedKeys={[selectedTabelaId.toString()]}
                        onSelectionChange={(keys) => {
                          const key = Array.from(keys)[0];
                          if (key) handleTabelaChange(Number(key));
                        }}
                        classNames={{
                          trigger: "h-20 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 px-6",
                          value: "text-lg font-bold text-slate-800 dark:text-slate-200"
                        }}
                        popoverProps={{
                          radius: "lg",
                          className: "p-2 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 shadow-[0_20px_50px_rgba(0,0,0,0.15)] dark:shadow-[0_20px_50px_rgba(0,0,0,0.5)] z-[9999]",
                        }}
                      >
                        {(selectedProduto.precosPorTabela || []).map((p) => (
                          <SelectItem key={p.tabelaPrecoIdERP.toString()} value={p.tabelaPrecoIdERP.toString()}>
                            {`${p.descricao || 'Tabela ' + p.tabelaPrecoIdERP} - R$ ${p.valorUnitario.toFixed(2)}${p.isPromocional ? ' (Promocional)' : ''}`}
                          </SelectItem>
                        ))}
                      </Select>
                    ) : (
                      <div className="h-20 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 px-6 rounded-2xl flex items-center justify-between">
                        <span className="text-lg font-bold text-slate-800 dark:text-slate-200">
                          {selectedTabelaEntry?.descricao || `Tabela ${selectedTabelaId}`}
                        </span>
                        <span className="text-sm font-bold text-slate-500 uppercase tracking-wider">Apenas Leitura</span>
                      </div>
                    )}
                  </div>
                )}

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 md:gap-8">
                    <Controller
                      name="quantidade"
                      control={control}
                      render={({ field }) => (
                        <div className="space-y-4">
                          <label className="premium-label tracking-[0.4em]">Quantidade</label>
                          <Input
                            {...field}
                            value={field.value?.toString()}
                            type="number"
                            variant="flat"
                            radius="lg"
                            onChange={(e) => field.onChange(Number(e.target.value))}
                            classNames={{
                              inputWrapper: "h-20 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 px-6",
                              input: "text-2xl font-black italic tracking-tighter text-blue-500 font-mono"
                            }}
                          />
                        </div>
                      )}
                    />
                    <Controller
                      name="valorUnitario"
                      control={control}
                      render={({ field }) => (
                        <div className="space-y-4">
                          <div className="flex items-center justify-between">
                            <label className="premium-label tracking-[0.4em]">Valor Unitário</label>
                            {isSelectedTabelaPromocional && (
                              <span className="px-3 py-1 bg-amber-500 text-white text-xs font-black rounded-full animate-pulse">PROMOÇÃO</span>
                            )}
                          </div>
                          <Input
                            type="text"
                            inputMode="decimal"
                            value={valorUnitarioInput}
                            variant="flat"
                            radius="lg"
                            onChange={(e) => {
                              handleDecimalChange(e.target.value, setValorUnitarioInput)
                              const parsed = parseFloat(e.target.value.replace(/,/g, '.'))
                              field.onChange(isNaN(parsed) ? 0 : parsed)
                            }}
                            onBlur={() => {
                              handleDecimalBlur(valorUnitarioInput, setValorUnitarioInput, field.onChange)
                            }}
                            classNames={{
                              inputWrapper: "h-20 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 px-6",
                              input: "text-2xl font-black italic tracking-tighter text-slate-600 dark:text-slate-400 font-mono"
                            }}
                          />
                        </div>
                      )}
                    />
                </div>

                <div className="space-y-4">
                  <label className="premium-label tracking-[0.4em]">Desconto Item</label>
                  <Controller
                    name="valorDesconto"
                    control={control}
                    render={({ field }) => (
                      <Input
                        type="text"
                        inputMode="decimal"
                        value={valorDescontoInput}
                        variant="flat"
                        radius="lg"
                        startContent={<BadgePercent className="h-6 w-6 text-amber-500" />}
                        onChange={(e) => {
                          handleDecimalChange(e.target.value, setValorDescontoInput)
                          const parsed = parseFloat(e.target.value.replace(/,/g, '.'))
                          field.onChange(isNaN(parsed) ? 0 : parsed)
                        }}
                        onBlur={() => {
                          handleDecimalBlur(valorDescontoInput, setValorDescontoInput, field.onChange)
                        }}
                        classNames={{
                          inputWrapper: "h-20 bg-slate-50 dark:bg-slate-950 border border-slate-200 dark:border-slate-800 px-6",
                          input: "text-2xl font-black italic tracking-tighter text-amber-500 font-mono"
                        }}
                      />
                    )}
                  />
                </div>

                {/* Styled Total Row from V3 */}
                <div className="pt-8 border-t border-slate-100 dark:border-slate-800 flex items-center justify-between">
                    <div className="space-y-2">
                        <p className="premium-label italic opacity-50">Subtotal Bruto</p>
                        <p className="text-xl font-bold font-mono text-slate-500 dark:text-slate-400 italic">{subtotalItem.toFixed(2)}</p>
                    </div>
                    <div className="text-right space-y-2">
                        <p className="premium-label tracking-[0.4em] text-blue-500">Valor Final Item</p>
                        <p className="text-5xl font-black font-mono tracking-tighter text-blue-500 italic">{totalItem.toFixed(2)}</p>
                    </div>
                </div>

                <Button 
                    type="submit" 
                    form="add-item-form"
                    className="w-full py-10 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-3xl shadow-2xl shadow-blue-500/20 dark:shadow-blue-900/40 transition-all uppercase tracking-[0.2em] text-xs italic tracking-tighter transform active:scale-95 shadow-inner"
                >
                    {editingItem ? 'Salvar Alterações' : 'Confirmar e Salvar Item'}
                </Button>
              </form>
            </ModalBody>
          </>
        )}
      </ModalContent>
    </Modal>
  )
}
