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
  Mail, 
  Phone, 
  MapPin, 
  Edit, 
  Trash2, 
  Users,
  Building
} from 'lucide-react'

import { searchClientes } from '@/lib/vendaApi'
import { Cliente } from '@/types/vendas'
import { Spinner } from '@nextui-org/react'

const columns = [
  { name: "CLIENTE", uid: "nome" },
  { name: "CONTATO", uid: "contato" },
  { name: "LOCALIZAÇÃO", uid: "local" },
  { name: "STATUS", uid: "status" },
  { name: "AÇÕES", uid: "actions" },
]

export default function ClientesPage() {
  const [clientes, setClientes] = React.useState<Cliente[]>([])
  const [isLoading, setIsLoading] = React.useState(true)

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
            name={<span className="text-sm font-black text-slate-900 dark:text-white">{client.nome}</span>}
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
      case "actions":
        return (
          <div className="flex justify-end gap-2">
            <Tooltip content="Editar Cliente" radius="lg">
              <Button isIconOnly size="sm" variant="light" color="primary">
                <Edit className="h-4 w-4" />
              </Button>
            </Tooltip>
            <Tooltip content="Remover Cliente" color="danger" radius="lg">
              <Button isIconOnly size="sm" variant="light" color="danger">
                <Trash2 className="h-4 w-4" />
              </Button>
            </Tooltip>
          </div>
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
        <Button 
          color="primary" 
          variant="shadow" 
          radius="full" 
          startContent={<Plus className="h-5 w-5" />}
          className="h-12 px-6 font-black uppercase tracking-widest text-xs"
        >
          Cadastrar Cliente
        </Button>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900" radius="lg">
        <CardHeader className="p-8 pb-2 flex-col items-start gap-4">
          <div className="flex w-full justify-between items-center gap-3">
            <Input
              isClearable
              className="w-full sm:max-w-[44%]"
              placeholder="Pesquisar por nome ou CNPJ..."
              startContent={<Search className="text-slate-400 h-4 w-4" />}
              variant="flat"
              radius="full"
              classNames={{ inputWrapper: "h-12 bg-slate-100 dark:bg-slate-950 px-4" }}
            />
            <div className="flex gap-2">
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
                <TableColumn key={column.uid} align={column.uid === "actions" ? "center" : "start"}>
                  {column.name}
                </TableColumn>
              )}
            </TableHeader>
            <TableBody 
              items={clientes}
              emptyContent={isLoading ? <div className="flex justify-center p-8"><Spinner label="Carregando clientes..." /></div> : "Nenhum cliente cadastrado."}
            >
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
                total={4}
                radius="lg"
              />
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
