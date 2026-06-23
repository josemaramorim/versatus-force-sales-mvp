import Dexie, { type Table } from 'dexie';
import { Cliente, Produto, TabelaPreco, CondicaoPagamento } from '@/types/vendas';

export interface OfflinePedido {
  id: string; // UUID gerado no frontend
  clienteId: string;
  clienteNome: string;
  itens: {
    produtoId: string;
    sku: string;
    nome: string;
    quantidade: number;
    precoUnitario: number;
    desconto: number;
  }[];
  condicaoPagamento: {
    condicaoPagamentoId: string;
    primeiroVencimento: string; // ISO date string
    formaPagamento: string;
  };
  observacao?: string;
  status: 'pendente_sync' | 'erro_sync' | 'sincronizado';
  totalLiquido: number;
  criadoEm: string; // ISO date string
  erroSyncDetail?: string; // Detalhes de erro caso a sincronização falhe
}

class VersatusOfflineDatabase extends Dexie {
  clientes!: Table<Cliente, string>;
  produtos!: Table<Produto, string>;
  tabelasPreco!: Table<TabelaPreco, number>;
  condicoesPagamento!: Table<CondicaoPagamento, number>;
  pedidos!: Table<OfflinePedido, string>;

  constructor() {
    super('VersatusOfflineDB');
    this.version(2).stores({
      clientes: 'id, nome, documento',
      produtos: 'id, sku, nome, precoBase',
      tabelasPreco: 'tabelaPrecoEstoqueIdERP, produtoIdERP, tabelaPrecoIdERP',
      condicoesPagamento: 'condicaoPagtoIdERP, descricao',
      pedidos: 'id, clienteId, status, criadoEm',
    });
  }
}

// Inicializa o banco de dados de forma segura (garante que roda apenas no browser)
export const db = typeof window !== 'undefined' ? new VersatusOfflineDatabase() : null;
