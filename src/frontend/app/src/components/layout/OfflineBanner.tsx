'use client'

import React, { useEffect, useState } from 'react';
import { WifiOff, Wifi, RefreshCw, CheckCircle2 } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';

export function OfflineBanner() {
  const [isOnline, setIsOnline] = useState(true);
  const [toastType, setToastType] = useState<'offline' | 'online_syncing' | 'online_success'>('offline');
  const [showToast, setShowToast] = useState(false);
  const [mounted, setMounted] = useState(false);

  useEffect(() => {
    setMounted(true);
    const onlineStatus = navigator.onLine;
    setIsOnline(onlineStatus);

    // Se abrir o app offline, já exibe o banner offline de início
    if (!onlineStatus) {
      setToastType('offline');
      setShowToast(true);
    }

    const handleOnline = () => {
      setIsOnline(true);
      setToastType('online_syncing');
      setShowToast(true);

      // Após 4 segundos de sincronização, exibe sucesso e depois oculta
      const successTimeout = setTimeout(() => {
        setToastType('online_success');
      }, 4000);

      const hideTimeout = setTimeout(() => {
        setShowToast(false);
      }, 7000);

      return () => {
        clearTimeout(successTimeout);
        clearTimeout(hideTimeout);
      };
    };

    const handleOffline = () => {
      setIsOnline(false);
      setToastType('offline');
      setShowToast(true);
    };

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  if (!mounted || !showToast) return null;

  const config = {
    offline: {
      bg: 'bg-amber-500/90 dark:bg-amber-500/80 border-amber-400/40 text-slate-950',
      icon: <WifiOff className="h-4 w-4 shrink-0" />,
      text: 'Modo Offline Ativo. Vendas serão salvas localmente no dispositivo.',
    },
    online_syncing: {
      bg: 'bg-blue-600/90 dark:bg-blue-600/80 border-blue-500/40 text-white',
      icon: <RefreshCw className="h-4 w-4 shrink-0 animate-spin" />,
      text: 'Conexão restabelecida. Sincronizando pedidos pendentes...',
    },
    online_success: {
      bg: 'bg-emerald-600/90 dark:bg-emerald-600/80 border-emerald-500/40 text-white',
      icon: <CheckCircle2 className="h-4 w-4 shrink-0" />,
      text: 'Online. Todos os pedidos locais foram sincronizados com sucesso!',
    },
  }[toastType];

  return (
    <AnimatePresence>
      <motion.div
        initial={{ opacity: 0, y: -50, scale: 0.95 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={{ opacity: 0, y: -20, scale: 0.95 }}
        transition={{ duration: 0.3, ease: 'easeOut' }}
        className="fixed top-28 left-1/2 -translate-x-1/2 z-50 w-full max-w-md px-4 pointer-events-none"
      >
        <div className={`pointer-events-auto flex items-center gap-3 py-3 px-5 border rounded-2xl shadow-xl backdrop-blur-md ${config.bg}`}>
          {config.icon}
          <p className="text-xs font-black tracking-wide leading-tight">{config.text}</p>
        </div>
      </motion.div>
    </AnimatePresence>
  );
}
