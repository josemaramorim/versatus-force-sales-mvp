'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState } from 'react'
import { useRouter } from 'next/navigation'
import { login } from '@/lib/auth'
import { Loader2, AlertCircle } from 'lucide-react'

const schema = z.object({
  tenantId: z.string().min(1, 'Informe o ID do Tenant'),
  username: z.string().min(1, 'Informe o usuário'),
  password: z.string().min(1, 'Informe a senha'),
})

type FormValues = z.infer<typeof schema>

export function LoginForm() {
  const router = useRouter()
  const [serverError, setServerError] = useState<string | null>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  async function onSubmit(values: FormValues) {
    setServerError(null)
    try {
      await login(values)
      router.push('/dashboard')
    } catch (err: unknown) {
      const status = (err as { response?: { status?: number } })?.response?.status
      if (status === 401) {
        setServerError('Credenciais inválidas. Verifique tenant, usuário e senha.')
      } else {
        setServerError('Erro ao conectar com o servidor. Tente novamente.')
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      {/* Server Error Message */}
      {serverError && (
        <div className="flex items-start gap-2 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-600">
          <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
          <span>{serverError}</span>
        </div>
      )}

      {/* Tenant ID */}
      <div>
        <label className="block text-sm font-medium text-slate-700 mb-2">ID do Tenant</label>
        <input
          {...register('tenantId')}
          type="text"
          placeholder="ex: versatus-demo"
          className="w-full px-4 py-4 rounded-2xl border border-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all outline-none bg-white font-medium"
        />
        {errors.tenantId && <p className="text-xs text-red-500 mt-1">{errors.tenantId.message}</p>}
      </div>

      {/* Username */}
      <div>
        <label className="block text-sm font-medium text-slate-700 mb-2">Usuário</label>
        <input
          {...register('username')}
          type="text"
          placeholder="seu_usuario"
          autoComplete="username"
          className="w-full px-4 py-4 rounded-2xl border border-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all outline-none bg-white font-medium"
        />
        {errors.username && <p className="text-xs text-red-500 mt-1">{errors.username.message}</p>}
      </div>

      {/* Password */}
      <div>
        <label className="block text-sm font-medium text-slate-700 mb-2">Senha</label>
        <input
          {...register('password')}
          type="password"
          placeholder="••••••••"
          autoComplete="current-password"
          className="w-full px-4 py-4 rounded-2xl border border-slate-200 focus:ring-2 focus:ring-blue-500 focus:border-blue-500 transition-all outline-none bg-white font-medium"
        />
        {errors.password && <p className="text-xs text-red-500 mt-1">{errors.password.message}</p>}
      </div>

      <div className="flex items-center justify-between py-2">
        <label className="flex items-center space-x-2 cursor-pointer">
          <input type="checkbox" className="w-4 h-4 rounded text-blue-600 border-slate-300 focus:ring-blue-500" />
          <span className="text-sm text-slate-600">Lembrar de mim</span>
        </label>
        <a href="#" className="text-sm font-medium text-blue-600 hover:text-blue-700">Esqueceu a senha?</a>
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="w-full py-4 bg-blue-600 hover:bg-blue-700 text-white font-bold rounded-2xl shadow-lg hover:shadow-xl transition-all transform active:scale-[0.98] disabled:opacity-70 disabled:cursor-not-allowed flex items-center justify-center gap-2"
      >
        {isSubmitting ? (
          <Loader2 className="h-5 w-5 animate-spin" />
        ) : (
          'Entrar no Sistema'
        )}
      </button>
    </form>
  )
}
