import api from './api'
import { Cliente, Produto } from '@/types/vendas'

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
    condicaoPagamentoId: string
    primeiroVencimento: string // ISO date string
    formaPagamento: string
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

export async function searchClientes(q?: string): Promise<Cliente[]> {
  const params: Record<string, string> = {}
  if (q) params.q = q
  const { data } = await api.get<ClientApiResponse[]>('/catalogo/clientes', { params })
  return data.map((c) => ({
    id: c.clientId,
    nome: c.nome,
    documento: c.documento,
    areaVenda: c.areaVenda,
  }))
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

export async function searchProdutos(q?: string): Promise<Produto[]> {
  const params: Record<string, string> = {}
  if (q) params.q = q
  const { data } = await api.get<ProductApiResponse[]>('/catalogo/produtos', { params })
  return data.map((p) => ({
    id: p.productId,
    sku: p.sku,
    nome: p.name,
    precoBase: p.price,
  }))
}

// ─── Orders ───────────────────────────────────────────────────────────────

export async function listPedidosApi(params?: { clienteId?: string; status?: string; page?: number; pageSize?: number }): Promise<PedidoSummary[]> {
  const { data } = await api.get<PedidoSummary[]>('/pedidos', { params })
  return data
}

export async function criarPedidoApi(payload: CriarPedidoPayload): Promise<PedidoCriado> {
  const { data } = await api.post<PedidoCriado>('/pedidos', payload)
  return data
}
