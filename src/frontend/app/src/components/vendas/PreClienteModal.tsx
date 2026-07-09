'use client'

import React, { useState } from 'react'
import { Modal, ModalContent, ModalHeader, ModalBody, ModalFooter, Button, Input } from '@nextui-org/react'
import { X, User, FileText, Phone, Mail, MapPin } from 'lucide-react'
import { PreCliente } from '@/types/vendas'

interface PreClienteModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: PreCliente) => void
  existingClientes: { nome: string; documento: string }[]
}

export function PreClienteModal({ isOpen, onClose, onSave, existingClientes }: PreClienteModalProps) {
  const [nome, setNome] = useState('')
  const [documento, setDocumento] = useState('')
  const [telefone, setTelefone] = useState('')
  const [email, setEmail] = useState('')
  const [logradouro, setLogradouro] = useState('')
  const [numero, setNumero] = useState('')
  const [complemento, setComplemento] = useState('')
  const [bairro, setBairro] = useState('')
  const [cidade, setCidade] = useState('')
  const [uf, setUf] = useState('')
  const [cep, setCep] = useState('')

  const [validationError, setValidationError] = useState('')

  const handleSave = () => {
    setValidationError('')

    if (!nome.trim()) {
      setValidationError('O Nome/Razão Social é obrigatório.')
      return
    }

    if (!documento.trim()) {
      setValidationError('O CPF/CNPJ é obrigatório.')
      return
    }

    // Normalização para comparar duplicidade
    const cleanStr = (str: string) =>
      str.toLowerCase().normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/[.\-\/]/g, "").trim()

    const cleanNome = cleanStr(nome)
    const cleanDoc = cleanStr(documento)

    const isDuplicate = existingClientes.some(
      (c) => cleanStr(c.nome) === cleanNome || cleanStr(c.documento) === cleanDoc
    )

    if (isDuplicate) {
      setValidationError('Cliente já cadastrado no catálogo com este Nome ou CPF/CNPJ!')
      return
    }

    const preCliente: PreCliente = {
      nome: nome.trim(),
      documento: documento.trim(),
      telefone: telefone.trim() || undefined,
      email: email.trim() || undefined,
      logradouro: logradouro.trim() || undefined,
      numero: numero.trim() || undefined,
      complemento: complemento.trim() || undefined,
      bairro: bairro.trim() || undefined,
      cidade: cidade.trim() || undefined,
      uf: uf.trim().toUpperCase() || undefined,
      cep: cep.trim() || undefined
    }

    onSave(preCliente)
    
    // Limpar campos após salvar com sucesso
    setNome('')
    setDocumento('')
    setTelefone('')
    setEmail('')
    setLogradouro('')
    setNumero('')
    setComplemento('')
    setBairro('')
    setCidade('')
    setUf('')
    setCep('')
    
    onClose()
  }

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      size="2xl"
      radius="none"
      backdrop="blur"
      className="bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-800 md:rounded-[3rem] p-0 md:p-2 shadow-2xl text-slate-900 dark:text-slate-100 md:my-auto md:mx-auto"
      scrollBehavior="inside"
      hideCloseButton
    >
      <ModalContent>
        {() => (
          <>
            <ModalHeader className="p-8 border-b border-slate-100 dark:border-slate-800 flex items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="h-12 w-12 bg-blue-600 rounded-2xl flex items-center justify-center shadow-xl shadow-blue-500/20">
                  <User className="h-6 w-6 text-white" />
                </div>
                <h2 className="text-2xl font-black italic tracking-tighter text-slate-900 dark:text-white">
                  Pré-Cadastro de Novo Cliente
                </h2>
              </div>
              <Button
                isIconOnly
                variant="flat"
                radius="full"
                onPress={onClose}
                className="bg-slate-100 dark:bg-slate-800 text-slate-500 hover:bg-slate-200 dark:hover:bg-slate-700"
              >
                <X size={24} />
              </Button>
            </ModalHeader>

            <ModalBody className="p-8 space-y-6 overflow-y-auto">
              {validationError && (
                <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800/30 rounded-2xl text-sm font-bold text-red-600 dark:text-red-400">
                  {validationError}
                </div>
              )}

              {/* Informações Básicas */}
              <div className="space-y-4">
                <label className="premium-label tracking-[0.4em]">Informações Gerais</label>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">
                      Nome / Razão Social <span className="text-red-500">*</span>
                    </span>
                    <Input
                      placeholder="Nome completo ou Razão Social"
                      value={nome}
                      onValueChange={setNome}
                      startContent={<User className="text-slate-400 h-4 w-4" />}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">
                      CPF / CNPJ <span className="text-red-500">*</span>
                    </span>
                    <Input
                      placeholder="Somente números"
                      value={documento}
                      onValueChange={setDocumento}
                      startContent={<FileText className="text-slate-400 h-4 w-4" />}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                </div>
              </div>

              {/* Contato */}
              <div className="space-y-4">
                <label className="premium-label tracking-[0.4em]">Contato</label>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Telefone / Celular</span>
                    <Input
                      placeholder="(00) 00000-0000"
                      value={telefone}
                      onValueChange={setTelefone}
                      startContent={<Phone className="text-slate-400 h-4 w-4" />}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">E-mail</span>
                    <Input
                      placeholder="exemplo@email.com"
                      value={email}
                      onValueChange={setEmail}
                      startContent={<Mail className="text-slate-400 h-4 w-4" />}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                </div>
              </div>

              {/* Endereço */}
              <div className="space-y-4">
                <label className="premium-label tracking-[0.4em]">Endereço</label>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="md:col-span-2 flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Logradouro (Rua/Avenida)</span>
                    <Input
                      placeholder="Rua, Av..."
                      value={logradouro}
                      onValueChange={setLogradouro}
                      startContent={<MapPin className="text-slate-400 h-4 w-4" />}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Número</span>
                    <Input
                      placeholder="123"
                      value={numero}
                      onValueChange={setNumero}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Complemento</span>
                    <Input
                      placeholder="Apto, Sala, Bloco..."
                      value={complemento}
                      onValueChange={setComplemento}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                  <div className="flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Bairro</span>
                    <Input
                      placeholder="Centro"
                      value={bairro}
                      onValueChange={setBairro}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                </div>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="md:col-span-2 flex flex-col gap-1.5">
                    <span className="text-xs font-bold text-slate-500 dark:text-slate-400">Cidade</span>
                    <Input
                      placeholder="São Paulo"
                      value={cidade}
                      onValueChange={setCidade}
                      classNames={{
                        inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                      }}
                    />
                  </div>
                  <div className="grid grid-cols-2 gap-2">
                    <div className="flex flex-col gap-1.5">
                      <span className="text-xs font-bold text-slate-500 dark:text-slate-400">UF</span>
                      <Input
                        placeholder="SP"
                        maxLength={2}
                        value={uf}
                        onValueChange={setUf}
                        classNames={{
                          inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                        }}
                      />
                    </div>
                    <div className="flex flex-col gap-1.5">
                      <span className="text-xs font-bold text-slate-500 dark:text-slate-400">CEP</span>
                      <Input
                        placeholder="00000-000"
                        value={cep}
                        onValueChange={setCep}
                        classNames={{
                          inputWrapper: "h-14 bg-slate-50 dark:bg-slate-950 border border-slate-100 dark:border-slate-800 rounded-2xl"
                        }}
                      />
                    </div>
                  </div>
                </div>
              </div>
            </ModalBody>

            <ModalFooter className="p-8 border-t border-slate-100 dark:border-slate-800 flex flex-col sm:flex-row gap-3">
              <Button
                variant="flat"
                onPress={onClose}
                className="w-full sm:w-auto h-14 rounded-2xl font-bold bg-slate-100 text-slate-600 dark:bg-slate-800 dark:text-slate-300"
              >
                Cancelar
              </Button>
              <Button
                color="primary"
                onPress={handleSave}
                className="w-full sm:w-auto h-14 rounded-2xl font-black bg-blue-600 text-white shadow-xl shadow-blue-500/20"
              >
                Salvar Cadastro
              </Button>
            </ModalFooter>
          </>
        )}
      </ModalContent>
    </Modal>
  )
}
