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
  Dropdown,
  DropdownTrigger,
  DropdownMenu,
  DropdownItem,
  Pagination
} from '@nextui-org/react'
import { 
  Search, 
  ChevronDown, 
  Plus, 
  MoreVertical, 
  FileText, 
  Eye, 
  Trash2, 
  Download,
  ClipboardList
} from 'lucide-react'

const columns = [
  { name: "PEDIDO", uid: "id" },
  { name: "CLIENTE", uid: "cliente" },
  { name: "VALOR", uid: "total" },
  { name: "STATUS", uid: "status" },
  { name: "DATA", uid: "data" },
  { name: "AÇÕES", uid: "actions" },
]

const statusColorMap: Record<string, "primary" | "success" | "warning" | "danger" | "default"> = {
  processado: "success",
  pendente: "warning",
  erro: "danger",
  enviado: "primary",
  rascunho: "default",
}

const orders = [
  { id: "#00124", cliente: "Supermercado Bom Preço", total: "R$ 1.240,00", status: "enviado", data: "Hoje, 14:30" },
  { id: "#00123", cliente: "Atacado Expresso Ltda", total: "R$ 3.870,50", status: "processado", data: "Hoje, 10:15" },
  { id: "#00122", cliente: "Mercearia São João", total: "R$ 560,00", status: "rascunho", data: "Ontem, 16:45" },
  { id: "#00121", cliente: "Distribuidora Norte Sul", total: "R$ 8.200,00", status: "processado", data: "Ontem, 09:20" },
  { id: "#00120", cliente: "Padaria do Zé", total: "R$ 320,50", status: "erro", data: "28/03/2024" },
]

export default function PedidosPage() {
  const [filterValue, setFilterValue] = React.useState("")

  const renderCell = React.useCallback((order: any, columnKey: React.Key) => {
    const cellValue = order[columnKey as keyof typeof order]

    switch (columnKey) {
      case "id":
        return (
          <p className="text-xs font-black font-mono text-blue-600">{cellValue}</p>
        )
      case "cliente":
        return (
          <div className="flex flex-col">
            <p className="text-sm font-bold text-slate-900 dark:text-white leading-none">{cellValue}</p>
            <p className="text-[10px] text-slate-400 font-bold uppercase tracking-widest mt-1">Vendedor: Principal</p>
          </div>
        )
      case "total":
        return (
          <p className="text-sm font-black text-slate-900 dark:text-white font-mono">{cellValue}</p>
        )
      case "status":
        return (
          <Chip className="capitalize font-black text-[9px] tracking-widest px-2" color={statusColorMap[order.status]} size="sm" variant="flat">
            {cellValue}
          </Chip>
        )
      case "data":
        return (
          <p className="text-xs font-bold text-slate-500">{cellValue}</p>
        )
      case "actions":
        return (
          <div className="relative flex justify-end items-center gap-2">
            <Dropdown placement="bottom-end" backdrop="blur">
              <DropdownTrigger>
                <Button isIconOnly size="sm" variant="light">
                  <MoreVertical className="text-slate-400 h-4 w-4" />
                </Button>
              </DropdownTrigger>
              <DropdownMenu aria-label="Ações de Pedido">
                <DropdownItem startContent={<Eye className="h-4 w-4" />}>Visualizar</DropdownItem>
                <DropdownItem startContent={<Download className="h-4 w-4" />}>Exportar PDF</DropdownItem>
                <DropdownItem color="danger" className="text-danger" startContent={<Trash2 className="h-4 w-4" />}>
                  Excluir Rascunho
                </DropdownItem>
              </DropdownMenu>
            </Dropdown>
          </div>
        )
      default:
        return cellValue
    }
  }, [])

  return (
    <div className="space-y-8 pb-10">
      
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between px-2">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 items-center justify-center rounded-[24px] bg-blue-600 text-white shadow-2xl shadow-blue-500/40 border-4 border-white dark:border-slate-800">
            <ClipboardList className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-black tracking-tighter text-slate-900 dark:text-white leading-none">Histórico de Pedidos</h1>
            <p className="text-[10px] font-black uppercase tracking-widest text-slate-400 mt-2">Versatus Force Sales v2.0</p>
          </div>
        </div>
        <Button 
          as="a" 
          href="/vendas/nova"
          color="primary" 
          variant="shadow" 
          radius="2xl" 
          startContent={<Plus className="h-5 w-5" />}
          className="h-12 px-6 font-black uppercase tracking-widest text-xs"
        >
          Novo Pedido
        </Button>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900 p-2" radius="3xl">
        <CardHeader className="p-6 pb-2 flex-col items-start gap-4">
          <div className="flex w-full justify-between items-center gap-3">
            <Input
              isClearable
              className="w-full sm:max-w-[44%]"
              placeholder="Pesquisar por pedido ou cliente..."
              startContent={<Search className="text-slate-400 h-4 w-4" />}
              value={filterValue}
              onValueChange={setFilterValue}
              variant="flat"
              radius="2xl"
              classNames={{
                inputWrapper: "bg-slate-100 dark:bg-slate-950 px-4",
              }}
            />
            <div className="flex gap-3">
              <Button 
                variant="flat" 
                color="secondary" 
                radius="2xl" 
                className="font-bold text-xs"
                endContent={<ChevronDown className="text-small" />}
              >
                Data
              </Button>
              <Button 
                variant="flat" 
                color="default" 
                radius="2xl" 
                className="font-bold text-xs"
                startContent={<Download className="h-4 w-4" />}
              >
                Exportar
              </Button>
            </div>
          </div>
        </CardHeader>
        <CardBody className="p-0">
          <Table 
            aria-label="Tabela de Pedidos"
            selectionMode="single"
            shadow="none"
            radius="none"
            className="px-4 pb-4"
            removeWrapper
            classNames={{
              table: "min-w-[600px]",
              thead: "bg-transparent",
              th: "bg-transparent text-[10px] font-black uppercase tracking-widest text-slate-400 py-6 h-auto",
              td: "py-5 px-6 border-b border-slate-50 dark:border-slate-800",
            }}
          >
            <TableHeader columns={columns}>
              {(column) => (
                <TableColumn key={column.uid} align={column.uid === "actions" ? "center" : "start"}>
                  {column.name}
                </TableColumn>
              )}
            </TableHeader>
            <TableBody items={orders}>
              {(item) => (
                <TableRow key={item.id} className="cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
                  {(columnKey) => <TableCell>{renderCell(item, columnKey)}</TableCell>}
                </TableRow>
              )}
            </TableBody>
          </Table>
          
          <div className="py-6 px-6 flex justify-between items-center border-t border-slate-50 dark:border-slate-800">
             <span className="text-xs font-bold text-slate-400 uppercase tracking-widest">Página 1 de 5</span>
             <Pagination
                isCompact
                showControls
                showShadow
                color="primary"
                page={1}
                total={10}
                radius="xl"
              />
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
