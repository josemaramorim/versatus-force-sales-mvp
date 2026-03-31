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

const columns = [
  { name: "CLIENTE", uid: "nome" },
  { name: "CONTATO", uid: "contato" },
  { name: "LOCALIZAÇÃO", uid: "local" },
  { name: "STATUS", uid: "status" },
  { name: "AÇÕES", uid: "actions" },
]

const clients = [
  { 
    id: "1", 
    nome: "Supermercado Bom Preço", 
    documento: "12.345.678/0001-90", 
    email: "compras@bompreco.com.br", 
    telefone: "(11) 4002-8922", 
    cidade: "São Paulo, SP", 
    status: "ativo" 
  },
  { 
    id: "2", 
    nome: "Atacado Expresso Ltda", 
    documento: "98.765.432/0001-21", 
    email: "financeiro@atacadoexp.com", 
    telefone: "(21) 3344-5566", 
    cidade: "Rio de Janeiro, RJ", 
    status: "ativo" 
  },
  { 
    id: "3", 
    nome: "Mercearia São João", 
    documento: "11.222.333/0001-44", 
    email: "saojoao@gmail.com", 
    telefone: "(41) 98877-6655", 
    cidade: "Curitiba, PR", 
    status: "pendente" 
  },
  { 
    id: "4", 
    nome: "Distribuidora Norte Sul", 
    documento: "44.555.666/0001-77", 
    email: "contato@nortesul.com.br", 
    telefone: "(31) 2211-3322", 
    cidade: "Belo Horizonte, MG", 
    status: "bloqueado" 
  },
]

export default function ClientesPage() {
  const renderCell = React.useCallback((client: any, columnKey: React.Key) => {
    const cellValue = client[columnKey as keyof typeof client]

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
              <Mail className="h-3 w-3" /> {client.email}
            </div>
            <div className="flex items-center gap-2 text-[10px] font-bold text-slate-400">
              <Phone className="h-3 w-3" /> {client.telefone}
            </div>
          </div>
        )
      case "local":
        return (
          <div className="flex items-center gap-2 text-xs font-bold text-slate-500">
            <MapPin className="h-3.5 w-3.5 text-blue-500" /> {client.cidade}
          </div>
        )
      case "status":
        return (
          <Chip 
            className="capitalize font-black text-[9px] tracking-widest px-2" 
            color={client.status === "ativo" ? "success" : client.status === "pendente" ? "warning" : "danger"} 
            size="sm" 
            variant="flat"
          >
            {cellValue}
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
        return cellValue
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
          radius="2xl" 
          startContent={<Plus className="h-5 w-5" />}
          className="h-12 px-6 font-black uppercase tracking-widest text-xs"
        >
          Cadastrar Cliente
        </Button>
      </div>

      <Card className="border-none shadow-2xl bg-white dark:bg-slate-900" radius="3xl">
        <CardHeader className="p-8 pb-2 flex-col items-start gap-4">
          <div className="flex w-full justify-between items-center gap-3">
            <Input
              isClearable
              className="w-full sm:max-w-[44%]"
              placeholder="Pesquisar por nome ou CNPJ..."
              startContent={<Search className="text-slate-400 h-4 w-4" />}
              variant="flat"
              radius="2xl"
              classNames={{ inputWrapper: "h-12 bg-slate-100 dark:bg-slate-950 px-4" }}
            />
            <div className="flex gap-2">
               <Button isIconOnly variant="flat" radius="xl" className="bg-slate-100 dark:bg-slate-950">
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
            <TableBody items={clients}>
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
                radius="xl"
              />
          </div>
        </CardBody>
      </Card>
    </div>
  )
}
