# Deploying LorryOwner

Two independent pieces, deployed separately:

- **API** (`src/TransTrack.Api`) — runs on your local server, port **6041**,
  exposed publicly as `https://loapi.lorryowner.com` through a
  Cloudflare Tunnel.
- **Web app** (`web/transtrack-web`) — deployed to Cloudflare Workers as
  `https://lorryowner.com`.

Everything below is scripted. The only steps that can't be scripted are the
two that inherently require *you* to prove account ownership to Cloudflare —
those are called out explicitly.

---

## API — local server, port 6041

### One-time setup
1. `cloudflared` installed and logged in to your Cloudflare account
   (`cloudflared tunnel login` — this is the one unavoidable manual step;
   nothing can prove you own the Cloudflare account except you).
2. Create the tunnel and route the hostname to it — see
   [`deploy/cloudflared-config.sample.yml`](deploy/cloudflared-config.sample.yml)
   for the exact commands and a ready-made `config.yml` to fill in and save.
3. Run the tunnel (`cloudflared tunnel run <your-tunnel-name>`, or
   `cloudflared service install` to keep it running across reboots).

> **If you set the tunnel up before the LorryOwner rename**, it is still
> called `transtruck-api` — a tunnel's name is fixed when it's created, and
> the rebrand did not (and could not) change it. Keep using that name, or
> create a fresh `sivayaan-local-server` tunnel and re-point the DNS route. The
> sample config uses the new name because it's written for a first-time
> setup.

### Every deploy after that (build + run)
```powershell
cd C:\Users\yugan\source\repos\yugandharsimhadri\TransTrack\TransTruck_Web
.\deploy\publish-api.ps1
.\deploy\run-api.ps1
```

`publish-api.ps1` builds a Release copy to `C:\TransTruckWeb\publish`.
`run-api.ps1` sets `ASPNETCORE_ENVIRONMENT=Production` and
`ASPNETCORE_URLS=http://localhost:6041`, then starts it. **First run only:**
it generates a random JWT signing key and saves it to
`C:\TransTruckWeb\secrets\jwt.key` — every run after that reuses the same
file, so existing logins keep working across restarts. That file is
machine-local and never goes in git (this repo is public).

The database lives at `C:\TransTruckWeb\DB\TransTruckWeb.db`, entirely
outside the publish folder — republishing (even wiping and recreating
`C:\TransTruckWeb\publish`) never touches it.

**Uploaded vehicle documents are a second thing to keep.** They live at
`C:\TransTruckWeb\VehicleDocs` (configurable — see
`VehicleDocumentDirectory` in `appsettings.Production.json`), because the
database stores only a *reference* to each file, not the file itself. A
backup of the database alone therefore restores rows that point at
documents which are no longer there. **Back up `VehicleDocs` together with
`DB`.** The app handles the mismatch gracefully rather than erroring — the
vehicle simply reads as having no document uploaded — but the file is gone
all the same.

**EnterpriseAdmin's login is unchanged**: username `EnterpriseAdmin`,
password `SivAyAAn@HMS` — that's a fixed constant in
[`AuthService.cs`](src/TransTrack.Data/AuthService.cs), completely separate
from the generated JWT key above, so it's identical on every machine with no
setup at all.

### To run it locally against the dev frontend instead (unchanged from before)
```powershell
cd src\TransTrack.Api
dotnet run
```
Still listens on `http://localhost:5034` in Development, exactly as it
always has — `deploy\run-api.ps1` is additive, not a replacement for local dev.

---

## Web app — Cloudflare Workers

### One-time setup
```powershell
cd web\transtrack-web
npm install
npx wrangler login
```
`wrangler login` is the second unavoidable manual step, same reason as
`cloudflared tunnel login` above.

### Every deploy after that
```powershell
cd web\transtrack-web
npm run deploy
```
That's `opennextjs-cloudflare build && opennextjs-cloudflare deploy` under
the hood. It builds the Next.js app, bundles it for Cloudflare Workers, and
pushes it live — and because
[`wrangler.jsonc`](web/transtrack-web/wrangler.jsonc) declares
`lorryowner.com` as a custom-domain route, the domain
attaches automatically on that same deploy; no dashboard click needed
(lorryowner.com already has to be an active zone in your
Cloudflare account for this to work — it already is, since it's serving
`loapi.lorryowner.com` via the tunnel).

The API's URL is baked in at
[`.env.production`](web/transtrack-web/.env.production)
(`NEXT_PUBLIC_API_URL=https://loapi.lorryowner.com`) — committed
to the repo since it's a public URL, not a secret, so no dashboard
environment variable needs setting either.

### Local production-parity preview (runs it in Cloudflare's local Workers runtime)
```powershell
npm run preview
```
Use this to sanity-check a build behaves the same way it will once deployed,
without actually publishing it.

### Plain local dev (unchanged from before)
```powershell
npm run dev
```
Still hits `http://localhost:5034` per `.env.local` — nothing about the
Cloudflare setup touches ordinary local development.

---

## Cross-origin auth, and why nothing needed to change there

`lorryowner.com` and `loapi.lorryowner.com`
are different *subdomains* but the same *registrable domain*
(`lorryowner.com`), and both are HTTPS. For the `SameSite` cookie
rules that matters — subdomains of the same registrable domain count as
"same-site" to a browser, so the existing `SameSite=Strict` session cookie
(set in [`AuthCookie.cs`](src/TransTrack.Api/Auth/AuthCookie.cs)) is sent on
cross-subdomain API calls exactly as it needs to be. Nothing there had to
change.

What *did* need attention: the Cloudflare Tunnel terminates HTTPS at
Cloudflare's edge and forwards plain HTTP to `localhost:6041`, so without
help the API would think every request arrived over insecure HTTP —
breaking the `Secure` cookie flag. `Program.cs` now trusts the tunnel's
forwarded headers (`X-Forwarded-Proto`) so the app correctly sees these as
HTTPS requests. See the comment above `ForwardedHeadersOptions` in
[`Program.cs`](src/TransTrack.Api/Program.cs) for the full reasoning.

---

## What's committed vs. generated

| Committed | Generated locally, never committed |
|---|---|
| `appsettings.Production.json` (CORS origin, issuer/audience — no secret) | `C:\TransTruckWeb\secrets\jwt.key` (JWT signing key) |
| `.env.production` (public API URL) | `C:\TransTruckWeb\DB\TransTruckWeb.db` (live data) |
| `brand/` (logo artwork sources) | `C:\TransTruckWeb\VehicleDocs\` (uploaded vehicle documents) |
| `wrangler.jsonc`, `open-next.config.ts` | `C:\TransTruckWeb\publish\` (Release build output) |
| `deploy/*.ps1`, `deploy/*.sample.yml` | `.open-next/`, `.wrangler/` (frontend build output) |
