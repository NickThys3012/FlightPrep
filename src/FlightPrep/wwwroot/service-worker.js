const CACHE_NAME = 'flightprep-v1';
const STATIC_ASSETS = [
    '/',
    '/app.css',
    '/icons/icon-192.png',
    '/icons/icon-512.png',
    '/manifest.json'
];

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(CACHE_NAME).then(cache => cache.addAll(STATIC_ASSETS))
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => k !== CACHE_NAME).map(k => caches.delete(k)))
        )
    );
    self.clients.claim();
});

self.addEventListener('fetch', event => {
    // Only cache GET requests for same-origin static assets
    if (event.request.method !== 'GET') return;
    const url = new URL(event.request.url);
    if (url.origin !== location.origin) return;

    // For navigation requests (HTML pages), always go network-first
    if (event.request.mode === 'navigate') {
        event.respondWith(
            fetch(event.request).catch(() =>
                caches.match('/').then(r => r || new Response('Offline - open de app opnieuw wanneer je verbinding hebt.', {headers: {'Content-Type': 'text/plain'}}))
            )
        );
        return;
    }

    // For static assets (CSS, JS, images): cache-first
    event.respondWith(
        caches.match(event.request).then(cached => cached || fetch(event.request))
    );
});
