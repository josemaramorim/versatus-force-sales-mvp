'use client'

import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useState, useEffect } from 'react'
import { useRouter } from 'next/navigation'
import { motion, AnimatePresence, type Variants } from 'framer-motion'
import { login, clearOfflineDatabase } from '@/lib/auth'
import { Loader2, AlertCircle, Moon, Sun, Eye, EyeOff, Zap } from 'lucide-react'
import { useTheme } from 'next-themes'

const schema = z.object({
  username: z.string().min(1, 'Informe o usuário'),
  password: z.string().min(1, 'Informe a senha'),
})

type FormValues = z.infer<typeof schema>

const fadeUp: Variants = {
  hidden: { opacity: 0, y: 20 },
  visible: (i: number) => ({
    opacity: 1,
    y: 0,
    transition: { delay: i * 0.08, duration: 0.4, ease: [0.22, 1, 0.36, 1] },
  }),
}

export default function LoginPage() {
  const router = useRouter()
  const { setTheme, resolvedTheme } = useTheme()
  const [mounted, setMounted] = useState(false)
  const [serverError, setServerError] = useState<string | null>(null)
  const [showPassword, setShowPassword] = useState(false)
  const [apiVersion, setApiVersion] = useState<string | null>(null)

  useEffect(() => {
    setMounted(true)
    clearOfflineDatabase().catch((err) => {
      console.error('[LoginPage] Erro ao limpar base local:', err)
    })
    fetch('/api/version')
      .then(r => r.json())
      .then(d => setApiVersion(d.version ?? 'Indisponível'))
      .catch(() => setApiVersion('Indisponível'))
  }, [])

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
        setServerError('Credenciais inválidas. Verifique seu usuário e senha.')
      } else {
        setServerError('Erro ao conectar com o servidor. Tente novamente.')
      }
    }
  }

  return (
    <div className="relative min-h-screen flex items-center justify-center overflow-hidden bg-slate-950">
      {/* Gradientes de fundo animados */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute -top-40 -left-40 w-[600px] h-[600px] rounded-full bg-violet-600/20 blur-[120px] animate-pulse" />
        <div className="absolute -bottom-40 -right-40 w-[500px] h-[500px] rounded-full bg-blue-600/15 blur-[100px] animate-pulse" style={{ animationDelay: '1s' }} />
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[400px] h-[400px] rounded-full bg-indigo-500/10 blur-[80px]" />
      </div>

      {/* Grid decorativo */}
      <div
        className="absolute inset-0 pointer-events-none opacity-[0.04]"
        style={{
          backgroundImage: 'linear-gradient(rgba(255,255,255,.3) 1px, transparent 1px), linear-gradient(90deg, rgba(255,255,255,.3) 1px, transparent 1px)',
          backgroundSize: '40px 40px',
        }}
      />

      {/* Theme Toggle */}
      <div className="absolute top-6 right-6 z-20">
        <button
          id="theme-toggle-btn"
          onClick={() => setTheme(resolvedTheme === 'dark' ? 'light' : 'dark')}
          className="p-2.5 rounded-xl bg-white/10 backdrop-blur-md border border-white/20 text-white hover:bg-white/20 transition-all duration-200 hover:scale-105 active:scale-95"
          title="Alternar Tema"
        >
          {mounted && (resolvedTheme === 'dark'
            ? <Sun className="h-4 w-4 text-amber-400" />
            : <Moon className="h-4 w-4 text-blue-300" />)}
        </button>
      </div>

      {/* Card principal */}
      <motion.div
        initial={{ opacity: 0, y: 32, scale: 0.97 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5, ease: [0.22, 1, 0.36, 1] }}
        className="relative z-10 w-full max-w-md mx-4"
      >
        <div className="bg-white/[0.07] backdrop-blur-xl border border-white/[0.12] rounded-3xl p-8 shadow-2xl shadow-black/40">
          {/* Logo e header */}
          <motion.div custom={0} variants={fadeUp} initial="hidden" animate="visible" className="text-center mb-8">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-gradient-to-br from-violet-500 to-blue-600 shadow-lg shadow-violet-500/30 mb-5">
              <Zap className="h-8 w-8 text-white" strokeWidth={2.5} />
            </div>
            <h1 className="text-2xl font-bold text-white tracking-tight">Versatus Force Sales</h1>
            <p className="text-sm text-slate-400 mt-1.5">Bem-vindo de volta! Acesse sua conta.</p>
          </motion.div>

          <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
            {/* Erro do servidor */}
            <AnimatePresence>
              {serverError && (
                <motion.div
                  key="server-error"
                  initial={{ opacity: 0, y: -8 }}
                  animate={{ opacity: 1, y: 0 }}
                  exit={{ opacity: 0, y: -8 }}
                  className="flex items-start gap-2.5 rounded-xl border border-red-500/30 bg-red-500/10 p-3.5 text-sm text-red-300"
                >
                  <AlertCircle className="h-4 w-4 shrink-0 mt-0.5 text-red-400" />
                  <span>{serverError}</span>
                </motion.div>
              )}
            </AnimatePresence>

            {/* Campo: Usuário */}
            <motion.div custom={1} variants={fadeUp} initial="hidden" animate="visible">
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
                Usuário
              </label>
              <input
                id="login-username"
                {...register('username')}
                type="text"
                placeholder="Digite seu usuário"
                autoComplete="username"
                autoFocus
                className="w-full rounded-xl bg-white/[0.06] border border-white/[0.12] px-4 py-3 text-sm text-white placeholder-slate-500 outline-none focus:border-violet-500/60 focus:ring-2 focus:ring-violet-500/20 transition-all duration-200"
              />
              {errors.username && <p className="text-[11px] text-red-400 mt-1.5 pl-1">{errors.username.message}</p>}
            </motion.div>

            {/* Campo: Senha */}
            <motion.div custom={2} variants={fadeUp} initial="hidden" animate="visible">
              <label className="block text-xs font-semibold text-slate-400 uppercase tracking-widest mb-2">
                Senha
              </label>
              <div className="relative">
                <input
                  id="login-password"
                  {...register('password')}
                  type={showPassword ? 'text' : 'password'}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  className="w-full rounded-xl bg-white/[0.06] border border-white/[0.12] px-4 py-3 pr-11 text-sm text-white placeholder-slate-500 outline-none focus:border-violet-500/60 focus:ring-2 focus:ring-violet-500/20 transition-all duration-200"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(v => !v)}
                  className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-300 transition-colors"
                  tabIndex={-1}
                >
                  {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                </button>
              </div>
              {errors.password && <p className="text-[11px] text-red-400 mt-1.5 pl-1">{errors.password.message}</p>}
            </motion.div>

            {/* Esqueceu a senha */}
            <motion.div custom={3} variants={fadeUp} initial="hidden" animate="visible" className="flex justify-end">
              <a href="#" className="text-xs text-violet-400 hover:text-violet-300 transition-colors">
                Esqueceu a senha?
              </a>
            </motion.div>

            {/* Botão Entrar */}
            <motion.div custom={4} variants={fadeUp} initial="hidden" animate="visible">
              <button
                id="login-submit-btn"
                type="submit"
                disabled={isSubmitting}
                className="w-full py-3.5 rounded-xl bg-gradient-to-r from-violet-600 to-blue-600 text-white text-sm font-semibold tracking-wide hover:from-violet-500 hover:to-blue-500 disabled:opacity-60 disabled:cursor-not-allowed transition-all duration-200 hover:shadow-lg hover:shadow-violet-500/25 active:scale-[0.98]"
              >
                {isSubmitting ? <Loader2 className="h-5 w-5 animate-spin mx-auto" /> : 'Entrar no Sistema'}
              </button>
            </motion.div>


          </form>

          <motion.p custom={6} variants={fadeUp} initial="hidden" animate="visible" className="text-center text-[10px] text-slate-600 mt-7 font-mono space-y-0.5">
            <span className="block">Front: {process.env.NEXT_PUBLIC_APP_VERSION ?? '...'}</span>
            <span className="block">API: {apiVersion ?? '...'}</span>
          </motion.p>
        </div>
      </motion.div>
    </div>
  )
}
