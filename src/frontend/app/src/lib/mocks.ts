import { Produto, Cliente } from '@/types/vendas';

export const MOCK_PRODUTOS: Produto[] = [
  { id: '1', sku: 'SKU-89076', nome: 'Cerveja Puro Malte 600ml', precoBase: 8.50, imagemUrl: 'https://images.unsplash.com/photo-1567696153598-5313f6d1b21f?q=80&w=200&h=200&auto=format&fit=crop' },
  { id: '2', sku: 'SKU-10293', nome: 'Refrigerante Cola 2L', precoBase: 12.00, imagemUrl: 'https://images.unsplash.com/photo-1622483767028-3f66f32aef97?q=80&w=200&h=200&auto=format&fit=crop' },
  { id: '3', sku: 'SKU-55432', nome: 'Suco de Uva Integral 1L', precoBase: 15.50, imagemUrl: 'https://images.unsplash.com/photo-1595981267035-7b04ca84a810?q=80&w=200&h=200&auto=format&fit=crop' },
  { id: '4', sku: 'SKU-22310', nome: 'Água Mineral 500ml', precoBase: 2.50, imagemUrl: 'https://images.unsplash.com/photo-1548839140-29a74211fd38?q=80&w=200&h=200&auto=format&fit=crop' },
  { id: '5', sku: 'SKU-99812', nome: 'Energético Nitro 473ml', precoBase: 9.90, imagemUrl: 'https://images.unsplash.com/photo-1622543953494-017001bed7c4?q=80&w=200&h=200&auto=format&fit=crop' },
];

export const MOCK_CLIENTES: Cliente[] = [
  { id: '101', nome: 'Supermercado Bom Preço', documento: '12.345.678/0001-90', areaVenda: 'Centro' },
  { id: '102', nome: 'Atacado Expresso Ltda', documento: '98.765.432/0001-21', areaVenda: 'Zona Norte' },
  { id: '103', nome: 'Mercearia São João', documento: '11.222.333/0001-44', areaVenda: 'Bairro Novo' },
];

export const MOCK_NATUREZAS = [
  { id: '5102', label: '5102 - Venda de Mercadoria' },
  { id: '5910', label: '5910 - Bonificação / Amostra' },
  { id: '5405', label: '5405 - Venda c/ Substituição Tributária' },
];
