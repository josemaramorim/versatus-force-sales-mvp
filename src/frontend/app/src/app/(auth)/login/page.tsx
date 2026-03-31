'use client'

import styles from './login.module.css'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { login, loginDemo } from '@/lib/auth'
import { Loader2, AlertCircle, Moon, Sun, MonitorPlay } from 'lucide-react'
import { useTheme } from 'next-themes'

const schema = z.object({
  tenantId: z.string().min(1, 'Informe o ID do Tenant'),
  username: z.string().min(1, 'Informe o usuário'),
  password: z.string().min(1, 'Informe a senha'),
})

type FormValues = z.infer<typeof schema>

export default function LoginPage() {
  const router = useRouter()
  const { setTheme, resolvedTheme } = useTheme()
  const [mounted, setMounted] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)

  useEffect(() => setMounted(true), [])

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema) })

  async function handleDemoLogin() {
    setServerError(null)
    await loginDemo()
    router.push('/dashboard')
  }

  async function onSubmit(values: FormValues) {
    setServerError(null)
    try {
      await login(values)
      router.push('/dashboard')
    } catch (err: any) {
      const status = err.response?.status
      if (status === 401) {
        setServerError('Credenciais inválidas. Verifique tenant, usuário e senha.')
      } else {
        setServerError('Erro ao conectar com o servidor. Tente novamente.')
      }
    }
  }

  return (
    <div className={styles.wrapper}>
      {/* Theme Toggle - Moderna e Discreta */}
      <div className="absolute top-8 right-8">
        <button
          onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
          className="p-3 rounded-2xl bg-white dark:bg-slate-900 shadow-xl border border-slate-100 dark:border-slate-800 transition-all hover:scale-110 active:scale-95"
          title="Alternar Tema"
        >
          {mounted && (resolvedTheme === 'dark' ? <Sun className="h-5 w-5 text-amber-500" /> : <Moon className="h-5 w-5 text-blue-600" />)}
        </button>
      </div>

      <div className={styles.card}>
        <div className={styles.header}>
          <div className={styles.logoBox}>
            <svg xmlns="http://www.w3.org/2000/svg" className="h-8 w-8 text-white" fill="none" viewBox="0 0 24 24" stroke="currentColor">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
          </div>
          <h1 className={styles.title}>Versatus.Net</h1>
          <p className={styles.subtitle}>Bem-vindo de volta, Sales Force</p>
        </div>

        <form onSubmit={handleSubmit(onSubmit)}>
          {serverError && (
            <div className="flex items-start gap-2 rounded-xl border border-red-100 bg-red-50 p-4 text-sm text-red-600 mb-6 animate-in fade-in slide-in-from-top-1">
              <AlertCircle className="h-4 w-4 shrink-0 mt-0.5" />
              <span>{serverError}</span>
            </div>
          )}

          <div className={styles.formGroup}>
            <label className={styles.label}>ID do Tenant</label>
            <input
              {...register('tenantId')}
              type="text"
              placeholder="ex: versatus-demo"
              className={styles.input}
            />
            {errors.tenantId && <p className="text-[11px] text-red-500 mt-1 pl-1">{errors.tenantId.message}</p>}
          </div>

          <div className={styles.formGroup}>
            <label className={styles.label}>Usuário</label>
            <input
              {...register('username')}
              type="text"
              placeholder="seu_usuario"
              autoComplete="username"
              className={styles.input}
            />
            {errors.username && <p className="text-[11px] text-red-500 mt-1 pl-1">{errors.username.message}</p>}
          </div>

          <div className={styles.formGroup}>
            <label className={styles.label}>Senha</label>
            <input
              {...register('password')}
              type="password"
              placeholder="••••••••"
              autoComplete="current-password"
              className={styles.input}
            />
            {errors.password && <p className="text-[11px] text-red-500 mt-1 pl-1">{errors.password.message}</p>}
          </div>

          <div className={styles.optionsRow}>
            <label className={styles.rememberMe}>
              <input type="checkbox" className={styles.checkbox} />
              <span className={styles.rememberLabel}>Lembrar de mim</span>
            </label>
            <a href="#" className={styles.forgotPassword}>Esqueceu a senha?</a>
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className={styles.submitBtn}
          >
            {isSubmitting ? (
              <Loader2 className="h-5 w-5 animate-spin mx-auto" />
            ) : (
              'Entrar no Sistema'
            )}
          </button>

          <button
            type="button"
            onClick={handleDemoLogin}
            className="mt-4 w-full flex items-center justify-center gap-2 rounded-xl border border-slate-200 py-3.5 text-[11px] font-black uppercase tracking-[0.2em] text-slate-500 hover:bg-slate-50 transition-all active:scale-95 dark:border-slate-800 dark:text-slate-400 dark:hover:bg-slate-900"
          >
            <MonitorPlay className="h-4 w-4" />
            Modo de Demonstração (Sem ERP)
          </button>
        </form>

        <p className={styles.footer}>Versatus.Net Desktop Migration v2.0</p>
      </div>
    </div>
  )
}
