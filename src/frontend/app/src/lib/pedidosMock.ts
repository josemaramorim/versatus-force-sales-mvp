import { PedidoDraft, ItemPedido } from '@/types/vendas'

export interface PedidoPersist {
  id: string
  clienteId: string | null
  itens: ItemPedido[]
  observacoes: string
  condicaoPagamentoId: string
  descontoGlobal: number
  acrescimoGlobal: number
  subtotal: number
  totalFinal: number
  status: string
  criadoEm: string
}

const STORAGE_KEY = 'versatus_mock_pedidos_v1'

function readStorage(): PedidoPersist[] {
  if (typeof window === 'undefined') return []
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw ? JSON.parse(raw) : []
  } catch {
    return []
  }
}

function writeStorage(items: PedidoPersist[]) {
  if (typeof window === 'undefined') return
  localStorage.setItem(STORAGE_KEY, JSON.stringify(items))
}

export function listPedidos(): Promise<PedidoPersist[]> {
  return new Promise((res) => {
    const items = readStorage()
    // simulate network latency
    setTimeout(() => res(items.reverse()), 200)
  })
}

export function createPedido(draft: PedidoDraft): Promise<PedidoPersist> {
  return new Promise((res) => {
    const items = readStorage()
    const id = `#${(Math.floor(Math.random() * 900000) + 100000).toString()}`
    const now = new Date().toISOString()
    const novo: PedidoPersist = {
      id,
      clienteId: draft.clienteId,
      itens: draft.itens,
      observacoes: draft.observacoes,
      condicaoPagamentoId: draft.condicaoPagamentoId,
      descontoGlobal: draft.descontoGlobal,
      acrescimoGlobal: draft.acrescimoGlobal,
      subtotal: draft.subtotal,
      totalFinal: draft.totalFinal,
      status: 'enviado',
      criadoEm: now,
    }
    items.push(novo)
    writeStorage(items)
    // simulate processing time
    setTimeout(() => res(novo), 300)
  })
}

export function clearMocks() {
  if (typeof window === 'undefined') return
  localStorage.removeItem(STORAGE_KEY)
}
