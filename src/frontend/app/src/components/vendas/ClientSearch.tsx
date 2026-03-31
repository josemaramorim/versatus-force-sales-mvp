'use client'

import React from 'react'
import { Autocomplete, AutocompleteItem, Avatar } from '@nextui-org/react'
import { MOCK_CLIENTES } from '@/lib/mocks'
import { Cliente } from '@/types/vendas'
import { Search } from 'lucide-react'

interface ClientSearchProps {
  onSelect: (cliente: Cliente) => void
  selectedId?: string | null
}

export function ClientSearch({ onSelect, selectedId }: ClientSearchProps) {
  const onSelectionChange = (id: React.Key | null) => {
    const cliente = MOCK_CLIENTES.find((c) => c.id === id)
    if (cliente) onSelect(cliente)
  }

  return (
    <div className="w-full">
      <Autocomplete
        label={null}
        placeholder="Pesquisar por nome ou CNPJ..."
        variant="flat"
        radius="none"
        labelPlacement="outside"
        className="max-w-full"
        defaultItems={MOCK_CLIENTES}
        selectedKey={selectedId || undefined}
        onSelectionChange={onSelectionChange}
        startContent={<Search className="text-slate-600 h-6 w-6 ml-2" />}
        inputProps={{
          classNames: {
            input: "text-lg font-bold text-slate-800 dark:text-slate-200 tracking-tight placeholder:text-slate-600 placeholder:italic",
            inputWrapper: "h-20 bg-slate-100 dark:bg-slate-950/80 border border-slate-200 dark:border-slate-800 rounded-[2rem] px-6 shadow-inner hover:border-blue-500 transition-all focus-within:ring-2 focus-within:ring-blue-500/30",
          },
        }}
        popoverProps={{
          radius: "lg",
          className: "p-2 bg-white dark:bg-slate-900 border-2 border-slate-200 dark:border-slate-800 shadow-[0_20px_50px_rgba(0,0,0,0.2)] dark:shadow-[0_20px_50px_rgba(0,0,0,0.5)] z-[9999]",
        }}
      >
        {(cliente) => (
          <AutocompleteItem 
            key={cliente.id} 
            textValue={cliente.nome}
            className="p-4 rounded-2xl hover:bg-slate-50 dark:hover:bg-slate-800/80"
          >
            <div className="flex gap-4 items-center">
              <Avatar 
                radius="lg" 
                size="md" 
                name={cliente.nome.charAt(0)}
                color="primary"
                isBordered
                className="bg-blue-600 text-white font-black"
              />
              <div className="flex flex-col gap-1">
                <span className="text-base font-black italic text-slate-900 dark:text-white leading-none">{cliente.nome}</span>
                <span className="text-[10px] text-slate-500 font-bold uppercase tracking-[0.3em] italic">
                  {cliente.documento} {cliente.areaVenda ? `• ${cliente.areaVenda}` : ''}
                </span>
              </div>
            </div>
          </AutocompleteItem>
        )}
      </Autocomplete>
    </div>
  )
}
