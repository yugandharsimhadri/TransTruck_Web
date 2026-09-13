# Deploying the Postgres build to a server

One document covering everything needed to take the Postgres-capable build
(`main` as of commit `9134099`) and run it on a **separate/new server** — not
the live production install. For the reasoning behind the migration itself,
see [../POSTGRES-MIGRATION.md](../POSTGRES-MIGRATION.md). For the data-copy
tool's own details (its summary format, how to reset a target for a re-run),
see [../tools/TransTrack.PgMigrate/README.md](../tools/TransTrack.PgMigrate/README.md).

> **This guide is for a fresh/separate server.** The real production machine
> (`C:\TransTruckWeb`, tunnelled to `ttapi.sivayaantechnologies.com`) still
> runs SQLite with real customer data untouched. Do not point these steps at
> it — see the last section of `POSTGRES-MIGRATION.md`'s runbook for what
> cutting *that* over actually requires.

---

## What you need before starting

- The published API build, copied to the new server. Built locally with:
  ```powershell
  dotnet publish src\TransTrack.Api\TransTrack.Api.csproj -c Release -o C:\TransTruckWeb-Postgres\publish
  ```
  This is **framework-dependent**, so the new server needs the **ASP.NET
  Core 10 Runtime** installed (not the full SDK) — check with:
  ```powershell
  dotnet --list-runtimes | Select-String "AspNetCore"
  ```
- A PostgreSQL server reachable from wherever the API will run — either
  installed on the new server itself, or a separate/managed instance on the
  network.
- Your dev machine keeps the source repo and the live SQLite file — you'll
  run the one-time data copy *from here*, pointed *at* the new server, so
  the new server never needs the .NET SDK or the source code at all.

You do **not** need `dotnet ef` on the new server. The published API applies
its own schema automatically on first startup (`DbBootstrapper.InitialiseAsync`
calls `Database.MigrateAsync()`) — starting the exe against an empty Postgres
database is enough to create every table.

---

## Step 1 — Create the Postgres role and database

Run from any machine with `psql` and network access to that Postgres server
(your dev machine, pointing `-h` at the new server instead of `localhost`):

```powershell
$env:PGPASSWORD = 'your-postgres-superuser-password'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h <postgres-host> -U postgres -c "CREATE ROLE transtrack_app LOGIN PASSWORD 'a-strong-password';"
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h <postgres-host> -U postgres -c "CREATE DATABASE transtruckweb OWNER transtrack_app;"
```

Replace `<postgres-host>` with `localhost` if Postgres runs on the new server
itself, or its real hostname/IP otherwise. Never run the app as the
`postgres` superuser role.

## Step 2 — Set the config on the new server

This is the **only** config change the app needs — no file to edit, no code
to change:

```powershell
$env:TRANSTRUCKWEB_PG_CONNECTION = "Host=<postgres-host>;Database=transtruckweb;Username=transtrack_app;Password=a-strong-password"
$env:ASPNETCORE_URLS = "http://localhost:5034"
```

To persist across reboots/new sessions instead of one window:
```powershell
[Environment]::SetEnvironmentVariable("TRANSTRUCKWEB_PG_CONNECTION", "Host=<postgres-host>;Database=transtruckweb;Username=transtrack_app;Password=a-strong-password", "Machine")
```

Set it → Postgres. Unset it → SQLite. This is how the same published build
serves either database.

**Never commit this connection string, or the password, to any tracked file.**

## Step 3 — Start the API once to create the schema

From the copied publish folder:

```powershell
cd C:\TransTruckWeb-Postgres\publish
.\TransTrack.Api.exe
```

Watch for this in the console:
```
Applying migrations to PostgreSQL: 20260913152646_InitialCreate
PostgreSQL database ready.
Now listening on: http://localhost:5034
```
Leave it running (or `Ctrl+C` and restart later as a service).

## Step 4 — Copy the data

Run from your **dev machine** — it has the source repo and the live SQLite
file — pointed at the new server's Postgres:

```powershell
$env:TRANSTRUCKWEB_PG_CONNECTION = "Host=<postgres-host>;Database=transtruckweb;Username=transtrack_app;Password=a-strong-password"
cd C:\Users\yugan\source\repos\yugandharsimhadri\TransTrack\TransTruck_Web
dotnet run --project tools\TransTrack.PgMigrate -- --sqlite "C:\TransTruckWeb\DB\TransTruckWeb.db" --yes
```

Check the `=== Migration summary ===` block: `Result: SUCCESS` and a row
count per table. It refuses to run against a database that already has
rows — see the tool's own README if you need to reset and re-run it.

## Step 5 — Verify

```powershell
Invoke-WebRequest http://<new-server-host>:5034/swagger/index.html -UseBasicParsing
```
Should return `200`. Then sign in through the frontend and confirm real
data loads (trips, vehicles, etc.).

## Step 6 — Frontend (if also deploying it here)

The frontend build already in the repo (`web/transtrack-web`, `.next` +
`node_modules`) is baked with `NEXT_PUBLIC_API_URL=http://localhost:5034`.
If the API is on a different host/port on the new server, update
`.env.local` and rebuild before copying:

```powershell
cd web\transtrack-web
# edit .env.local: NEXT_PUBLIC_API_URL=http://<new-server-host>:5034
npm run build
```

Copy the resulting `.next` folder + `node_modules` + `package.json` to the
new server and run:
```powershell
npm run start
```

This is a separate, plain Node build — **not** the Cloudflare Pages ZIP
(`deploy/frontend-artifacts/transtruck-web-pages-deploy.zip`), which is
shaped for Cloudflare's Workers runtime and points at the real production
API. Don't mix the two up.

---

## Quick reference

| Item | Value |
|---|---|
| API publish command | `dotnet publish src\TransTrack.Api\TransTrack.Api.csproj -c Release -o C:\TransTruckWeb-Postgres\publish` |
| API build path (local) | `C:\TransTruckWeb-Postgres\publish` |
| Frontend build path (local) | `web\transtrack-web` (`.next` + `node_modules`, run via `npm run start`) |
| Cloudflare Pages ZIP (production FE, unrelated to this) | `deploy\frontend-artifacts\transtruck-web-pages-deploy.zip` |
| Config switch | `TRANSTRUCKWEB_PG_CONNECTION` env var — set = Postgres, unset = SQLite |
| Schema creation | Automatic, on the API's first startup against an empty database |
| Data copy tool | `dotnet run --project tools\TransTrack.PgMigrate -- --sqlite "<path>" --yes` |
| Data copy tool docs | [../tools/TransTrack.PgMigrate/README.md](../tools/TransTrack.PgMigrate/README.md) |
| Full migration analysis + history | [../POSTGRES-MIGRATION.md](../POSTGRES-MIGRATION.md) |
