export interface Produto {
  id: string;
  sku: string;
  nome: string;
  precoBase: number;
  precosPorTabela?: PriceTableEntry[];
  imagemUrl?: string;
  categoria?: string;
  saldo?: number;
  unidade?: string;
}

export interface PriceTableEntry {
  tabelaPrecoIdERP: number;
  tabelaPrecoEstoqueIdERP: number;
  descricao: string;
  valorUnitario: number;
  isPromocional: boolean;
  vigenciaInicio?: string;
  vigenciaFim?: string;
}

export interface TabelaPrecoMetadata {
  tabelaPrecoIdERP: number;
  descricao: string;
  isPromocional: boolean;
  ativa: boolean;
  vigenciaInicio?: string;
  vigenciaFim?: string;
}

export interface TenantParameters {
  tabelaPrecoIdDefault: number;
  permiteAlterarTabelaPreco: boolean;
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
  tabelaPrecoEstoqueIdERP?: number;
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

export interface TabelaPreco {
  tabelaPrecoEstoqueIdERP: number;
  produtoIdERP: number;
  tabelaPrecoIdERP: number;
  valorUnitario: number;
  percentualDescontoMaximo: number;
  controlaDescontoMaximo: boolean;
  descricao: string;
}

export interface CondicaoPagamento {
  condicaoPagtoIdERP: number;
  descricao: string;
  quantidadeParcela: number;
  diasParcelamento: number;
  acrescimo: number;
  desconto: number;
  formaCobrancaIdERP: number;
  usarMesComercial: boolean;
}
