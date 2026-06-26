import api from './api'
import { Cliente, Produto, TabelaPreco, CondicaoPagamento } from '@/types/vendas'
import { db, type OfflinePedido } from './offlineDb'

// ─── Response shapes matching backend serialization ───────────────────────

export interface PedidoSummary {
  pedidoId: string
  clienteId: string
  criadoEm: string
  status: string
  itensCount: number
  parcelasCount: number
  totalBruto: number
  totalDesconto: number
  totalLiquido: number
  erroDetail?: string
}

export interface PedidoCriado {
  pedidoId: string
  status: string
  itensCount: number
  parcelasCount: number
  totalBruto: number
  totalDesconto: number
  totalLiquido: number
}

export interface CriarPedidoPayload {
  clienteId: string
  itens: {
    produtoId: string
    sku: string
    nome: string
    quantidade: number
    precoUnitario: number
    desconto: number
  }[]
  condicaoPagamento: {
    condicaoPagamentoId: string;
    primeiroVencimento: string; // ISO date string
    formaPagamento: string;
  }
  observacao?: string
}

// ─── Client catalog ───────────────────────────────────────────────────────

interface ClientApiResponse {
  clientId: string
  nome: string
  documento: string
  areaVenda?: string
}

export async function searchClientes(q?: string, limit?: number): Promise<Cliente[]> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  if (isOffline && localDb) {
    console.log('[Offline] Buscando clientes do banco local...')
    const query = q?.toLowerCase() || ''
    const all = await localDb.clientes.toArray()
    all.sort((a, b) => a.nome.localeCompare(b.nome))
    if (!query) return all
    return all.filter((c) => 
      c.nome.toLowerCase().includes(query) || 
      c.documento.includes(query)
    )
  }

  try {
    const params: Record<string, string | number> = {}
    if (q) params.q = q
    if (limit) params.limit = limit
    const { data } = await api.get<ClientApiResponse[]>('/catalogo/clientes', { params })
    const results = data.map((c) => ({
      id: c.clientId,
      nome: c.documento, // Mapeado invertido para corrigir o retorno do banco legado
      documento: c.nome, // Mapeado invertido para corrigir o retorno do banco legado
      areaVenda: c.areaVenda,
    }))

    results.sort((a, b) => a.nome.localeCompare(b.nome))

    return results
  } catch (error) {
    console.warn('[Offline Fallback] Falha ao consultar API de clientes, buscando no local...', error)
    if (localDb) {
      const query = q?.toLowerCase() || ''
      const all = await localDb.clientes.toArray()
      all.sort((a, b) => a.nome.localeCompare(b.nome))
      if (!query) return all
      return all.filter((c) => 
        c.nome.toLowerCase().includes(query) || 
        c.documento.includes(query)
      )
    }
    throw error
  }
}

// ─── Product catalog ──────────────────────────────────────────────────────

interface ProductApiResponse {
  productId: string
  sku: string
  name: string
  unit: string
  price: number
  availableStock: number
}

export async function searchProdutos(q?: string, limit?: number): Promise<Produto[]> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  if (isOffline && localDb) {
    console.log('[Offline] Buscando produtos do banco local...')
    const query = q?.toLowerCase() || ''
    const all = await localDb.produtos.toArray()
    all.sort((a, b) => a.nome.localeCompare(b.nome))
    if (!query) return all
    return all.filter((p) => 
      p.nome.toLowerCase().includes(query) || 
      p.sku.toLowerCase().includes(query)
    )
  }

  try {
    const params: Record<string, string | number> = {}
    if (q) params.q = q
    if (limit) params.limit = limit
    const { data } = await api.get<ProductApiResponse[]>('/catalogo/produtos', { params })
    const results = data.map((p) => ({
      id: p.productId,
      sku: p.sku,
      nome: p.name,
      precoBase: p.price,
    }))

    results.sort((a, b) => a.nome.localeCompare(b.nome))

    return results
  } catch (error) {
    console.warn('[Offline Fallback] Falha ao consultar API de produtos, buscando no local...', error)
    if (localDb) {
      const query = q?.toLowerCase() || ''
      const all = await localDb.produtos.toArray()
      all.sort((a, b) => a.nome.localeCompare(b.nome))
      if (!query) return all
      return all.filter((p) => 
        p.nome.toLowerCase().includes(query) || 
        p.sku.toLowerCase().includes(query)
      )
    }
    throw error
  }
}

export async function getTabelasPreco(): Promise<TabelaPreco[]> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  if (isOffline && localDb) {
    return localDb.tabelasPreco.toArray()
  }

  try {
    const { data } = await api.get<TabelaPreco[]>('/catalogo/tabelas-preco')
    if (typeof window !== 'undefined' && localDb) {
      await localDb.tabelasPreco.clear()
      await localDb.tabelasPreco.bulkPut(data)
    }
    return data
  } catch (error) {
    console.warn('[Offline Fallback] Falha ao consultar API de tabelas de preco, buscando no local...', error)
    if (localDb) {
      return localDb.tabelasPreco.toArray()
    }
    throw error
  }
}

export async function getCondicoesPagamento(): Promise<CondicaoPagamento[]> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  if (isOffline && localDb) {
    return localDb.condicoesPagamento.toArray()
  }

  try {
    const { data } = await api.get<CondicaoPagamento[]>('/catalogo/condicoes-pagamento')
    if (typeof window !== 'undefined' && localDb) {
      await localDb.condicoesPagamento.clear()
      await localDb.condicoesPagamento.bulkPut(data)
    }
    return data
  } catch (error) {
    console.warn('[Offline Fallback] Falha ao consultar API de condicoes de pagamento, buscando no local...', error)
    if (localDb) {
      return localDb.condicoesPagamento.toArray()
    }
    throw error
  }
}

// ─── Orders ───────────────────────────────────────────────────────────────

export async function listPedidosApi(params?: { clienteId?: string; status?: string; page?: number; pageSize?: number }): Promise<PedidoSummary[]> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  // Se estiver offline, serve tudo do IndexedDB local
  if (isOffline && localDb) {
    console.log('[Offline] Listando pedidos do IndexedDB local...')
    const localPedidos = await localDb.pedidos.toArray()
    
    // Filtrar de forma simples localmente se params forem passados
    let filtered = localPedidos
    if (params?.clienteId) {
      filtered = filtered.filter(p => p.clienteId === params.clienteId)
    }
    if (params?.status) {
      filtered = filtered.filter(p => p.status === params.status)
    }

    // Ordena do mais novo para o mais antigo
    filtered.sort((a, b) => new Date(b.criadoEm).getTime() - new Date(a.criadoEm).getTime())

    return filtered.map(p => ({
      pedidoId: p.id,
      clienteId: p.clienteNome, // Exibe o nome do cliente na UI
      criadoEm: p.criadoEm,
      status: p.status,
      itensCount: p.itens.length,
      parcelasCount: 1,
      totalBruto: p.totalLiquido,
      totalDesconto: 0,
      totalLiquido: p.totalLiquido,
      erroDetail: p.erroSyncDetail
    }))
  }

  try {
    const { data: apiPedidos } = await api.get<PedidoSummary[]>('/pedidos', { params })

    if (localDb && typeof window !== 'undefined') {
      // 1. Obter os pedidos locais pendentes de sincronização
      const pendingPedidos = await localDb.pedidos
        .filter(p => p.status === 'pendente_sync' || p.status === 'erro_sync')
        .toArray()

      // 2. Cachear os pedidos recebidos da API (com status 'sincronizado')
      const localIds = apiPedidos.map(p => p.pedidoId)
      await localDb.transaction('rw', [localDb.pedidos, localDb.clientes], async () => {
        // Remove os que agora estão vindo do servidor para atualizar
        await localDb.pedidos.bulkDelete(localIds)
        
        const toCache: OfflinePedido[] = []
        for (const apiPed of apiPedidos) {
          const cliente = await localDb.clientes.get(apiPed.clienteId)
          toCache.push({
            id: apiPed.pedidoId,
            clienteId: apiPed.clienteId,
            clienteNome: cliente?.nome || 'Cliente Sincronizado',
            itens: [], 
            condicaoPagamento: { condicaoPagamentoId: 'avista', primeiroVencimento: apiPed.criadoEm, formaPagamento: 'dinheiro' },
            status: 'sincronizado',
            totalLiquido: apiPed.totalLiquido,
            criadoEm: apiPed.criadoEm
          })
        }
        if (toCache.length > 0) {
          await localDb.pedidos.bulkPut(toCache)
        }
      })

      // 3. Mesclar pedidos locais não-sincronizados no topo da listagem e resolver os nomes dos clientes
      const mappedPending: PedidoSummary[] = pendingPedidos.map(p => ({
        pedidoId: p.id,
        clienteId: p.clienteNome, // Usa o nome legível
        criadoEm: p.criadoEm,
        status: p.status,
        itensCount: p.itens.length,
        parcelasCount: 1,
        totalBruto: p.totalLiquido,
        totalDesconto: 0,
        totalLiquido: p.totalLiquido,
        erroDetail: p.erroSyncDetail
      }))

      // Mapear os pedidos recebidos da API para também resolver o nome do cliente se estiver em cache local
      const mappedApi: PedidoSummary[] = []
      for (const apiPed of apiPedidos) {
        const cliente = await localDb.clientes.get(apiPed.clienteId)
        mappedApi.push({
          ...apiPed,
          clienteId: cliente ? cliente.nome : apiPed.clienteId
        })
      }

      // Combinar e ordenar por data decrescente
      const combined = [...mappedPending, ...mappedApi]
      combined.sort((a, b) => new Date(b.criadoEm).getTime() - new Date(a.criadoEm).getTime())
      return combined
    }

    return apiPedidos
  } catch (error) {
    console.warn('[Offline Fallback] Falha ao listar pedidos da API. Retornando dados locais...', error)
    if (localDb) {
      const localPedidos = await localDb.pedidos.toArray()
      let filtered = localPedidos
      if (params?.clienteId) {
        filtered = filtered.filter(p => p.clienteId === params.clienteId)
      }
      if (params?.status) {
        filtered = filtered.filter(p => p.status === params.status)
      }
      filtered.sort((a, b) => new Date(b.criadoEm).getTime() - new Date(a.criadoEm).getTime())

      return filtered.map(p => ({
        pedidoId: p.id,
        clienteId: p.clienteNome,
        criadoEm: p.criadoEm,
        status: p.status,
        itensCount: p.itens.length,
        parcelasCount: 1,
        totalBruto: p.totalLiquido,
        totalDesconto: 0,
        totalLiquido: p.totalLiquido,
        erroDetail: p.erroSyncDetail
      }))
    }
    throw error
  }
}

export async function criarPedidoApi(payload: CriarPedidoPayload): Promise<PedidoCriado> {
  const localDb = db
  const isOffline = typeof window !== 'undefined' && !navigator.onLine

  if (isOffline && localDb) {
    console.log('[Offline] Gravando pedido localmente no IndexedDB...')
    const pedidoId = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `off_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`
    
    const totalBruto = payload.itens.reduce((acc, item) => acc + (item.quantidade * item.precoUnitario), 0)
    const totalDesconto = payload.itens.reduce((acc, item) => acc + item.desconto, 0)
    const totalLiquido = totalBruto - totalDesconto

    const cliente = await localDb.clientes.get(payload.clienteId)
    const clienteNome = cliente?.nome || 'Cliente Local'

    const offlinePedido: OfflinePedido = {
      id: pedidoId,
      clienteId: payload.clienteId,
      clienteNome: clienteNome,
      itens: payload.itens,
      condicaoPagamento: payload.condicaoPagamento,
      observacao: payload.observacao,
      status: 'pendente_sync',
      totalLiquido,
      criadoEm: new Date().toISOString()
    }

    await localDb.pedidos.put(offlinePedido)
    console.log('[Offline] Pedido gravado localmente com ID:', pedidoId)

    return {
      pedidoId,
      status: 'offline',
      itensCount: payload.itens.length,
      parcelasCount: 1,
      totalBruto,
      totalDesconto,
      totalLiquido
    }
  }

  try {
    const { data } = await api.post<PedidoCriado>('/pedidos', payload)
    return data
  } catch (error) {
    console.warn('[Offline Fallback] Erro ao enviar pedido ao backend. Salvando localmente como contingência...', error)
    if (localDb) {
      const pedidoId = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : `off_${Date.now()}_${Math.random().toString(36).substring(2, 9)}`
      
      const totalBruto = payload.itens.reduce((acc, item) => acc + (item.quantidade * item.precoUnitario), 0)
      const totalDesconto = payload.itens.reduce((acc, item) => acc + item.desconto, 0)
      const totalLiquido = totalBruto - totalDesconto

      const cliente = await localDb.clientes.get(payload.clienteId)
      const clienteNome = cliente?.nome || 'Cliente Local'

      const offlinePedido: OfflinePedido = {
        id: pedidoId,
        clienteId: payload.clienteId,
        clienteNome: clienteNome,
        itens: payload.itens,
        condicaoPagamento: payload.condicaoPagamento,
        observacao: payload.observacao,
        status: 'pendente_sync',
        totalLiquido,
        criadoEm: new Date().toISOString(),
        erroSyncDetail: 'Falha na conexão inicial de rede'
      }

      await localDb.pedidos.put(offlinePedido)
      return {
        pedidoId,
        status: 'offline',
        itensCount: payload.itens.length,
        parcelasCount: 1,
        totalBruto,
        totalDesconto,
        totalLiquido
      }
    }
    throw error
  }
}
