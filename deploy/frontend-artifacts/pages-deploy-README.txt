TransTrack frontend — Cloudflare Pages upload package
=======================================================

This zip is laid out for Cloudflare PAGES "Advanced Mode": _worker.js sits
at the same root level as the static files (_next/, favicon.ico, etc.),
not nested in a subfolder. Pages runs _worker.js for every request and
automatically gives it an ASSETS binding pointing at the sibling static
files, so no separate assets-binding setup is needed like the Workers
version.

Do NOT re-zip a subfolder (e.g. don't zip "transtruck-web-pages-deploy/" as
one entry) — the ZIP's top level must contain _worker.js and _next/ etc.
directly, otherwise Pages will 404 exactly like last time.

One-time setup
---------------
1. Cloudflare dashboard -> Workers & Pages -> Create -> Pages ->
   Upload assets.
2. Project name: transtruck-web (or whatever you like — Pages projects
   don't need to match a binding name the way Workers did).
3. Drag/upload this zip's CONTENTS (not the zip as a nested folder).
4. After the first deploy, go to the project's Settings -> Functions:
   - Compatibility date: 2026-08-10 (or later)
   - Compatibility flags (production AND preview): nodejs_compat,
     global_fetch_strictly_public
   Then redeploy (Pages needs a fresh deployment to pick up flag changes —
   re-upload the same zip again, or use "Retry deployment").
5. Settings -> Custom domains -> Add transtruck.sivayaantechnologies.com.
   (Remove it from the old Pages/Worker attempt first if it's still
   attached there — a domain can only point to one project at a time.)

Known limitation
------------------
The build was originally produced for Cloudflare Workers, which wires up
an extra "WORKER_SELF_REFERENCE" service binding for background/on-demand
revalidation calls the app makes to itself. Pages Direct Upload has no
dashboard option to bind a Pages project to itself this way, so that binding
is simply absent here. Normal page loads and API calls are unaffected —
this only matters for a narrow revalidation code path. If you notice any
page throwing a server error (not a 404) after this deploys cleanly, send
me the error and I'll check if it's related.

Every future update
---------------------
From web/transtrack-web:
   npx opennextjs-cloudflare build
   npx wrangler deploy --dry-run --outdir=.deploy-dryrun
Then rebuild the zip: copy everything from .open-next/assets/ to the zip
root, plus .deploy-dryrun/worker.js renamed to _worker.js at that same
root. Re-upload as a new deployment on the same Pages project (Settings ->
Deployments -> Create deployment / drag the new zip in) — no need to
redo the Custom domain or Compatibility flag steps, those stick.
