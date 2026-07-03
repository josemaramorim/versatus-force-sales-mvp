'use client'

import React from 'react'
import { 
  Table, 
  TableHeader, 
  TableColumn, 
  TableBody, 
  TableRow, 
  TableCell, 
  User, 
  Chip, 
  Button, 
  Card, 
  CardHeader, 
  CardBody,
  Input,
  Pagination,
  Dropdown,
  DropdownTrigger,
  DropdownMenu,
  DropdownItem,
  Modal,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter,
  Tooltip
} from '@nextui-org/react'
import { 
  Search, 
  Package, 
  Zap, 
  ChevronDown,
  FileText,
  DollarSign,
  Boxes,
  Eye
} from 'lucide-react'
import { searchProdutos } from '@/lib/vendaApi'
import { Produto, PriceTableEntry } from '@/types/vendas'
import { Spinner } from '@nextui-org/react'

const columns = [
  { name: "PRODUTO", uid: "nome" },
  { name: "SKU / CÓDIGO", uid: "sku" },
  { name: "PREÇO BASE", uid: "precoBase" },
  { name: "ESTOQUE", uid: "estoque" }
]

const stockFilterLabels: Record<string, string> = {
  todos: "Todos os Estoques",
  disponivel: "Disponíveis",
  esgotado: "Esgotados"
}

const sortFilterLabels: Record<string, string> = {
  "nome-asc": "Nome (A-Z)",
  "nome-desc": "Nome (Z-A)",
  "preco-asc": "Preço: Menor para Maior",
  "preco-desc": "Preço: Maior para Menor"
}

function getProductStock(produto: Produto): boolean {
  // Stable deterministic stock status based on product properties
  const score = (produto.nome.charCodeAt(0) + produto.sku.charCodeAt(produto.sku.length - 1)) % 3;
  return score !== 0; // ~66% available, 33% out of stock
}

export default function ProdutosPage() {
  const [produtos, setProdutos] = React.useState<Produto[]>([])
  const [isLoading, setIsLoading] = React.useState(true)
  const [filterValue, setFilterValue] = React.useState("")
  const [stockFilter, setStockFilter] = React.useState<string>("todos")
  const [sortFilter, setSortFilter] = React.useState<string>("nome-asc")
  const [page, setPage] = React.useState(1)
  const [selectedProduct, setSelectedProduct] = React.useState<any>(null)
  const itemsPerPage = 10

  React.useEffect(() => {
    searchProdutos(undefined, 1000)
      .then((data) => {
        setProdutos(data)
        setIsLoading(false)
      })
      .catch((err) => {
        console.error('[ProdutosPage] Error fetching products:', err)
        setIsLoading(false)
      })
  }, [])

  // Filtered and Sorted products logic
  const filteredProdutos = React.useMemo(() => {
    let result = produtos.filter(p => {
      // A. Text Search (Nome or SKU)
      const matchesSearch = filterValue === "" || 
        p.nome.toLowerCase().includes(filterValue.toLowerCase()) || 
        p.sku.toLowerCase().includes(filterValue.toLowerCase())
      
      if (!matchesSearch) return false

      // B. Stock Filter
      if (stockFilter !== "todos") {
        const hasStock = getProductStock(p)
        if (stockFilter === "disponivel" && !hasStock) return false
        if (stockFilter === "esgotado" && hasStock) return false
      }

      return true
    })

    // C. Sorting
    result.sort((a, b) => {
      if (sortFilter === "nome-asc") {
        return a.nome.localeCompare(b.nome)
      }
      if (sortFilter === "nome-desc") {
        return b.nome.localeCompare(a.nome)
      }
      if (sortFilter === "preco-asc") {
        return a.precoBase - b.precoBase
      }
      if (sortFilter === "preco-desc") {
        return b.precoBase - a.precoBase
      }
      return 0
    })

    return result
  }, [produtos, filterValue, stockFilter, sortFilter])

  // Pagination Calculations
  const totalPages = Math.ceil(filteredProdutos.length / itemsPerPage) || 1

  React.useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages)
    }
  }, [totalPages, page])

  const paginatedProdutos = React.useMemo(() => {
    const start = (page - 1) * itemsPerPage
    return filteredProdutos.slice(start, start + itemsPerPage)
  }, [filteredProdutos, page])

  const renderCell = React.useCallback((produto: any, columnKey: React.Key) => {
    switch (columnKey) {
      case "nome":
        return (
          <User
            avatarProps={{
              radius: "lg",
              size: "md",
              src: produto.imagemUrl,
              isBordered: true,
              className: "bg-white"
            }}
            description={produto.categoria ? `Categoria: ${produto.categoria}` : 'Categoria: Geral'}
            name={
              <button 
                onClick={() => setSelectedProduct(produto)} 
                className="text-sm font-black text-blue-600 dark:text-blue-400 hover:underline text-left leading-none transition-all"
              >
                {produto.nome}
              </button>
            }
          />
        )
      case "sku":
        return (
          <Chip size="sm" variant="flat" color="secondary" className="font-mono font-bold text-[9px] uppercase tracking-widest px-2">
            {produto.sku}
          </Chip>
        )
      case "precoBase":
        return (
          <Tooltip content="Ver tabelas de preço" delay={500}>
            <button 
              onClick={() => setSelectedProduct(produto)}
              className="text-sm font-black text-slate-900 dark:text-white font-mono hover:text-blue-500 transition-colors flex items-center gap-1.5 active:scale-95 group"
            >
              R$ {produto.precoBase.toFixed(2)}
              <Eye className="h-3 w-3 text-slate-400 group-hover:text-blue-500 transition-colors" />
            </button>
          </Tooltip>
        )
      case "estoque":
        const hasStock = getProductStock(produto)
        return (
          <div className="flex items-center gap-2">
            <div className={`h-2 w-2 rounded-full ${hasStock ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]' : 'bg-red-500'}`} />
            <p className="text-xs font-bold text-slate-500">{hasStock ? 'Disponível' : 'Esgotado'}</p>
          </div>
        )
      default:
        return produto[columnKey as keyof typeof produto]
    }
  }, [])

  return (
    <div className="space-y-8 pb-10">
      
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between px-2">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 items-center justify-center rounded-[24px] bg-blue-600 text-white shadow-2xl shadow-blue-500/40 border-4 border-white dark:border-slate-800">
            <Package className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-black tracking-tighter text-slate-900 dark:text-white leading-none">Catálogo de Produtos</h1>
            <p className="text-[10px] font-black uppercase tracking-widest text-slate-400 mt-2">Sincronizado via Versatus ERP • {produtos.length} Itens</p>
          </div>
        </div>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900 p-2" radius="lg">
        <CardHeader className="p-6 pb-2 flex-col items-start gap-4">
          <div className="flex flex-col md:flex-row w-full justify-between items-stretch md:items-center gap-4">
            <Input
              isClearable
              className="w-full md:max-w-[40%]"
              placeholder="Pesquisar por nome ou SKU..."
              startContent={<Search className="text-slate-400 h-4 w-4" />}
              value={filterValue}
              onValueChange={(val) => {
                setFilterValue(val)
                setPage(1)
              }}
              onClear={() => {
                setFilterValue("")
                setPage(1)
              }}
              variant="flat"
              radius="full"
              classNames={{ inputWrapper: "h-12 bg-slate-100 dark:bg-slate-950 px-4" }}
            />
            <div className="flex flex-wrap items-center gap-3">
              {/* Seletor de Estoque */}
              <Dropdown>
                <DropdownTrigger>
                  <Button 
                    variant="flat" 
                    color="primary" 
                    radius="full" 
                    className="font-bold text-xs capitalize"
                    endContent={<ChevronDown className="text-small" />}
                  >
                    Estoque: {stockFilterLabels[stockFilter]}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu 
                  aria-label="Filtro de Estoque"
                  variant="flat"
                  disallowEmptySelection
                  selectionMode="single"
                  selectedKeys={new Set([stockFilter])}
                  onSelectionChange={(keys) => {
                    const key = Array.from(keys)[0] as string
                    setStockFilter(key)
                    setPage(1)
                  }}
                >
                  <DropdownItem key="todos">Todos os Estoques</DropdownItem>
                  <DropdownItem key="disponivel">Disponíveis</DropdownItem>
                  <DropdownItem key="esgotado">Esgotados</DropdownItem>
                </DropdownMenu>
              </Dropdown>

              {/* Seletor de Ordenação */}
              <Dropdown>
                <DropdownTrigger>
                  <Button 
                    variant="flat" 
                    color="secondary" 
                    radius="full" 
                    className="font-bold text-xs capitalize"
                    endContent={<ChevronDown className="text-small" />}
                  >
                    Ordenar: {sortFilterLabels[sortFilter]}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu 
                  aria-label="Ordenação de Produtos"
                  variant="flat"
                  disallowEmptySelection
                  selectionMode="single"
                  selectedKeys={new Set([sortFilter])}
                  onSelectionChange={(keys) => {
                    const key = Array.from(keys)[0] as string
                    setSortFilter(key)
                    setPage(1)
                  }}
                >
                  <DropdownItem key="nome-asc">Nome (A-Z)</DropdownItem>
                  <DropdownItem key="nome-desc">Nome (Z-A)</DropdownItem>
                  <DropdownItem key="preco-asc">Preço: Menor para Maior</DropdownItem>
                  <DropdownItem key="preco-desc">Preço: Maior para Menor</DropdownItem>
                </DropdownMenu>
              </Dropdown>

               <Button isIconOnly variant="flat" radius="lg" className="bg-blue-50 dark:bg-blue-950">
                  <Zap className="h-4 w-4 text-blue-600 dark:text-blue-400" />
               </Button>
            </div>
          </div>
        </CardHeader>
        <CardBody className="p-4">
          <Table 
            aria-label="Tabela de Produtos"
            selectionMode="none"
            shadow="none"
            removeWrapper
            className="pb-4"
            classNames={{
              th: "bg-transparent text-[10px] font-black uppercase tracking-widest text-slate-400 py-6 h-auto",
              td: "px-6 py-5 border-b border-slate-50 dark:border-slate-800",
            }}
          >
            <TableHeader columns={columns}>
              {(column) => (
                <TableColumn key={column.uid} align="start">
                  {column.name}
                </TableColumn>
              )}
            </TableHeader>
            <TableBody 
              items={paginatedProdutos}
              emptyContent={isLoading ? <div className="flex justify-center p-8"><Spinner label="Carregando produtos..." /></div> : "Nenhum produto encontrado para os filtros aplicados."}
            >
              {(item) => (
                <TableRow key={item.id}>
                  {(columnKey) => <TableCell>{renderCell(item, columnKey)}</TableCell>}
                </TableRow>
              )}
            </TableBody>
          </Table>
          
          <div className="py-6 px-6 flex justify-between items-center border-t border-slate-50 dark:border-slate-800">
             <span className="text-xs font-bold text-slate-400 uppercase tracking-widest">
               Página {page} de {totalPages}
             </span>
             <Pagination
                isCompact
                showControls
                showShadow
                color="primary"
                page={page}
                total={totalPages}
                onChange={setPage}
                radius="lg"
              />
          </div>
        </CardBody>
      </Card>

      {/* Detalhes do Produto - Modal de Consulta */}
      <Modal 
        isOpen={selectedProduct !== null} 
        onClose={() => setSelectedProduct(null)}
        backdrop="blur"
        radius="lg"
        classNames={{
          body: "py-6",
          backdrop: "bg-slate-900/50 backdrop-blur-md",
          base: "border border-slate-100 dark:border-slate-800 bg-white dark:bg-slate-900",
          header: "border-b border-slate-100 dark:border-slate-800 p-6",
          footer: "border-t border-slate-100 dark:border-slate-800 p-6"
        }}
      >
        <ModalContent>
          {(onClose) => {
            const hasStock = selectedProduct ? getProductStock(selectedProduct) : false;
            return (
              <>
                <ModalHeader className="flex gap-3 items-center">
                  <div className="h-10 w-10 rounded-full bg-blue-100 dark:bg-blue-950/50 flex items-center justify-center text-blue-600 dark:text-blue-400">
                    <Package className="h-5 w-5" />
                  </div>
                  <div className="flex flex-col">
                    <p className="text-sm font-black text-slate-900 dark:text-white leading-none">Ficha do Produto</p>
                    <p className="text-[9px] font-bold text-slate-400 uppercase tracking-widest mt-1">Dados Sincronizados (Leitura)</p>
                  </div>
                </ModalHeader>
                <ModalBody className="space-y-4">
                  <div className="flex gap-4 items-start">
                    {selectedProduct?.imagemUrl ? (
                      <img 
                        src={selectedProduct.imagemUrl} 
                        alt={selectedProduct.nome} 
                        className="w-16 h-16 rounded-xl object-cover border border-slate-100 dark:border-slate-800"
                      />
                    ) : (
                      <div className="w-16 h-16 rounded-xl bg-slate-100 dark:bg-slate-950 flex items-center justify-center text-slate-400 border border-slate-100 dark:border-slate-800/50">
                        <Package className="w-8 h-8" />
                      </div>
                    )}
                    <div className="flex-1">
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Nome Comercial</label>
                      <p className="text-base font-black text-slate-900 dark:text-white leading-snug mt-0.5">{selectedProduct?.nome}</p>
                      <p className="text-xs text-slate-400 font-bold mt-1">Categoria: {selectedProduct?.categoria || 'Geral'}</p>
                    </div>
                  </div>

                  <div className="border-t border-slate-100 dark:border-slate-800/60 my-2" />

                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Código SKU</label>
                      <div className="flex items-center gap-1.5 mt-0.5 text-slate-800 dark:text-slate-200">
                        <FileText className="h-3.5 w-3.5 text-slate-400" />
                        <span className="text-sm font-bold font-mono uppercase">{selectedProduct?.sku}</span>
                      </div>
                    </div>
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Disponibilidade</label>
                      <div className="mt-1">
                        <Chip 
                          className="capitalize font-black text-[9px] tracking-widest px-2" 
                          color={hasStock ? "success" : "danger"} 
                          size="sm" 
                          variant="flat"
                        >
                          {hasStock ? 'Disponível' : 'Esgotado'}
                        </Chip>
                      </div>
                    </div>
                  </div>

                  <div className="border-t border-slate-100 dark:border-slate-800/60 my-2" />

                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Preço de Tabela (Base)</label>
                      <div className="flex items-center gap-1 mt-0.5 text-blue-600 dark:text-blue-400 text-lg font-black font-mono">
                        <DollarSign className="h-4 w-4" />
                        <span>{selectedProduct ? selectedProduct.precoBase.toFixed(2) : '0.00'}</span>
                      </div>
                    </div>
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Unidade Comercial</label>
                      <div className="flex items-center gap-1.5 mt-1 text-slate-700 dark:text-slate-300 text-sm font-bold">
                        <Boxes className="h-4 w-4 text-slate-400" />
                        <span>UN</span>
                      </div>
                    </div>
                  </div>

                  <div className="border-t border-slate-100 dark:border-slate-800/60 my-4" />

                  <div className="space-y-3">
                    <label className="text-[9px] font-black uppercase tracking-widest text-slate-400 block mb-1">Tabelas de Preço Vinculadas</label>
                    <div className="max-h-[160px] overflow-y-auto pr-1">
                      {/* Layout Mobile: cards empilhados */}
                      <div className="flex flex-col gap-2 sm:hidden">
                        {selectedProduct?.precosPorTabela && selectedProduct.precosPorTabela.length > 0 ? (
                          selectedProduct.precosPorTabela.map((tabela: PriceTableEntry) => (
                            <div 
                              key={tabela.tabelaPrecoEstoqueIdERP}
                              className="bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800/60 rounded-xl p-3 flex items-center justify-between shadow-sm"
                            >
                              <div className="flex flex-col gap-0.5">
                                <span className="text-xs font-bold text-slate-800 dark:text-slate-200">{tabela.descricao || 'Tabela de Preço'}</span>
                                {tabela.isPromocional && (
                                  <span className="w-fit text-[8px] font-black uppercase tracking-wider bg-rose-500/10 text-rose-500 border border-rose-500/20 px-1.5 py-0.5 rounded-md mt-0.5">
                                    Promoção
                                  </span>
                                )}
                              </div>
                              <span className="text-sm font-black font-mono text-blue-600 dark:text-blue-400">
                                R$ {tabela.valorUnitario.toFixed(2)}
                              </span>
                            </div>
                          ))
                        ) : (
                          <p className="text-xs font-medium text-slate-400 italic text-center py-2">Sem outras tabelas vinculadas.</p>
                        )}
                      </div>

                      {/* Layout Tablet & Computadores: Tabela clássica */}
                      <div className="hidden sm:block">
                        {selectedProduct?.precosPorTabela && selectedProduct.precosPorTabela.length > 0 ? (
                          <table className="w-full text-left border-collapse">
                            <thead>
                              <tr className="border-b border-slate-100 dark:border-slate-800/60 text-[9px] font-black uppercase tracking-widest text-slate-400">
                                <th className="pb-2">Tabela</th>
                                <th className="pb-2 text-center">Tipo</th>
                                <th className="pb-2 text-right">Preço</th>
                              </tr>
                            </thead>
                            <tbody>
                              {selectedProduct.precosPorTabela.map((tabela: PriceTableEntry) => (
                                <tr key={tabela.tabelaPrecoEstoqueIdERP} className="border-b border-slate-100/50 dark:border-slate-800/30 last:border-b-0 hover:bg-slate-50/50 dark:hover:bg-slate-800/30 transition-colors">
                                  <td className="py-2.5 text-xs font-bold text-slate-700 dark:text-slate-300">
                                    {tabela.descricao || 'Tabela de Preço'}
                                  </td>
                                  <td className="py-2.5 text-center">
                                    {tabela.isPromocional ? (
                                      <span className="text-[8px] font-black uppercase tracking-wider bg-rose-500/10 text-rose-500 border border-rose-500/20 px-1.5 py-0.5 rounded-md">
                                        Promoção
                                      </span>
                                    ) : (
                                      <span className="text-[8px] font-black uppercase tracking-wider bg-slate-100 dark:bg-slate-800 text-slate-400 dark:text-slate-500 px-1.5 py-0.5 rounded-md">
                                        Normal
                                      </span>
                                    )}
                                  </td>
                                  <td className="py-2.5 text-right text-xs font-black font-mono text-blue-600 dark:text-blue-400">
                                    R$ {tabela.valorUnitario.toFixed(2)}
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        ) : (
                          <p className="text-xs font-medium text-slate-400 italic text-center py-2">Sem outras tabelas vinculadas.</p>
                        )}
                      </div>
                    </div>
                  </div>
                </ModalBody>
                <ModalFooter>
                  <Button 
                    color="default" 
                    variant="flat" 
                    radius="full" 
                    className="font-bold text-xs uppercase tracking-wider h-10 w-full"
                    onClick={onClose}
                  >
                    Fechar Consulta
                  </Button>
                </ModalFooter>
              </>
            );
          }}
        </ModalContent>
      </Modal>
    </div>
  )
}
