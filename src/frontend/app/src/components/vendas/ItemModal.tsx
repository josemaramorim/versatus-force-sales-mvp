'use client'

import React, { useState, useEffect } from 'react'
import { useForm, Controller } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { MOCK_PRODUTOS, MOCK_NATUREZAS } from '@/lib/mocks'
import { ItemPedido, Produto } from '@/types/vendas'
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
}

export function ItemModal({ isOpen, onClose, onAdd }: ItemModalProps) {
  const [selectedProduto, setSelectedProduto] = useState<Produto | null>(null)

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

  useEffect(() => {
    if (isOpen) {
      reset({
        quantidade: 1,
        valorDesconto: 0,
        valorAcrescimo: 0,
        naturezaOperacao: '5102',
      })
      setSelectedProduto(null)
    }
  }, [isOpen, reset])

  function handleProductChange(id: React.Key | null) {
    const produto = MOCK_PRODUTOS.find((p) => p.id === id)
    if (produto) {
      setSelectedProduto(produto)
      setValue('produtoId', produto.id)
      setValue('valorUnitario', produto.precoBase)
    } else {
      setSelectedProduto(null)
      setValue('produtoId', '')
    }
  }

  function onSubmit(values: FormValues) {
    if (!selectedProduto) return

    const newItem: ItemPedido = {
      id: Math.random().toString(36).substring(2, 9),
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
    }

    onAdd(newItem)
    onClose()
  }

  return (
    <Modal 
      isOpen={isOpen} 
      onClose={onClose} 
      size="2xl"
      radius="none"
      backdrop="blur"
      className="bg-slate-900 border border-slate-800 rounded-[3rem] p-2 dark shadow-2xl"
      scrollBehavior="outside"
      hideCloseButton
    >
      <ModalContent>
        {(onClose) => (
          <>
            <ModalHeader className="p-8 border-b border-slate-800 flex items-center justify-between">
                <div className="flex items-center gap-4">
                    <div className="h-12 w-12 bg-blue-600 rounded-2xl flex items-center justify-center shadow-xl shadow-blue-500/20">
                         <Package className="h-6 w-6 text-white" />
                    </div>
                    <h2 className="text-2xl font-black italic tracking-tighter">Gerenciar Item</h2>
                </div>
                <Button isIconOnly variant="flat" radius="full" onPress={onClose} className="bg-slate-800 text-slate-500 hover:bg-slate-700">
                    <X size={24} />
                </Button>
            </ModalHeader>

            <ModalBody className="p-10 space-y-8">
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
                      defaultItems={MOCK_PRODUTOS}
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
                        className: "p-2 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 shadow-[0_20px_50px_rgba(0,0,0,0.5)] z-[9999]",
                      }}
                    >
                      {(produto) => (
                        <AutocompleteItem key={produto.id} textValue={produto.nome} className="p-4 rounded-2xl">
                          <div className="flex gap-4 items-center">
                            <Avatar src={produto.imagemUrl} radius="lg" size="md" isBordered className="bg-slate-800 border-slate-700" />
                            <div className="flex flex-col gap-1">
                              <span className="text-base font-black italic text-slate-200 leading-none">{produto.nome}</span>
                              <span className="text-[10px] text-slate-500 font-bold uppercase tracking-widest italic">SKU: {produto.sku} • R$ {produto.precoBase.toFixed(2)}</span>
                            </div>
                          </div>
                        </AutocompleteItem>
                      )}
                    </Autocomplete>
                </div>

                <div className="grid grid-cols-2 gap-8">
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
                              inputWrapper: "h-20 bg-slate-950 border border-slate-800 px-6",
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
                          <label className="premium-label tracking-[0.4em]">Valor Unitário</label>
                          <Input
                            {...field}
                            value={field.value?.toString()}
                            type="number"
                            variant="flat"
                            radius="lg"
                            onChange={(e) => field.onChange(Number(e.target.value))}
                            classNames={{
                              inputWrapper: "h-20 bg-slate-950 border border-slate-800 px-6",
                              input: "text-2xl font-black italic tracking-tighter text-slate-400 font-mono"
                            }}
                          />
                        </div>
                      )}
                    />
                </div>

                <div className="space-y-4">
                  <label className="premium-label tracking-[0.4em]">Desconto Item (R$)</label>
                  <Controller
                    name="valorDesconto"
                    control={control}
                    render={({ field }) => (
                      <Input
                        {...field}
                        value={field.value?.toString()}
                        type="number"
                        variant="flat"
                        radius="lg"
                        startContent={<BadgePercent className="h-6 w-6 text-amber-500" />}
                        onChange={(e) => field.onChange(Number(e.target.value))}
                        classNames={{
                          inputWrapper: "h-20 bg-slate-950 border border-slate-800 px-6",
                          input: "text-2xl font-black italic tracking-tighter text-amber-500 font-mono"
                        }}
                      />
                    )}
                  />
                </div>

                {/* Styled Total Row from V3 */}
                <div className="pt-8 border-t border-slate-800 flex items-center justify-between">
                    <div className="space-y-2">
                        <p className="premium-label italic opacity-50">Subtotal Bruto</p>
                        <p className="text-xl font-bold font-mono text-slate-600 italic">R$ {subtotalItem.toFixed(2)}</p>
                    </div>
                    <div className="text-right space-y-2">
                        <p className="premium-label tracking-[0.4em] text-blue-500">Valor Final Item</p>
                        <p className="text-5xl font-black font-mono tracking-tighter text-blue-500 italic">R$ {totalItem.toFixed(2)}</p>
                    </div>
                </div>

                <Button 
                    type="submit" 
                    form="add-item-form"
                    className="w-full py-10 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-3xl shadow-2xl shadow-blue-900/40 transition-all uppercase tracking-[0.2em] text-xs italic tracking-tighter transform active:scale-95 shadow-inner"
                >
                    Confirmar e Salvar Item
                </Button>
              </form>
            </ModalBody>
          </>
        )}
      </ModalContent>
    </Modal>
  )
}
