'use client'

import React, { useEffect } from 'react'
import Link from 'next/link'
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
  Pagination,
  Modal,
  ModalContent,
  ModalHeader,
  ModalBody,
  ModalFooter
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
  RefreshCw,
  AlertTriangle
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
    rawCriadoEm: p.criadoEm,
    erroDetail: p.erroDetail
  }
}

const dateFilterLabels: Record<string, string> = {
  hoje: "Hoje",
  ontem: "Ontem",
  "7dias": "Últimos 7 dias",
  "este-mes": "Este Mês",
  todos: "Todos os Pedidos",
  custom: "Personalizado..."
}

const statusFilterLabels: Record<string, string> = {
  todos: "Todos os Status",
  processado: "Processado",
  pendente: "Pendente",
  enviado: "Enviado",
  rascunho: "Rascunho",
  pendente_sync: "Aguardando Rede",
  erro_sync: "Erro de Estoque"
}

export default function PedidosPage() {
  const [isAlertOpen, setIsAlertOpen] = React.useState(false)
  const [alertTitle, setAlertTitle] = React.useState('')
  const [alertMessage, setAlertMessage] = React.useState('')
  const [filterValue, setFilterValue] = React.useState("")
  const [orders, setOrders] = React.useState<any[]>([])
  const [dateFilter, setDateFilter] = React.useState<'hoje' | 'ontem' | '7dias' | 'este-mes' | 'todos' | 'custom'>('hoje')
  const [startDate, setStartDate] = React.useState("")
  const [endDate, setEndDate] = React.useState("")
  const [statusFilter, setStatusFilter] = React.useState<string>('todos')
  const [page, setPage] = React.useState(1)
  const itemsPerPage = 10

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
        setAlertTitle('Operação Não Permitida')
        setAlertMessage('Pedidos já integrados com o servidor não podem ser excluídos pelo vendedor.')
        setIsAlertOpen(true)
      }
    } else if (actionKey === 'retentar') {
      const { syncPendingOrders } = await import('@/lib/syncQueue')
      await syncPendingOrders()
      const list = await listPedidosApi()
      setOrders(list.map(mapPedidoToRow))
    }
  }, [setAlertTitle, setAlertMessage, setIsAlertOpen])

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

  // Filtered Orders logic
  const filteredOrders = React.useMemo(() => {
    return orders.filter(order => {
      // A. Text Search Query
      const matchesSearch = filterValue === "" || 
         order.id.toLowerCase().includes(filterValue.toLowerCase()) ||
         order.pedidoId.toLowerCase().includes(filterValue.toLowerCase()) ||
         order.cliente.toLowerCase().includes(filterValue.toLowerCase())
         
      if (!matchesSearch) return false
      
      // B. Status Filter
      const matchesStatus = statusFilter === "todos" || order.status === statusFilter
      if (!matchesStatus) return false
      
      // C. Date Filter
      if (dateFilter === "todos") return true
      
      const orderDate = new Date(order.rawCriadoEm)
      if (isNaN(orderDate.getTime())) return false
      
      const today = new Date()
      
      if (dateFilter === "hoje") {
         return orderDate.getFullYear() === today.getFullYear() &&
                orderDate.getMonth() === today.getMonth() &&
                orderDate.getDate() === today.getDate()
      }
      if (dateFilter === "ontem") {
         const yesterday = new Date()
         yesterday.setDate(today.getDate() - 1)
         return orderDate.getFullYear() === yesterday.getFullYear() &&
                orderDate.getMonth() === yesterday.getMonth() &&
                orderDate.getDate() === yesterday.getDate()
      }
      if (dateFilter === "7dias") {
         const sevenDaysAgo = new Date()
         sevenDaysAgo.setDate(today.getDate() - 7)
         sevenDaysAgo.setHours(0, 0, 0, 0)
         return orderDate >= sevenDaysAgo && orderDate <= today
      }
      if (dateFilter === "este-mes") {
         return orderDate.getFullYear() === today.getFullYear() &&
                orderDate.getMonth() === today.getMonth()
      }
      if (dateFilter === "custom") {
         const start = startDate ? new Date(startDate + 'T00:00:00') : null
         const end = endDate ? new Date(endDate + 'T23:59:59') : null
         return (!start || orderDate >= start) && (!end || orderDate <= end)
      }
      
      return true
    })
  }, [orders, filterValue, statusFilter, dateFilter, startDate, endDate])

  // Pagination Calculations
  const totalPages = Math.ceil(filteredOrders.length / itemsPerPage) || 1

  // Adjust page if it exceeds total pages after filtering
  React.useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages)
    }
  }, [totalPages, page])

  // Slice for current page
  const paginatedOrders = React.useMemo(() => {
    const startIdx = (page - 1) * itemsPerPage
    return filteredOrders.slice(startIdx, startIdx + itemsPerPage)
  }, [filteredOrders, page, itemsPerPage])

  // Export to CSV Action
  const handleExportCSV = React.useCallback(() => {
    if (filteredOrders.length === 0) {
      setAlertTitle('Aviso')
      setAlertMessage('Nenhum pedido para exportar.')
      setIsAlertOpen(true)
      return
    }
    const headers = ["ID Pedido", "Cliente", "Valor", "Status", "Data de Criacao"]
    const rows = filteredOrders.map(o => [
      o.pedidoId,
      o.cliente,
      o.total,
      statusLabelMap[o.status] || o.status,
      o.data
    ])
    
    const csvContent = "data:text/csv;charset=utf-8,\uFEFF" 
      + [headers.join(";"), ...rows.map(e => e.join(";"))].join("\n")
    const encodedUri = encodeURI(csvContent)
    const link = document.createElement("a")
    link.setAttribute("href", encodedUri)
    link.setAttribute("download", `pedidos_filtrados_${new Date().toISOString().split('T')[0]}.csv`)
    document.body.appendChild(link)
    link.click()
    document.body.removeChild(link)
  }, [filteredOrders, setAlertTitle, setAlertMessage, setIsAlertOpen])

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
          as={Link} 
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
          <div className="flex flex-col md:flex-row w-full justify-between items-stretch md:items-center gap-4">
            <Input
              isClearable
              className="w-full md:max-w-[40%]"
              placeholder="Pesquisar por pedido ou cliente..."
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
              classNames={{
                inputWrapper: "bg-slate-100 dark:bg-slate-950 px-4",
              }}
            />
            <div className="flex flex-wrap items-center gap-3">
              {/* Status Filter */}
              <Dropdown>
                <DropdownTrigger>
                  <Button 
                    variant="flat" 
                    color="primary" 
                    radius="full" 
                    className="font-bold text-xs capitalize"
                    endContent={<ChevronDown className="text-small" />}
                  >
                    {statusFilterLabels[statusFilter]}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu 
                  aria-label="Filtro de Status"
                  variant="flat"
                  disallowEmptySelection
                  selectionMode="single"
                  selectedKeys={new Set([statusFilter])}
                  onSelectionChange={(keys) => {
                    const key = Array.from(keys)[0] as string
                    setStatusFilter(key)
                    setPage(1)
                  }}
                >
                  <DropdownItem key="todos">Todos os Status</DropdownItem>
                  <DropdownItem key="processado">Processado</DropdownItem>
                  <DropdownItem key="pendente">Pendente</DropdownItem>
                  <DropdownItem key="enviado">Enviado</DropdownItem>
                  <DropdownItem key="rascunho">Rascunho</DropdownItem>
                  <DropdownItem key="pendente_sync">Aguardando Rede</DropdownItem>
                  <DropdownItem key="erro_sync">Erro de Estoque</DropdownItem>
                </DropdownMenu>
              </Dropdown>

              {/* Date Period Filter */}
              <Dropdown>
                <DropdownTrigger>
                  <Button 
                    variant="flat" 
                    color="secondary" 
                    radius="full" 
                    className="font-bold text-xs capitalize"
                    endContent={<ChevronDown className="text-small" />}
                  >
                    Data: {dateFilterLabels[dateFilter]}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu 
                  aria-label="Filtro de Data"
                  variant="flat"
                  disallowEmptySelection
                  selectionMode="single"
                  selectedKeys={new Set([dateFilter])}
                  onSelectionChange={(keys) => {
                    const key = Array.from(keys)[0] as string
                    setDateFilter(key as any)
                    setPage(1)
                  }}
                >
                  <DropdownItem key="hoje">Hoje</DropdownItem>
                  <DropdownItem key="ontem">Ontem</DropdownItem>
                  <DropdownItem key="7dias">Últimos 7 dias</DropdownItem>
                  <DropdownItem key="este-mes">Este Mês</DropdownItem>
                  <DropdownItem key="todos">Todos os Pedidos</DropdownItem>
                  <DropdownItem key="custom">Personalizado...</DropdownItem>
                </DropdownMenu>
              </Dropdown>

              {/* Export CSV Button */}
              <Button 
                variant="flat" 
                color="default" 
                radius="full" 
                className="font-bold text-xs"
                startContent={<Download className="h-4 w-4" />}
                onClick={handleExportCSV}
              >
                Exportar
              </Button>
            </div>
          </div>

          {/* Custom Date Selection */}
          {dateFilter === 'custom' && (
            <div className="flex flex-wrap gap-4 items-center mt-2 p-3 bg-slate-50 dark:bg-slate-950 rounded-2xl w-full border border-slate-100 dark:border-slate-800/50">
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">De:</span>
                <input 
                  type="date"
                  value={startDate}
                  onChange={(e) => {
                    setStartDate(e.target.value)
                    setPage(1)
                  }}
                  className="px-3 py-1.5 text-xs font-bold rounded-full bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              <div className="flex items-center gap-2">
                <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">Até:</span>
                <input 
                  type="date"
                  value={endDate}
                  onChange={(e) => {
                    setEndDate(e.target.value)
                    setPage(1)
                  }}
                  className="px-3 py-1.5 text-xs font-bold rounded-full bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 text-slate-800 dark:text-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500"
                />
              </div>
              {(startDate || endDate) && (
                <Button 
                  size="sm" 
                  variant="light" 
                  color="danger" 
                  radius="full" 
                  className="text-xs font-bold"
                  onClick={() => {
                    setStartDate("")
                    setEndDate("")
                    setPage(1)
                  }}
                >
                  Limpar Datas
                </Button>
              )}
            </div>
          )}
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
            <TableBody 
              items={paginatedOrders}
              emptyContent="Nenhum pedido encontrado para os filtros aplicados."
            >
              {(item) => (
                <TableRow key={item.id} className="cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
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

      {/* Modal de Alerta Customizado Estilizado */}
      <Modal 
        isOpen={isAlertOpen} 
        onClose={() => setIsAlertOpen(false)}
        backdrop="blur"
        placement="center"
        classNames={{
          backdrop: "bg-slate-950/60 backdrop-blur-md",
          base: "border border-white/10 bg-slate-900/90 text-white rounded-3xl p-4 shadow-2xl z-[99999] mx-4",
        }}
      >
        <ModalContent>
          {(onClose) => (
            <>
              <ModalHeader className="flex flex-col gap-1 items-center pb-2 text-center">
                <div className="w-12 h-12 bg-rose-500/10 border border-rose-500/20 rounded-2xl flex items-center justify-center mb-2 animate-pulse">
                  <AlertTriangle className="h-6 w-6 text-rose-500" />
                </div>
                <h3 className="text-xl font-black italic tracking-tight premium-title">{alertTitle}</h3>
              </ModalHeader>
              <ModalBody className="py-4 text-center leading-relaxed">
                <p className="text-slate-400 font-medium text-sm italic">
                  {alertMessage}
                </p>
              </ModalBody>
              <ModalFooter className="flex flex-col sm:flex-row items-stretch sm:items-center gap-3 justify-center pt-2 w-full">
                <Button 
                  color="primary" 
                  onPress={() => setIsAlertOpen(false)}
                  className="w-full sm:w-auto px-6 py-4 bg-blue-600 hover:bg-blue-500 text-white font-black rounded-xl active:scale-95 transition-transform uppercase tracking-wider text-xs"
                >
                  Entendido
                </Button>
              </ModalFooter>
            </>
          )}
        </ModalContent>
      </Modal>
    </div>
  )
}
