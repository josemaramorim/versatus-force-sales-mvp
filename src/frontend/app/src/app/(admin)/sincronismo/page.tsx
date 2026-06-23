'use client'

import React, { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { db } from '@/lib/offlineDb'
import { syncCatalogLocal, getLastSyncTime } from '@/lib/syncCatalog'
import { searchClientes, searchProdutos, getTabelasPreco, getCondicoesPagamento } from '@/lib/vendaApi'
import { 
  RefreshCw, 
  CheckCircle2, 
  Database, 
  Users, 
  ShoppingBag, 
  Tag, 
  CreditCard, 
  ArrowLeft
} from 'lucide-react'
import { Button, Card, CardBody } from '@nextui-org/react'

export default function SincronismoPage() {
  const router = useRouter()
  const [isSyncingAll, setIsSyncingAll] = useState(false)
  const [lastSyncTime, setLastSyncTime] = useState<string | null>(null)
  
  // Counts
  const [counts, setCounts] = useState({
    clientes: 0,
    produtos: 0,
    tabelasPreco: 0,
    condicoesPagamento: 0
  })

  // Loading states for individual tables
  const [syncStates, setSyncStates] = useState<Record<string, 'idle' | 'syncing' | 'success' | 'error'>>({
    clientes: 'idle',
    produtos: 'idle',
    tabelasPreco: 'idle',
    condicoesPagamento: 'idle'
  })

  async function loadStats() {
    if (typeof window === 'undefined' || !db) return
    try {
      const [c, p, tp, cp] = await Promise.all([
        db.clientes.count(),
        db.produtos.count(),
        db.tabelasPreco.count(),
        db.condicoesPagamento.count()
      ])
      setCounts({
        clientes: c,
        produtos: p,
        tabelasPreco: tp,
        condicoesPagamento: cp
      })
      setLastSyncTime(getLastSyncTime())
    } catch (err) {
      console.error('Error loading offline DB stats:', err)
    }
  }

  useEffect(() => {
    loadStats()
  }, [])

  async function handleSyncAll() {
    setIsSyncingAll(true)
    setSyncStates({
      clientes: 'syncing',
      produtos: 'syncing',
      tabelasPreco: 'syncing',
      condicoesPagamento: 'syncing'
    })

    try {
      const success = await syncCatalogLocal()
      if (success) {
        setSyncStates({
          clientes: 'success',
          produtos: 'success',
          tabelasPreco: 'success',
          condicoesPagamento: 'success'
        })
        await loadStats()
      } else {
        setSyncStates({
          clientes: 'error',
          produtos: 'error',
          tabelasPreco: 'error',
          condicoesPagamento: 'error'
        })
      }
    } catch {
      setSyncStates({
        clientes: 'error',
        produtos: 'error',
        tabelasPreco: 'error',
        condicoesPagamento: 'error'
      })
    } finally {
      setIsSyncingAll(false)
    }
  }

  async function handleSyncIndividual(table: 'clientes' | 'produtos' | 'tabelasPreco' | 'condicoesPagamento') {
    setSyncStates(prev => ({ ...prev, [table]: 'syncing' }))
    try {
      if (table === 'clientes') {
        const data = await searchClientes(undefined, 100000)
        if (db) {
          await db.clientes.clear()
          if (data.length > 0) await db.clientes.bulkPut(data)
        }
      } else if (table === 'produtos') {
        const data = await searchProdutos(undefined, 100000)
        if (db) {
          await db.produtos.clear()
          if (data.length > 0) await db.produtos.bulkPut(data)
        }
      } else if (table === 'tabelasPreco') {
        await getTabelasPreco()
      } else if (table === 'condicoesPagamento') {
        await getCondicoesPagamento()
      }

      setSyncStates(prev => ({ ...prev, [table]: 'success' }))
      const now = new Date().toISOString()
      localStorage.setItem('versatus_last_sync_catalog', now)
      await loadStats()
    } catch (err) {
      console.error(`Error syncing table ${table}:`, err)
      setSyncStates(prev => ({ ...prev, [table]: 'error' }))
    }
  }

  const formattedDate = lastSyncTime 
    ? new Date(lastSyncTime).toLocaleString('pt-BR') 
    : 'Nunca sincronizado'

  return (
    <div className="space-y-12 max-w-5xl mx-auto">
      
      {/* Header */}
      <header className="flex flex-col sm:flex-row items-center sm:justify-between gap-6 pb-8 border-b border-slate-100 dark:border-slate-900 leading-none">
        <div className="flex flex-col sm:flex-row items-center gap-4 lg:gap-6 text-center sm:text-left">
          <Button 
            isIconOnly
            variant="light"
            radius="full"
            onPress={() => router.back()}
            className="text-slate-500 hover:text-slate-800 dark:hover:text-slate-200"
          >
            <ArrowLeft className="h-6 w-6" />
          </Button>
          <div className="min-w-0">
            <h1 className="text-2xl lg:text-4xl premium-title leading-none truncate pr-2">Sincronização do Catálogo</h1>
            <p className="text-[10px] font-black uppercase tracking-[0.2em] lg:tracking-[0.4em] text-slate-500 mt-2 italic">Gerenciamento Offline e Integridade de Dados</p>
          </div>
        </div>
        <div className="flex items-center space-x-4 shrink-0 bg-slate-50 dark:bg-slate-900/50 px-6 py-4 rounded-[1.5rem] border border-slate-100 dark:border-slate-800">
          <Database className="h-5 w-5 text-blue-500" />
          <div>
            <p className="text-[10px] text-slate-400 font-bold uppercase tracking-wider">Última Atualização Geral</p>
            <p className="text-xs font-black font-mono text-slate-700 dark:text-slate-300">{formattedDate}</p>
          </div>
        </div>
      </header>

      {/* Main Actions & Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
        
        {/* Unified Sync Card */}
        <Card className="premium-card md:col-span-1 border-none shadow-xl bg-slate-50/50 dark:bg-slate-900/50">
          <CardBody className="p-8 flex flex-col justify-between min-h-[300px]">
            <div className="space-y-4">
              <h2 className="text-lg font-black italic text-slate-800 dark:text-white flex items-center gap-2">
                <RefreshCw className={`h-5 w-5 text-blue-500 ${isSyncingAll ? 'animate-spin' : ''}`} />
                Sincronismo Geral
              </h2>
              <p className="text-xs font-bold leading-relaxed text-slate-500 dark:text-slate-450">
                Baixe e atualize de forma segura todas as entidades do catálogo corporativo (clientes, produtos, preços e condições) em um único bloco transacionado.
              </p>
            </div>
            
            <div className="space-y-4 pt-6">
              <Button
                fullWidth
                size="lg"
                onPress={handleSyncAll}
                isLoading={isSyncingAll}
                className="bg-gradient-to-r from-blue-600 to-indigo-700 text-white font-black rounded-2xl py-6 uppercase tracking-wider text-xs shadow-lg shadow-blue-500/20 active:scale-95 transition-all transform"
              >
                Sincronizar Catálogo Completo
              </Button>
            </div>
          </CardBody>
        </Card>

        {/* Database Status Cards */}
        <div className="md:col-span-2 grid grid-cols-1 sm:grid-cols-2 gap-6">
          
          {/* Clientes Card */}
          <Card className="premium-card p-6 flex flex-col justify-between border-none shadow-md bg-slate-50/50 dark:bg-slate-900/50">
            <div className="flex justify-between items-start">
              <div className="flex items-center gap-4">
                <div className="p-4 bg-blue-500/10 text-blue-500 rounded-2xl">
                  <Users className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="font-black text-slate-800 dark:text-white">Clientes</h3>
                  <p className="text-xs text-slate-500 font-bold">Filtros de carteira e área de venda</p>
                </div>
              </div>
              <span className={`text-[10px] font-black uppercase tracking-wider px-3 py-1 rounded-full ${
                syncStates.clientes === 'syncing' ? 'bg-amber-500/10 text-amber-500 animate-pulse' :
                syncStates.clientes === 'success' ? 'bg-emerald-500/10 text-emerald-500' :
                syncStates.clientes === 'error' ? 'bg-rose-500/10 text-rose-500' :
                'bg-slate-100 dark:bg-slate-800 text-slate-500'
              }`}>
                {syncStates.clientes === 'syncing' ? 'Sync...' : syncStates.clientes === 'success' ? 'Sucesso' : syncStates.clientes === 'error' ? 'Falhou' : 'Pronto'}
              </span>
            </div>
            <div className="flex items-baseline justify-between mt-8">
              <div className="flex flex-col">
                <span className="text-3xl font-black font-mono text-blue-500">{counts.clientes}</span>
                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest">Registros Locais</span>
              </div>
              <Button 
                size="sm"
                variant="bordered"
                isDisabled={isSyncingAll}
                className="border-slate-200 dark:border-slate-800 text-slate-650 dark:text-slate-350 font-black rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-xs py-4 px-4"
                onPress={() => handleSyncIndividual('clientes')}
              >
                Sync
              </Button>
            </div>
          </Card>

          {/* Produtos Card */}
          <Card className="premium-card p-6 flex flex-col justify-between border-none shadow-md bg-slate-50/50 dark:bg-slate-900/50">
            <div className="flex justify-between items-start">
              <div className="flex items-center gap-4">
                <div className="p-4 bg-emerald-500/10 text-emerald-500 rounded-2xl">
                  <ShoppingBag className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="font-black text-slate-800 dark:text-white">Produtos</h3>
                  <p className="text-xs text-slate-500 font-bold">Catálogo geral com saldo de estoque</p>
                </div>
              </div>
              <span className={`text-[10px] font-black uppercase tracking-wider px-3 py-1 rounded-full ${
                syncStates.produtos === 'syncing' ? 'bg-amber-500/10 text-amber-500 animate-pulse' :
                syncStates.produtos === 'success' ? 'bg-emerald-500/10 text-emerald-500' :
                syncStates.produtos === 'error' ? 'bg-rose-500/10 text-rose-500' :
                'bg-slate-100 dark:bg-slate-800 text-slate-500'
              }`}>
                {syncStates.produtos === 'syncing' ? 'Sync...' : syncStates.produtos === 'success' ? 'Sucesso' : syncStates.produtos === 'error' ? 'Falhou' : 'Pronto'}
              </span>
            </div>
            <div className="flex items-baseline justify-between mt-8">
              <div className="flex flex-col">
                <span className="text-3xl font-black font-mono text-emerald-500">{counts.produtos}</span>
                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest">Registros Locais</span>
              </div>
              <Button 
                size="sm"
                variant="bordered"
                isDisabled={isSyncingAll}
                className="border-slate-200 dark:border-slate-800 text-slate-650 dark:text-slate-350 font-black rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-xs py-4 px-4"
                onPress={() => handleSyncIndividual('produtos')}
              >
                Sync
              </Button>
            </div>
          </Card>

          {/* Tabelas de Preço Card */}
          <Card className="premium-card p-6 flex flex-col justify-between border-none shadow-md bg-slate-50/50 dark:bg-slate-900/50">
            <div className="flex justify-between items-start">
              <div className="flex items-center gap-4">
                <div className="p-4 bg-amber-500/10 text-amber-500 rounded-2xl">
                  <Tag className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="font-black text-slate-800 dark:text-white">Tabelas de Preço</h3>
                  <p className="text-xs text-slate-500 font-bold">Preços unitários e desconto máximo</p>
                </div>
              </div>
              <span className={`text-[10px] font-black uppercase tracking-wider px-3 py-1 rounded-full ${
                syncStates.tabelasPreco === 'syncing' ? 'bg-amber-500/10 text-amber-500 animate-pulse' :
                syncStates.tabelasPreco === 'success' ? 'bg-emerald-500/10 text-emerald-500' :
                syncStates.tabelasPreco === 'error' ? 'bg-rose-500/10 text-rose-500' :
                'bg-slate-100 dark:bg-slate-800 text-slate-500'
              }`}>
                {syncStates.tabelasPreco === 'syncing' ? 'Sync...' : syncStates.tabelasPreco === 'success' ? 'Sucesso' : syncStates.tabelasPreco === 'error' ? 'Falhou' : 'Pronto'}
              </span>
            </div>
            <div className="flex items-baseline justify-between mt-8">
              <div className="flex flex-col">
                <span className="text-3xl font-black font-mono text-amber-500">{counts.tabelasPreco}</span>
                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest">Registros Locais</span>
              </div>
              <Button 
                size="sm"
                variant="bordered"
                isDisabled={isSyncingAll}
                className="border-slate-200 dark:border-slate-800 text-slate-650 dark:text-slate-350 font-black rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-xs py-4 px-4"
                onPress={() => handleSyncIndividual('tabelasPreco')}
              >
                Sync
              </Button>
            </div>
          </Card>

          {/* Condições de Pagamento Card */}
          <Card className="premium-card p-6 flex flex-col justify-between border-none shadow-md bg-slate-50/50 dark:bg-slate-900/50">
            <div className="flex justify-between items-start">
              <div className="flex items-center gap-4">
                <div className="p-4 bg-indigo-500/10 text-indigo-500 rounded-2xl">
                  <CreditCard className="h-6 w-6" />
                </div>
                <div>
                  <h3 className="font-black text-slate-800 dark:text-white">Condições Pagto</h3>
                  <p className="text-xs text-slate-500 font-bold">Prazos, taxas de acréscimo e desconto</p>
                </div>
              </div>
              <span className={`text-[10px] font-black uppercase tracking-wider px-3 py-1 rounded-full ${
                syncStates.condicoesPagamento === 'syncing' ? 'bg-amber-500/10 text-amber-500 animate-pulse' :
                syncStates.condicoesPagamento === 'success' ? 'bg-emerald-500/10 text-emerald-500' :
                syncStates.condicoesPagamento === 'error' ? 'bg-rose-500/10 text-rose-500' :
                'bg-slate-100 dark:bg-slate-800 text-slate-500'
              }`}>
                {syncStates.condicoesPagamento === 'syncing' ? 'Sync...' : syncStates.condicoesPagamento === 'success' ? 'Sucesso' : syncStates.condicoesPagamento === 'error' ? 'Falhou' : 'Pronto'}
              </span>
            </div>
            <div className="flex items-baseline justify-between mt-8">
              <div className="flex flex-col">
                <span className="text-3xl font-black font-mono text-indigo-500">{counts.condicoesPagamento}</span>
                <span className="text-[9px] font-black text-slate-400 uppercase tracking-widest">Registros Locais</span>
              </div>
              <Button 
                size="sm"
                variant="bordered"
                isDisabled={isSyncingAll}
                className="border-slate-200 dark:border-slate-800 text-slate-650 dark:text-slate-350 font-black rounded-xl hover:bg-slate-100 dark:hover:bg-slate-800 text-xs py-4 px-4"
                onPress={() => handleSyncIndividual('condicoesPagamento')}
              >
                Sync
              </Button>
            </div>
          </Card>

        </div>
      </div>
    </div>
  )
}
