const CACHE_NAME = 'flappybrain-kiosk-v2'
const STATIC_ASSETS = ['/manifest.json', '/icon-192.png', '/icon-512.png']

self.addEventListener('install', (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(STATIC_ASSETS).catch(() => undefined))
      .then(() => self.skipWaiting())
  )
})

self.addEventListener('activate', (event) => {
  event.waitUntil(
    caches.keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))))
      .then(() => self.clients.claim())
  )
})

self.addEventListener('fetch', (event) => {
  const req = event.request
  if (req.method !== 'GET') return

  const url = new URL(req.url)

  // Always network-first for HTML navigation (never cache the page shell)
  if (req.mode === 'navigate' || url.pathname === '/') {
    event.respondWith(
      fetch(req).catch(() => caches.match(req).then((c) => c || Response.error()))
    )
    return
  }

  // API: network only
  if (url.pathname.startsWith('/api/')) {
    event.respondWith(fetch(req).catch(() => Response.error()))
    return
  }

  // Static assets: cache-first
  event.respondWith(
    caches.match(req).then((cached) => {
      if (cached) return cached
      return fetch(req).then((res) => {
        if (res.ok) {
          const clone = res.clone()
          caches.open(CACHE_NAME).then((cache) => cache.put(req, clone)).catch(() => undefined)
        }
        return res
      })
    })
  )
})
