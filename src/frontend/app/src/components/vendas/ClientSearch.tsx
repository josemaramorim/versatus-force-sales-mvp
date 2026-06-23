'use client'

import React, { useState, useEffect, useMemo } from 'react'
import { Autocomplete, AutocompleteItem, Avatar } from '@nextui-org/react'
import { searchClientes } from '@/lib/vendaApi'
import { Cliente } from '@/types/vendas'
import { Search, RefreshCw } from 'lucide-react'
import { syncCatalogLocal } from '@/lib/syncCatalog'

interface ClientSearchProps {
  onSelect: (cliente: Cliente) => void
  selectedId?: string | null
}

export function ClientSearch({ onSelect, selectedId }: ClientSearchProps) {
  const [clientes, setClientes] = useState<Cliente[]>([])
  const [isSyncing, setIsSyncing] = useState(false)
  const [inputValue, setInputValue] = useState('')

  useEffect(() => {
    searchClientes()
      .then(setClientes)
      .catch(() => {
        // keep mock fallback on network error
      })
  }, [])

  // Sync input value with selected client name when selection changes externally/internally
  useEffect(() => {
    if (selectedId) {
      const cliente = clientes.find((c) => c.id === selectedId)
      if (cliente) {
        setInputValue(cliente.nome)
      }
    } else {
      setInputValue('')
    }
  }, [selectedId, clientes])

  const filteredClientes = useMemo(() => {
    const clean = (str: string) => str.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[.\-\/]/g, "")
    const cleanedInput = clean(inputValue)

    if (!cleanedInput) return clientes

    // If selected client matches current inputValue, do not filter so user can see all when they click the dropdown
    const selectedCliente = clientes.find((c) => c.id === selectedId)
    if (selectedCliente && selectedCliente.nome === inputValue) {
      return clientes
    }

    return clientes.filter((c) => {
      const cleanedNome = clean(c.nome)
      const cleanedDoc = clean(c.documento)
      return cleanedNome.includes(cleanedInput) || cleanedDoc.includes(cleanedInput)
    })
  }, [clientes, inputValue, selectedId])

  const onSelectionChange = (id: React.Key | null) => {
    const cliente = clientes.find((c) => c.id === id)
    if (cliente) {
      onSelect(cliente)
    } else {
      onSelect(null as any) // pass null if cleared
    }
  }

  const handleSync = async () => {
    setIsSyncing(true)
    try {
      const success = await syncCatalogLocal()
      if (success) {
        const updatedClientes = await searchClientes()
        setClientes(updatedClientes)
      }
    } catch (err) {
      console.error('[ClientSearch] Sync error:', err)
    } finally {
      setIsSyncing(false)
    }
  }

  return (
    <div className="w-full flex items-center gap-4">
      <div className="flex-1">
        <Autocomplete
          label={null}
          placeholder="Pesquisar por nome ou CNPJ..."
          variant="flat"
          radius="none"
          labelPlacement="outside"
          className="max-w-full"
          items={filteredClientes}
          inputValue={inputValue}
          onInputChange={setInputValue}
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
              textValue={`${cliente.nome} ${cliente.documento}`}
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
      <button
        type="button"
        onClick={handleSync}
        disabled={isSyncing}
        className="h-20 w-20 shrink-0 flex flex-col items-center justify-center bg-slate-100 dark:bg-slate-950/80 border border-slate-200 dark:border-slate-800 rounded-[2rem] hover:border-blue-500 hover:text-blue-500 hover:dark:text-blue-400 focus:outline-none transition-all active:scale-95 text-slate-500 disabled:opacity-50"
        title="Sincronizar todo o catálogo"
      >
        <RefreshCw className={`h-6 w-6 ${isSyncing ? 'animate-spin text-blue-500' : ''}`} />
        <span className="text-[8px] font-black tracking-tighter uppercase mt-1 leading-none">Sync</span>
      </button>
    </div>
  )
}
