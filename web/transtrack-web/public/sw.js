// Minimal offline shell — caches the app's own static assets so the shell
// still loads with no connection; API calls always go to the network (never
// cached, since fleet data must never be served stale to a driver mid-trip).
//
// Bumped whenever what is cached changes, so every existing client drops the
// previous copy instead of being stranded on it.
const CACHE_NAME = "lorryowner-shell-v4";
// addAll is all-or-nothing: one 404 here and the whole install rejects, which
// silently costs the offline shell. Keep this list to files that certainly exist.
//
// The brand images are here rather than left to the runtime cache because they
// are on the critical path of the first screen anyone sees — precaching them
// means the sign-in page paints its logo immediately on a slow connection.
const SHELL_URLS = [
  "/",
  "/login",
  "/manifest.json",
  "/icon-192.png",
  "/lorryowner-logo.png",
  "/lorryowner-mark.png",
];

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

  // A page request is answered from the network first, and only falls back to
  // the cache when the network genuinely fails.
  //
  // This used to be cache-first like everything else, which quietly left every
  // returning visitor one deploy behind: the HTML shell carries the compiled
  // JS references, and that JS carries the API URL, so a browser holding an
  // older /login page kept calling an API host that had since moved and could
  // not sign in — while a browser with an empty cache signed in fine. Being a
  // deploy behind is never worth it for a document; for the hashed assets
  // below it costs nothing, since a changed file gets a changed URL.
  const isPageRequest =
    event.request.mode === "navigate" || event.request.destination === "document";

  if (isPageRequest) {
    event.respondWith(
      fetch(event.request)
        .then((response) => {
          if (response.ok) {
            const clone = response.clone();
            caches.open(CACHE_NAME).then((cache) => cache.put(event.request, clone));
          }
          return response;
        })
        .catch(() => caches.match(event.request).then((cached) => cached ?? caches.match("/login"))),
    );
    return;
  }

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
