'use client'

import React, { useEffect } from 'react'
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
  ClipboardList,
  RefreshCw
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
  pendente_sync: "warning",
  erro_sync: "danger",
  offline: "warning",
}

const statusLabelMap: Record<string, string> = {
  processado: "Processado",
  pendente: "Pendente",
  erro: "Rejeitado ERP",
  enviado: "Enviado",
  rascunho: "Rascunho",
  pendente_sync: "Aguardando Rede",
  erro_sync: "Erro de Estoque",
  offline: "Offline",
}

import { listPedidosApi, PedidoSummary } from '@/lib/vendaApi'
import { db } from '@/lib/offlineDb'

function mapPedidoToRow(p: PedidoSummary) {
  return {
    id: p.pedidoId.substring(0, 8).toUpperCase(),
    pedidoId: p.pedidoId,
    cliente: p.clienteId,
    total: `R$ ${Number(p.totalLiquido).toFixed(2)}`,
    status: p.status || 'rascunho',
    data: new Date(p.criadoEm).toLocaleString('pt-BR'),
    erroDetail: p.erroDetail
  }
}

export default function PedidosPage() {
  const [filterValue, setFilterValue] = React.useState("")
  const [orders, setOrders] = React.useState<any[]>([])

  const handleAction = React.useCallback(async (actionKey: React.Key, order: any) => {
    if (actionKey === 'excluir') {
      const isLocal = order.status === 'erro_sync' || order.status === 'pendente_sync' || order.pedidoId.startsWith('off_')
      if (isLocal && db) {
        try {
          console.log('[Offline Action] Removendo pedido local:', order.pedidoId)
          await db.pedidos.delete(order.pedidoId)
          const list = await listPedidosApi()
          setOrders(list.map(mapPedidoToRow))
        } catch (err) {
          console.error('Erro ao excluir pedido local:', err)
        }
      } else {
        alert('Pedidos já integrados com o servidor não podem ser excluídos pelo vendedor.')
      }
    } else if (actionKey === 'retentar') {
      const { syncPendingOrders } = await import('@/lib/syncQueue')
      await syncPendingOrders()
      const list = await listPedidosApi()
      setOrders(list.map(mapPedidoToRow))
    }
  }, [])

  useEffect(() => {
    let mounted = true
    listPedidosApi().then((list) => {
      if (!mounted) return
      setOrders(list.map(mapPedidoToRow))
    }).catch(() => {
      // noop
    })
    return () => { mounted = false }
  }, [])

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
            {order.status === 'erro_sync' && order.erroDetail && (
              <p className="text-[10px] font-semibold text-red-500 mt-1.5 leading-tight max-w-xs">{order.erroDetail}</p>
            )}
          </div>
        )
      case "total":
        return (
          <p className="text-sm font-black text-slate-900 dark:text-white font-mono">{cellValue}</p>
        )
      case "status":
        return (
          <Chip className="capitalize font-black text-[9px] tracking-widest px-2" color={statusColorMap[order.status]} size="sm" variant="flat">
            {statusLabelMap[order.status] || cellValue}
          </Chip>
        )
      case "data":
        return (
          <p className="text-xs font-bold text-slate-500">{cellValue}</p>
        )
      case "actions": {
        const dropdownItems = [
          { key: 'visualizar', label: 'Visualizar', icon: <Eye className="h-4 w-4" />, color: 'default' },
          ...(order.status === 'erro_sync' ? [{ key: 'retentar', label: 'Tentar Enviar Novamente', icon: <RefreshCw className="h-4 w-4 text-blue-500" />, color: 'default' }] : []),
          { key: 'exportar', label: 'Exportar PDF', icon: <Download className="h-4 w-4" />, color: 'default' },
          ...(order.status === 'rascunho' || order.status === 'erro_sync' || order.status === 'pendente_sync' ? [{ key: 'excluir', label: 'Excluir Rascunho', icon: <Trash2 className="h-4 w-4" />, color: 'danger' }] : [])
        ]

        return (
          <div className="relative flex justify-end items-center gap-2">
            <Dropdown placement="bottom-end" backdrop="blur">
              <DropdownTrigger>
                <Button isIconOnly size="sm" variant="light">
                  <MoreVertical className="text-slate-400 h-4 w-4" />
                </Button>
              </DropdownTrigger>
              <DropdownMenu 
                aria-label="Ações de Pedido" 
                onAction={(key) => handleAction(key, order)}
                items={dropdownItems}
              >
                {(item: any) => (
                  <DropdownItem 
                    key={item.key} 
                    color={item.color === 'danger' ? 'danger' : 'default'} 
                    className={item.color === 'danger' ? 'text-danger' : ''}
                    startContent={item.icon}
                  >
                    {item.label}
                  </DropdownItem>
                )}
              </DropdownMenu>
            </Dropdown>
          </div>
        )
      }
      default:
        return cellValue
    }
  }, [handleAction])

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
          radius="full" 
          startContent={<Plus className="h-5 w-5" />}
          className="h-12 px-6 font-black uppercase tracking-widest text-xs"
        >
          Novo Pedido
        </Button>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900 p-2" radius="lg">
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
              radius="full"
              classNames={{
                inputWrapper: "bg-slate-100 dark:bg-slate-950 px-4",
              }}
            />
            <div className="flex gap-3">
              <Button 
                variant="flat" 
                color="secondary" 
                radius="full" 
                className="font-bold text-xs"
                endContent={<ChevronDown className="text-small" />}
              >
                Data
              </Button>
              <Button 
                variant="flat" 
                color="default" 
                radius="full" 
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
                radius="lg"
              />
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
