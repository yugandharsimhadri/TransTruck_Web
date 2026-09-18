# Deploying LorryOwner

Two independent pieces, deployed separately:

- **API** (`src/TransTrack.Api`) — runs on the production server at
  `C:\server\loapi`, port **6041**, on **PostgreSQL** (local to that server),
  exposed publicly as `https://loapi.lorryowner.com` through a Cloudflare
  Tunnel.
- **Web app** (`web/transtrack-web`) — deployed to **Cloudflare Pages** as
  `https://lorryowner.com`, by uploading a ZIP.

This is the *routine* guide — what to do every time a change ships. The
one-time move from SQLite to Postgres (done 2026-09-18) is documented
separately in [POSTGRES-MIGRATION.md](POSTGRES-MIGRATION.md); nothing there
needs repeating.

**Every change should ship with its deployment steps.** If a change needs
anything beyond the routine below — a new setting, a manual data fix, a
different order — that is written down alongside the change, in the commit
message or in this file, not left to be rediscovered on the server.

**When both halves change, deploy the API first, then the frontend.** An old
API ignores JSON properties it doesn't know, so a new frontend against an old
API mostly survives — but a new screen calling an endpoint that doesn't exist
yet fails outright. API first closes that window. An API-only change never
needs a frontend deploy, and vice versa.

> **Names in paths and code still say TransTrack / TransTruck.** That is
> deliberate. The rebrand changed what users see — the app name, logo, icons,
> the `lorryowner.com` domains — and left internal identifiers alone: the
> `src/TransTrack.*` projects, the `TRANSTRUCKWEB_*` variable names, the JWT
> issuer/audience, the `transtruckweb` database name. Renaming those buys
> nothing user-facing, and changing the JWT issuer would sign every user out.

---

## API

### The one file that configures it

`C:\server\loapi\appsettings.json` **on the server** is the entire
configuration. It holds:

| Setting | What it is |
|---|---|
| `Urls` | `http://localhost:6041` — the port the tunnel forwards to |
| `PostgresConnectionString` | how to reach the database, **including the password** |
| `Jwt.Key` | the session-signing key, generated once at cutover — **never change it casually**, it signs everyone out |
| `Cors.AllowedOrigins` | the sites allowed to call the API (`lorryowner.com`) |
| `EnableSwagger`, `LogRequests`, log paths, upload limits | see the comments in the file |

No environment variables are required. `ASPNETCORE_ENVIRONMENT` is not set
on the server, and does not need to be — the base file is production-correct
on its own.

**The copy in the repo ships placeholders.** The connection string is `null`
and the key is the published dev value. The server's file has the real ones.
That is the whole reason the deploy steps below are careful about that one
file.

### Every deploy — step by step

**1. Build, on the dev machine:**

```powershell
cd C:\Users\yugan\source\repos\yugandharsimhadri\TransTrack\TransTruck_Web
dotnet publish src\TransTrack.Api\TransTrack.Api.csproj -c Release -o C:\TransTruckWeb-Postgres\publish
```

**2. Copy it to the server — without `appsettings.json`.**

The published folder contains a placeholder `appsettings.json`. Copying it
over the server's would replace the real connection string and JWT key with
the placeholders, and the API would refuse to start (no connection string)
or, worse, start with the public dev key. So exclude that one file:

```powershell
# On the server, after getting the publish folder onto it:
robocopy C:\TransTruckWeb-Postgres\publish C:\server\loapi /E /XF appsettings.json
```

`/XF appsettings.json` is the important part. If you move the folder by
hand instead (USB, RDP paste), **delete `appsettings.json` from the copy
first**, then paste.

Do this while the API is still running — the files copy fine; it only
matters that it restarts afterwards.

**3. Restart the API** (stop the running `TransTrack.Api.exe`, start it the
same way it normally runs).

If the change included a database migration, it is applied automatically on
this start. Expect to see it on the console:

```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '2026xxxxxxxxxx_WhateverItIsCalled'.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:6041
```

With no migration, expect `No migrations were applied. The database is
already up to date.` instead. Either way, `Now listening` is the line that
says it started.

**4. Verify:**

```powershell
Invoke-RestMethod http://localhost:6041/api/health
```

Expect `status: Healthy`, `database: PostgreSQL`, `databaseConnected: True`,
and a `version` whose suffix is the commit you just built (e.g.
`1.0.0+2e28584…`). If `version` still shows the old commit, the copy in step
2 did not land where the running exe is.

Then sign in at `https://lorryowner.com` once.

### If something is wrong — rollback

Keep the previous build: before step 2, `Copy-Item C:\server\loapi
C:\server\loapi-previous -Recurse`. Rolling back is stopping the API,
copying that folder back over `C:\server\loapi` (this time *including* its
`appsettings.json`, which is the same real one), and starting it.

A rollback does **not** undo a database migration that already ran. The
schema stays at the newer version; the older build usually runs against it
fine (migrations here have all been additive), but check the migration's
`Down()` if it dropped or renamed anything.

### Backups — this changed with Postgres, and needs setting up

On SQLite the API took its own daily copy of the database file. **On
Postgres it does not** — that machinery is file-based and is skipped. Until a
backup is scheduled, production has none.

A nightly dump is one command; put it in Task Scheduler:

```powershell
$env:PGPASSWORD = 'the-transtrack_app-password'
& "C:\Program Files\PostgreSQL\18\bin\pg_dump.exe" -h localhost -U transtrack_app -d transtruckweb -F c -f "C:\TransTruckWeb\DBBackup\transtruckweb-$(Get-Date -Format yyyyMMdd).dump"
```

Restore with `pg_restore -h localhost -U transtrack_app -d transtruckweb --clean <file>`.

**Uploaded vehicle documents are a second thing to keep.** They live on disk
(`VehicleDocumentDirectory` in `appsettings.json`, default
`C:\TransTruckWeb\VehicleDocs`) — the database stores only a reference. Back
that folder up with the dump; a dump alone restores rows pointing at files
that are gone.

### Reading logs and testing endpoints on the server

- `https://loapi.lorryowner.com/swagger` — every endpoint, callable with a
  token from `/api/auth/login`.
- `GET /api/health` — anonymous; is it up, which database, does it connect.
- `GET /api/health/logs?lines=200` — the log tail, EnterpriseAdmin only.
- Failed requests are always written to the log file; set `LogRequests: true`
  in `appsettings.json` (and restart) to log every request while chasing
  something, then set it back.

**EnterpriseAdmin's login is unchanged**: username `EnterpriseAdmin`,
password `SivAyAAn@HMS` — a fixed constant in
[`AuthService.cs`](src/TransTrack.Data/AuthService.cs), separate from the
JWT key, identical on every machine.

### The tunnel (one-time, already done)

`cloudflared` runs on the server and forwards `loapi.lorryowner.com` to
`localhost:6041`. It is independent of the API — restarting the API does not
touch it. If it ever needs recreating, see
[`deploy/cloudflared-config.sample.yml`](deploy/cloudflared-config.sample.yml).
The tunnel may still be named `transtruck-api` from before the rename; that
is fine, a tunnel's name is fixed at creation.

### Running locally against the dev frontend

```powershell
cd src\TransTrack.Api
dotnet run
```

Listens on `http://localhost:5034` — `appsettings.Development.json` sets
`Urls` back to 5034 over the base file's 6041, and `dotnet run` loads that
overlay. **This build needs a Postgres connection string to start** — it
cannot run on SQLite. For local work set it in the shell rather than in a
file, since every `appsettings*.json` here is committed:

```powershell
$env:TRANSTRUCKWEB_PG_CONNECTION = "Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=..."
dotnet run
```

> `deploy\publish-api.ps1` and `deploy\run-api.ps1` are from the old
> `C:\TransTruckWeb\publish` + SQLite layout and are not used for the
> production server. `run-api.ps1` in particular would override `Jwt.Key`
> from a secrets file — do not run it on the server.

---

## Web app — Cloudflare Pages

### Every deploy

**1. Build the package, on the dev machine, with no `next dev` running:**

```powershell
cd C:\Users\yugan\source\repos\yugandharsimhadri\TransTrack\TransTruck_Web
.\deploy\frontend-artifacts\make-pages-package.ps1
```

It pins the API URL to `https://loapi.lorryowner.com` regardless of any
`.env.local` on the machine, applies the shim Pages needs to serve static
files, checks its own output, and writes
`deploy\frontend-artifacts\transtruck-web-pages-deploy.zip`. Its last lines
must say `API URL check: ok (localhost 0, api 1)` and `ASSETS shim: applied`
— it refuses to produce a package otherwise.

**2. Commit the ZIP.** It is kept in the repo on purpose, as the
last-known-good deployable.

**3. Upload it:** Cloudflare dashboard → Workers & Pages → the `lorryowner`
project → **Create deployment** → upload the ZIP's **contents** (not the ZIP
as a nested folder — `_worker.js` and `_next/` must be at the top level).
Custom domain and compatibility flags are already set on the project and
carry over.

The full packaging notes, including the one-time project setup, are in
[`deploy/frontend-artifacts/pages-deploy-README.txt`](deploy/frontend-artifacts/pages-deploy-README.txt).

### Does the frontend need redeploying for this change?

Only if something under `web/transtrack-web` changed:

```powershell
git log --oneline <last-deployed-commit>..HEAD -- web/transtrack-web
```

Empty output means the deployed site is already current. The API and the
frontend are independent — an API-only change (including database changes)
never needs a frontend deploy, and vice versa.

### Plain local dev

```powershell
cd web\transtrack-web
npm run dev
```

Hits `http://localhost:5034` per `.env.local`. Stop it before running the
packaging script — it holds a lock on the build output.

---

## Cross-origin auth, and why nothing needed to change there

`lorryowner.com` and `loapi.lorryowner.com` are different *subdomains* but
the same *registrable domain*, and both are HTTPS. For the `SameSite`
cookie rules that is what matters — subdomains of one registrable domain
count as "same-site" to a browser, so the `SameSite=Strict` session cookie
(set in [`AuthCookie.cs`](src/TransTrack.Api/Auth/AuthCookie.cs)) is sent on
cross-subdomain API calls exactly as it needs to be.

What *did* need attention: the tunnel terminates HTTPS at Cloudflare's edge
and forwards plain HTTP to `localhost:6041`, so without help the API would
think every request arrived insecurely and refuse to set the `Secure`
cookie. `Program.cs` trusts the tunnel's `X-Forwarded-Proto` header — see the
comment above `ForwardedHeadersOptions` there.

The one thing that *has* broken here is CORS: the API only answers browsers
from the origins in `Cors.AllowedOrigins`. If a login fails with a generic
"Something went wrong" and the browser console shows a CORS error, that list
on the server is wrong.

---

## What's committed vs. what lives only on the server

| Committed (placeholders where secret) | On the server only |
|---|---|
| `appsettings.json` — every setting, with `PostgresConnectionString: null` and the dev `Jwt.Key` | `C:\server\loapi\appsettings.json` — the same file with the real connection string and key |
| `.env.production` (public API URL) | the PostgreSQL database `transtruckweb` |
| `deploy\frontend-artifacts\transtruck-web-pages-deploy.zip` (last-known-good frontend) | `VehicleDocs\` (uploaded documents) |
| `brand/`, `wrangler.jsonc`, `open-next.config.ts` | `C:\TransTruckWeb-Postgres\publish\` on the dev machine (build output) |

The API build output is not committed, unlike the frontend ZIP: it is ~180 MB
unpacked, past GitHub's per-file limit and permanent bloat in a public repo.
It is rebuilt from source with the one `dotnet publish` line above whenever
it is needed.
