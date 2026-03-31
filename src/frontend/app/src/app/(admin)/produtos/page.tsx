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
  Tooltip
} from '@nextui-org/react'
import { 
  Plus, 
  Search, 
  Edit, 
  Package, 
  Zap, 
  Filter,
  Eye
} from 'lucide-react'
import { MOCK_PRODUTOS } from '@/lib/mocks'

const columns = [
  { name: "PRODUTO", uid: "nome" },
  { name: "SKU / CÓDIGO", uid: "sku" },
  { name: "PREÇO BASE", uid: "precoBase" },
  { name: "ESTOQUE", uid: "estoque" },
  { name: "AÇÕES", uid: "actions" },
]

export default function ProdutosPage() {
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
            description="Categoria: Bebidas"
            name={<span className="text-sm font-black text-slate-900 dark:text-white leading-none">{produto.nome}</span>}
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
          <p className="text-sm font-black text-slate-900 dark:text-white font-mono">
            R$ {produto.precoBase.toFixed(2)}
          </p>
        )
      case "estoque":
        const hasStock = Math.random() > 0.3; // Simulação de estoque
        return (
          <div className="flex items-center gap-2">
            <div className={`h-2 w-2 rounded-full ${hasStock ? 'bg-emerald-500 shadow-[0_0_8px_rgba(16,185,129,0.5)]' : 'bg-red-500'}`} />
            <p className="text-xs font-bold text-slate-500">{hasStock ? 'Disponível' : 'Esgotado'}</p>
          </div>
        )
      case "actions":
        return (
          <div className="flex justify-end gap-2">
            <Tooltip content="Ver Detalhes" radius="lg">
              <Button isIconOnly size="sm" variant="light" color="default">
                <Eye className="h-4 w-4 text-slate-400" />
              </Button>
            </Tooltip>
            <Tooltip content="Editar Produto" radius="lg">
              <Button isIconOnly size="sm" variant="light" color="primary">
                <Edit className="h-4 w-4" />
              </Button>
            </Tooltip>
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
            <p className="text-[10px] font-black uppercase tracking-widest text-slate-400 mt-2">Sincronizado via Versatus ERP • 1.240 Itens</p>
          </div>
        </div>
        <Button 
          color="primary" 
          variant="shadow" 
          radius="2xl" 
          startContent={<Plus className="h-5 w-5" />}
          className="h-12 px-6 font-black uppercase tracking-widest text-xs"
        >
          Novo Produto
        </Button>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900 p-2" radius="3xl">
        <CardHeader className="p-6 pb-2 flex-col items-start gap-4">
          <div className="flex w-full justify-between items-center gap-3">
            <Input
              isClearable
              className="w-full sm:max-w-[44%]"
              placeholder="Pesquisar por nome ou SKU..."
              startContent={<Search className="text-slate-400 h-4 w-4" />}
              variant="flat"
              radius="2xl"
              classNames={{ inputWrapper: "h-12 bg-slate-100 dark:bg-slate-950 px-4" }}
            />
            <div className="flex gap-2">
               <Button isIconOnly variant="flat" radius="xl" className="bg-slate-100 dark:bg-slate-950">
                  <Filter className="h-4 w-4 text-slate-500" />
               </Button>
               <Button isIconOnly variant="flat" radius="xl" className="bg-blue-50 dark:bg-blue-950">
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
                <TableColumn key={column.uid} align={column.uid === "actions" ? "center" : "start"}>
                  {column.name}
                </TableColumn>
              )}
            </TableHeader>
            <TableBody items={MOCK_PRODUTOS}>
              {(item) => (
                <TableRow key={item.id}>
                  {(columnKey) => <TableCell>{renderCell(item, columnKey)}</TableCell>}
                </TableRow>
              )}
            </TableBody>
          </Table>
          
          <div className="py-6 flex justify-center border-t border-slate-50 dark:border-slate-800">
             <Pagination
                isCompact
                showControls
                showShadow
                color="primary"
                page={1}
                total={12}
                radius="xl"
              />
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
