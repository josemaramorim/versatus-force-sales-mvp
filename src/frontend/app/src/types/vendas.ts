export interface Produto {
  id: string;
  sku: string;
  nome: string;
  precoBase: number;
  imagemUrl?: string;
}

export interface Cliente {
  id: string;
  nome: string;
  documento: string;
  areaVenda?: string;
}

export interface ItemPedido {
  id: string;
  produtoId: string;
  sku: string;
  nome: string;
  quantidade: number;
  valorUnitario: number;
  valorDesconto: number;
  valorAcrescimo: number;
  naturezaOperacao: string;
  total: number;
  imagemUrl?: string;
}

export interface PedidoDraft {
  clienteId: string | null;
  itens: ItemPedido[];
  observacoes: string;
  condicaoPagamentoId: string;
  descontoGlobal: number;
  acrescimoGlobal: number;
  subtotal: number;
  totalFinal: number;
}
