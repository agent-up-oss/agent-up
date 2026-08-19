const markerCache = 'agent-up-release-marker';
const markerUrl = '/__agent-up-active-release';

self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));

self.addEventListener('message', event => {
  if (event.data?.type !== 'INSTALL_RELEASE') return;
  event.waitUntil((async () => {
    const cacheName = `agent-up-release-${event.data.release.channel}-${event.data.release.sha}`;
    try {
      const cache = await caches.open(cacheName);
      await Promise.all(event.data.files.map(file => cache.put(file.path, new Response(file.body, {
        headers: { 'Content-Type': contentType(file.path) },
      }))));
      const marker = await caches.open(markerCache);
      await marker.put(markerUrl, new Response(cacheName));
      const cacheNames = await caches.keys();
      await Promise.all(cacheNames
        .filter(name => name.startsWith('agent-up-release-') && name !== markerCache && name !== cacheName)
        .map(name => caches.delete(name)));
      event.ports[0]?.postMessage({ ok: true });
    } catch (error) {
      await caches.delete(cacheName);
      event.ports[0]?.postMessage({ ok: false, error: String(error) });
    }
  })());
});

self.addEventListener('fetch', event => {
  if (event.request.method !== 'GET' || new URL(event.request.url).origin !== self.location.origin) return;
  event.respondWith((async () => {
    const requestUrl = new URL(event.request.url);
    if (requestUrl.searchParams.has('agent-up-recovery')) {
      await clearActiveRelease();
      return fetch(event.request);
    }
    const marker = await caches.open(markerCache);
    const active = await marker.match(markerUrl);
    if (active) {
      const cache = await caches.open(await active.text());
      const url = new URL(event.request.url);
      const path = url.pathname === '/' ? '/index.html' : url.pathname;
      const cached = await cache.match(path)
        ?? (event.request.mode === 'navigate' ? await cache.match(`${path}.html`) : undefined)
        ?? (event.request.mode === 'navigate' ? await cache.match('/index.html') : undefined);
      if (cached) return cached;
    }
    return fetch(event.request);
  })());
});

function contentType(path) {
  const extension = path.split('.').pop()?.toLowerCase();
  return ({
    css: 'text/css; charset=utf-8', html: 'text/html; charset=utf-8',
    js: 'text/javascript; charset=utf-8', json: 'application/json; charset=utf-8',
    png: 'image/png', svg: 'image/svg+xml', ttf: 'font/ttf', woff: 'font/woff', woff2: 'font/woff2',
  })[extension] ?? 'application/octet-stream';
}

async function clearActiveRelease() {
  const cacheNames = await caches.keys();
  await Promise.all(cacheNames
    .filter(name => name === markerCache || name.startsWith('agent-up-release-'))
    .map(name => caches.delete(name)));
}
