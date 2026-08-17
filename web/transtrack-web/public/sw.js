// Minimal offline shell — caches the app's own static assets so the shell
// still loads with no connection; API calls always go to the network (never
// cached, since fleet data must never be served stale to a driver mid-trip).
const CACHE_NAME = "lorryowner-shell-v2";
// addAll is all-or-nothing: one 404 here and the whole install rejects, which
// silently costs the offline shell. Keep this list to files that certainly exist.
const SHELL_URLS = ["/", "/login", "/manifest.json", "/icon-192.png", "/lorryowner-logo.png"];

self.addEventListener("install", (event) => {
  event.waitUntil(caches.open(CACHE_NAME).then((cache) => cache.addAll(SHELL_URLS)));
  self.skipWaiting();
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))),
    ),
  );
  self.clients.claim();
});

self.addEventListener("fetch", (event) => {
  const url = new URL(event.request.url);

  // Never intercept API calls — those must always hit the real server.
  if (url.pathname.startsWith("/api/")) return;
  if (event.request.method !== "GET") return;

  event.respondWith(
    caches.match(event.request).then((cached) => {
      const network = fetch(event.request)
        .then((response) => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => cached);
      return cached ?? network;
    }),
  );
});
