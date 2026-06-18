const STATIC_CACHE = 'versatus-static-v1';
const RUNTIME_CACHE = 'versatus-runtime-v1';

// Recursos estáticos obrigatórios para carregar a tela inicial offline
const PRECACHE_ASSETS = [
  '/login',
  '/manifest.json',
  '/icons/icon-192x192.png',
  '/icons/icon-512x512.png',
  '/favicon.ico'
];

self.addEventListener('install', event => {
  event.waitUntil(
    caches.open(STATIC_CACHE)
      .then(cache => {
        console.log('[SW] Fazendo pre-cache de arquivos estáticos obrigatórios...');
        return cache.addAll(PRECACHE_ASSETS);
      })
      .then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', event => {
  event.waitUntil(
    caches.keys().then(cacheNames => {
      return Promise.all(
        cacheNames
          .filter(name => name !== STATIC_CACHE && name !== RUNTIME_CACHE)
          .map(name => {
            console.log('[SW] Removendo cache obsoleto:', name);
            return caches.delete(name);
          })
      );
    }).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);

  // Apenas intercepta requisições do método GET
  if (request.method !== 'GET') {
    return;
  }

  // Não intercepta requisições destinadas à API do backend
  // A lógica de sincronização de dados local (Dexie.js/IndexedDB) do app cuidará da API.
  if (url.pathname.startsWith('/api/')) {
    return;
  }

  // Estratégia Stale-While-Revalidate para páginas e assets (JS, CSS, Imagens)
  event.respondWith(
    caches.match(request).then(cachedResponse => {
      if (cachedResponse) {
        // Busca na rede em background para atualizar o cache discretamente
        fetch(request)
          .then(networkResponse => {
            if (networkResponse.status === 200) {
              caches.open(RUNTIME_CACHE).then(cache => cache.put(request, networkResponse));
            }
          })
          .catch(() => { /* ignora falha de atualização em background */ });

        return cachedResponse;
      }

      // Se não estiver no cache, busca na rede
      return fetch(request)
        .then(networkResponse => {
          if (!networkResponse || networkResponse.status !== 200 || networkResponse.type !== 'basic') {
            return networkResponse;
          }

          const responseToCache = networkResponse.clone();
          caches.open(RUNTIME_CACHE).then(cache => cache.put(request, responseToCache));

          return networkResponse;
        })
        .catch(async () => {
          // Fallback offline se a rede falhar e for navegação de página HTML
          if (request.mode === 'navigate') {
            const loginPage = await caches.match('/login');
            if (loginPage) {
              return loginPage;
            }
          }
          return new Response('Rede indisponível (Offline)', { status: 503, statusText: 'Offline' });
        });
    })
  );
});
