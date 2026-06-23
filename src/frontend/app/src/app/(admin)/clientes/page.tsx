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
  ModalFooter
} from '@nextui-org/react'
import { 
  Search, 
  Mail, 
  Phone, 
  MapPin, 
  Users,
  Building,
  ChevronDown,
  FileText,
  UserCheck
} from 'lucide-react'

import { searchClientes } from '@/lib/vendaApi'
import { Cliente } from '@/types/vendas'
import { Spinner } from '@nextui-org/react'

const columns = [
  { name: "CLIENTE", uid: "nome" },
  { name: "CONTATO", uid: "contato" },
  { name: "LOCALIZAÇÃO", uid: "local" },
  { name: "STATUS", uid: "status" }
]

const statusFilterLabels: Record<string, string> = {
  todos: "Todos os Status",
  ativo: "Ativos",
  inativo: "Inativos",
  pendente: "Pendentes"
}

export default function ClientesPage() {
  const [clientes, setClientes] = React.useState<Cliente[]>([])
  const [isLoading, setIsLoading] = React.useState(true)
  const [filterValue, setFilterValue] = React.useState("")
  const [statusFilter, setStatusFilter] = React.useState<string>("todos")
  const [page, setPage] = React.useState(1)
  const [selectedClient, setSelectedClient] = React.useState<any>(null)
  const itemsPerPage = 10

  React.useEffect(() => {
    searchClientes(undefined, 1000)
      .then((data) => {
        setClientes(data)
        setIsLoading(false)
      })
      .catch((err) => {
        console.error('[ClientesPage] Error fetching clients:', err)
        setIsLoading(false)
      })
  }, [])

  // Filtered Clients logic
  const filteredClientes = React.useMemo(() => {
    return clientes.filter(c => {
      const matchesSearch = filterValue === "" || 
        c.nome.toLowerCase().includes(filterValue.toLowerCase()) || 
        c.documento.includes(filterValue)

      const clientStatus = (c as any).status || 'ativo'
      const matchesStatus = statusFilter === "todos" || clientStatus.toLowerCase() === statusFilter.toLowerCase()

      return matchesSearch && matchesStatus
    })
  }, [clientes, filterValue, statusFilter])

  // Pagination Calculations
  const totalPages = Math.ceil(filteredClientes.length / itemsPerPage) || 1

  React.useEffect(() => {
    if (page > totalPages) {
      setPage(totalPages)
    }
  }, [totalPages, page])

  const paginatedClientes = React.useMemo(() => {
    const start = (page - 1) * itemsPerPage
    return filteredClientes.slice(start, start + itemsPerPage)
  }, [filteredClientes, page])

  const renderCell = React.useCallback((client: any, columnKey: React.Key) => {
    switch (columnKey) {
      case "nome":
        return (
          <User
            avatarProps={{
              radius: "lg",
              size: "md",
              name: client.nome.charAt(0),
              color: "primary",
              isBordered: true,
              className: "bg-blue-600 text-white"
            }}
            description={client.documento}
            name={
              <button 
                onClick={() => setSelectedClient(client)} 
                className="text-sm font-black text-blue-600 dark:text-blue-400 hover:underline text-left transition-all"
              >
                {client.nome}
              </button>
            }
          />
        )
      case "contato":
        return (
          <div className="flex flex-col gap-1">
            <div className="flex items-center gap-2 text-xs font-bold text-slate-500">
              <Mail className="h-3 w-3" /> {client.email || 'contato@cliente.com.br'}
            </div>
            <div className="flex items-center gap-2 text-[10px] font-bold text-slate-400">
              <Phone className="h-3 w-3" /> {client.telefone || 'Sem telefone'}
            </div>
          </div>
        )
      case "local":
        return (
          <div className="flex items-center gap-2 text-xs font-bold text-slate-500">
            <MapPin className="h-3.5 w-3.5 text-blue-500" /> {client.cidade || client.areaVenda || 'Geral'}
          </div>
        )
      case "status":
        const statusValue = client.status || 'ativo';
        return (
          <Chip 
            className="capitalize font-black text-[9px] tracking-widest px-2" 
            color={statusValue === "ativo" ? "success" : statusValue === "pendente" ? "warning" : "danger"} 
            size="sm" 
            variant="flat"
          >
            {statusValue}
          </Chip>
        )
      default:
        return client[columnKey as keyof typeof client] || ''
    }
  }, [])

  return (
    <div className="space-y-8 pb-10">
      
      {/* Page Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between px-2">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 items-center justify-center rounded-[24px] bg-blue-600 text-white shadow-2xl shadow-blue-500/40 border-4 border-white dark:border-slate-800">
            <Users className="h-6 w-6" />
          </div>
          <div>
            <h1 className="text-3xl font-black tracking-tighter text-slate-900 dark:text-white leading-none">Gestão de Clientes</h1>
            <p className="text-[10px] font-black uppercase tracking-widest text-slate-400 mt-2">Base de Dados Integrada Versatus ERP</p>
          </div>
        </div>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900" radius="lg">
        <CardHeader className="p-8 pb-2 flex-col items-start gap-4">
          <div className="flex flex-col md:flex-row w-full justify-between items-stretch md:items-center gap-4">
            <Input
              isClearable
              className="w-full md:max-w-[40%]"
              placeholder="Pesquisar por nome ou CNPJ..."
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
            <div className="flex items-center gap-3">
              {/* Seletor de Status */}
              <Dropdown>
                <DropdownTrigger>
                  <Button 
                    variant="flat" 
                    color="primary" 
                    radius="full" 
                    className="font-bold text-xs capitalize"
                    endContent={<ChevronDown className="text-small" />}
                  >
                    Status: {statusFilterLabels[statusFilter]}
                  </Button>
                </DropdownTrigger>
                <DropdownMenu 
                  aria-label="Filtro de Status de Cliente"
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
                  <DropdownItem key="ativo">Ativos</DropdownItem>
                  <DropdownItem key="inativo">Inativos</DropdownItem>
                  <DropdownItem key="pendente">Pendentes</DropdownItem>
                </DropdownMenu>
              </Dropdown>

               <Button isIconOnly variant="flat" radius="lg" className="bg-slate-100 dark:bg-slate-950">
                  <Building className="h-4 w-4 text-slate-500" />
               </Button>
            </div>
          </div>
        </CardHeader>
        <CardBody className="p-4">
          <Table 
            aria-label="Tabela de Clientes"
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
              items={paginatedClientes}
              emptyContent={isLoading ? <div className="flex justify-center p-8"><Spinner label="Carregando clientes..." /></div> : "Nenhum cliente encontrado para os filtros aplicados."}
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

      {/* Detalhes do Cliente - Modal de Consulta */}
      <Modal 
        isOpen={selectedClient !== null} 
        onClose={() => setSelectedClient(null)}
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
          {(onClose) => (
            <>
              <ModalHeader className="flex gap-3 items-center">
                <div className="h-10 w-10 rounded-full bg-blue-100 dark:bg-blue-950/50 flex items-center justify-center text-blue-600 dark:text-blue-400">
                  <Building className="h-5 w-5" />
                </div>
                <div className="flex flex-col">
                  <p className="text-sm font-black text-slate-900 dark:text-white leading-none">Ficha do Cliente</p>
                  <p className="text-[9px] font-bold text-slate-400 uppercase tracking-widest mt-1">Dados Sincronizados (Leitura)</p>
                </div>
              </ModalHeader>
              <ModalBody className="space-y-4">
                <div>
                  <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Nome Razão Social</label>
                  <p className="text-base font-black text-slate-900 dark:text-white leading-snug mt-0.5">{selectedClient?.nome}</p>
                </div>

                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Documento</label>
                    <div className="flex items-center gap-1.5 mt-0.5 text-slate-800 dark:text-slate-200">
                      <FileText className="h-3.5 w-3.5 text-slate-400" />
                      <span className="text-sm font-bold font-mono">{selectedClient?.documento}</span>
                    </div>
                  </div>
                  <div>
                    <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Status</label>
                    <div className="mt-1">
                      <Chip 
                        className="capitalize font-black text-[9px] tracking-widest px-2" 
                        color={selectedClient?.status === "inativo" ? "danger" : selectedClient?.status === "pendente" ? "warning" : "success"} 
                        size="sm" 
                        variant="flat"
                      >
                        {selectedClient?.status || 'ativo'}
                      </Chip>
                    </div>
                  </div>
                </div>

                <div className="border-t border-slate-100 dark:border-slate-800/60 my-2" />

                <div className="space-y-3">
                  <p className="text-[10px] font-black uppercase tracking-widest text-slate-900 dark:text-white">Informações de Contato</p>
                  <div className="space-y-2">
                    <div className="flex items-center gap-2.5 text-sm font-bold text-slate-600 dark:text-slate-300">
                      <Mail className="h-4 w-4 text-slate-400" />
                      <span>{selectedClient?.email || 'contato@cliente.com.br'}</span>
                    </div>
                    <div className="flex items-center gap-2.5 text-sm font-bold text-slate-600 dark:text-slate-300">
                      <Phone className="h-4 w-4 text-slate-400" />
                      <span>{selectedClient?.telefone || 'Sem telefone cadastrado'}</span>
                    </div>
                  </div>
                </div>

                <div className="border-t border-slate-100 dark:border-slate-800/60 my-2" />

                <div className="space-y-3">
                  <p className="text-[10px] font-black uppercase tracking-widest text-slate-900 dark:text-white">Localização & Vendas</p>
                  <div className="grid grid-cols-2 gap-4">
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Cidade / Região</label>
                      <div className="flex items-center gap-1.5 mt-0.5 text-slate-700 dark:text-slate-300 text-sm font-bold">
                        <MapPin className="h-4 w-4 text-blue-500" />
                        <span>{selectedClient?.cidade || 'Geral'}</span>
                      </div>
                    </div>
                    <div>
                      <label className="text-[9px] font-black uppercase tracking-widest text-slate-400">Área de Venda ERP</label>
                      <div className="flex items-center gap-1.5 mt-0.5 text-slate-700 dark:text-slate-300 text-sm font-bold">
                        <UserCheck className="h-4 w-4 text-emerald-500" />
                        <span>{selectedClient?.areaVenda || 'Não especificada'}</span>
                      </div>
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
          )}
        </ModalContent>
      </Modal>
    </div>
  )
}
