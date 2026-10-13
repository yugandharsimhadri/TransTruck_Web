# Running the API in Docker on Linux

**Written:** 2026-10-13
**Status: plan only — nothing implemented.**
**Scope:** the API and its PostgreSQL database. The frontend is unaffected —
it stays on Cloudflare Pages.

Written by reading the code rather than from general Docker advice. Every
blocker below cites where it came from, so it can be re-checked when the code
moves on.

---

## Why this is worth doing

Not for scale — the database is small and one container is plenty. The case is
that it removes the entire class of failure that has cost the last fortnight:

- the ASP.NET Core **Hosting Bundle** missing on a new machine (`500.19`,
  `0x8007000d`) — twice, on two different machines
- IIS **app pools** stopped by rapid-fail protection and not retrying
- `appsettings.json` **overwritten by a redeploy**, replacing the real
  connection string and JWT key with the repo's placeholders
- a dump **restored by the wrong role**, leaving tables the app cannot read
  (`42501`)
- a connection string pointing at a host that was never the problem

None of these exist with an image. It either runs or it does not, identically
on every machine, and `docker compose up` on a bare VM reproduces the whole
stack. That is the real return here: **repeatability**, not performance.

---

## Blockers found in the code

Ordered worst first. The first two need code or config changes; the rest are
container configuration.

- [ ] **D1 — 🔴 `Urls` binds loopback only.**
      `appsettings.json:30` sets `"Urls": "http://localhost:6041"`. Inside a
      container `localhost` is the container itself, so a published port
      connects to nothing and the symptom is a silent connection refused with a
      perfectly healthy-looking app. Must become `http://+:8080` (or be left
      unset and driven by `ASPNETCORE_URLS`).
      *This is the single most likely thing to waste an afternoon.*

- [ ] **D2 — 🔴 QuestPDF asks for Arial, which Linux does not have.**
      `src/TransTrack.Api/Documents/PdfHelpers.cs:21` —
      `page.DefaultTextStyle(x => x.FontFamily(Fonts.Arial)...)`. A stock
      .NET container has no fonts and no fontconfig, so **every PDF breaks**:
      the LR, the per-trip Bill, the party Bill, every report export.
      Two parts to the fix, and both are needed:
      1. install `fontconfig` and a font package in the image — Liberation
         Sans is the right choice, being metric-compatible with Arial so
         nothing re-flows;
      2. either register that family with QuestPDF
         (`FontManager.RegisterFont`) or change `Fonts.Arial` to the family
         name actually installed.
      *Verify by generating one of each document and comparing against a
      Windows-produced copy, not just by checking it does not throw.*

- [ ] **D3 — 🟠 The data-root default is Windows-shaped.**
      `src/TransTrack.Data/AppPaths.cs:44` falls back to
      `Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\", "TransTruckWeb")`.
      On Linux that produces a literal `C:\TransTruckWeb` **as a relative
      directory name** next to the binary. Harmless-looking, and it means
      uploads silently land inside the container and vanish on the next
      deploy. Set `DataRoot` (or `TRANSTRUCKWEB_ROOT`) explicitly — never rely
      on the default in a container.

- [ ] **D4 — 🟠 The log directory default is not writable.**
      `src/TransTrack.Data/AppLog.cs:47` falls back to
      `Environment.SpecialFolder.CommonApplicationData`, which is `/usr/share`
      on Linux — not writable by a non-root container user. `AppLog` degrades
      to the temp folder rather than failing, so the cost is losing the logs
      exactly when they are wanted. Set `LogDirectory` explicitly.

- [ ] **D5 — 🟡 Globalization.**
      Money and dates are formatted throughout the PDF and Excel builders
      (`BillDocument`, `LrDocument`, `PartyBillDocument`, `ReportPdfBuilder`).
      Use the Debian-based `mcr.microsoft.com/dotnet/aspnet:10.0`, which ships
      ICU. **Do not use Alpine, and do not set `InvariantGlobalization=true`** —
      either will quietly change how amounts print.

- [ ] **D6 — 🟡 Timezone.**
      Containers run UTC. The stored data is UTC-correct already (that was
      settled during the Postgres move), so this only affects
      `DateTime.Now` used for log and backup filenames — a file dated a day
      out when read at night. Set `TZ=Asia/Kolkata`.

- [ ] **D7 — 🟡 Migrations apply on startup.**
      `DbBootstrapper.InitialiseAsync` runs `MigrateAsync()` as the app boots.
      Fine at one replica. **Two replicas starting together will race on the
      same migration.** Keep it single-replica, or split migrations into an
      init container that runs to completion first.

### Checked and *not* a problem

- **Document paths are case-safe.** Linux filesystems are case-sensitive;
  stored paths are lowercase GUID hex (`StoredPath` in `Documents`), so they
  move across unchanged.
- **No Windows-only APIs** beyond D3/D4 — no registry, no `System.Drawing`, no
  shelling out to `.exe`.
- **The tests already run on PostgreSQL** as of 2026-09-20 — `UatDatabase`
  creates a throwaway `transtruck_uat_*` database per run, and the unit tests
  build their schema from the model. So a containerised CI run is possible
  without further work there.
- **A health endpoint already exists** — `GET /api/health` reports whether the
  database actually opens, which is exactly what `HEALTHCHECK` and
  `depends_on: condition: service_healthy` need.

---

## Physical layout on the host

The two directories that must outlive the container are the uploaded documents
and the database. Everything else is rebuildable.

| Purpose | Host path | In container | Notes |
|---|---|---|---|
| Uploaded documents | `/srv/lorryowner/docs` | `/var/lib/lorryowner/docs` | **Bind mount.** These are files on disk; the database stores only a reference. Losing them is the failure already hit once during a server move |
| Logs | `/srv/lorryowner/logs` | `/var/log/lorryowner` | Bind mount, so they can be read without entering the container |
| PostgreSQL data | named volume `pgdata` | `/var/lib/postgresql/data` | A named volume, not a bind mount — avoids host permission problems |
| `pg_dump` output | `/srv/lorryowner/backups` | — | Written by a **host** cron job, not from inside a container |
| Application | — | `/app` | Immutable, comes from the image |

Back up `/srv/lorryowner/docs` **and** the dumps together. Restoring one
without the other leaves records pointing at documents that are not there.

---

## Shape of the stack

Three services:

| Service | Image | Notes |
|---|---|---|
| `api` | built from `src/TransTrack.Api` on `mcr.microsoft.com/dotnet/aspnet:10.0` | non-root user; `HEALTHCHECK` curling `/api/health`; the two bind mounts above |
| `postgres` | `postgres:18` | named volume; its own healthcheck via `pg_isready` |
| `cloudflared` | `cloudflare/cloudflared` | token in the environment — replaces the Windows service, and removes the "marked for deletion" class of problem entirely |

`api` should `depends_on` postgres with `condition: service_healthy`, so a
cold `compose up` does not race the database.

### Configuration — all environment, no baked secrets

Every one of these is already supported by the app; no new settings are
needed.

```
TRANSTRUCKWEB_PG_CONNECTION=Host=postgres;Database=transtruckweb;Username=transtrack_app;Password=...
TRANSTRUCKWEB_ROOT=/var/lib/lorryowner
ASPNETCORE_URLS=http://+:8080
Jwt__Key=<the existing production key>
TZ=Asia/Kolkata
```

Two things worth saying plainly:

- **Reuse the existing `Jwt__Key`.** A new one signs every user out. It is
  48 bytes of base64 and lives only in the server's `appsettings.json` today —
  copy it into the environment file before the cutover, and keep a copy
  somewhere offline.
- **Keep the `.env` file out of git.** `appsettings.json` in the image should
  keep shipping placeholders, exactly as it does now; the environment supplies
  the real values. This also retires the `/XF appsettings.json` copy rule that
  a Windows redeploy currently depends on remembering.

### Note on the database

Running PostgreSQL in the same compose file is simplest and fine at this size.
The alternative — Azure Database for PostgreSQL — removes backup and patching
work but costs more and needs `SSL Mode=Require` in the connection string.
Either works; the container layout above does not change, only the host in the
connection string.

---

## Migration path

Each step ends somewhere checkable, and production keeps running throughout.

1. **Fix D1 and D2 on Windows first.** Both are provable before any container
   exists: the binding change is config, and the font work can be verified by
   generating a Bill and an LR and diffing them against today's output.
   *Verify:* the existing suites pass, PDFs look identical.
2. **Build the image and run it locally** against a throwaway Postgres loaded
   from a dump. *Verify:* `/api/health` reports `Healthy`/`PostgreSQL`, and one
   of each document generates correctly — this is where D2 will show up if the
   font work was incomplete.
3. **Run the UAT suite against the container.** It already provisions its own
   database per run.
4. **Stand the stack up on the VM beside IIS**, on a different port, pointed at
   a *copy* of the database. Compare figures against production.
5. **Cut over**: stop IIS, repoint the tunnel's public hostname to the
   container's port, start the stack. The SQLite-era rollback story is gone —
   the rollback here is starting IIS again, so leave it installed and
   configured until a week of real use has passed.
6. **Decommission IIS** and remove the Hosting Bundle dependency from the
   deployment docs.

### Effort

| Step | Effort |
|---|---|
| D1 + D2 (binding, fonts, verifying PDFs) | 0.5–1 day |
| Dockerfile + compose + healthchecks | 0.5 day |
| Data move and reconciliation | 0.5 day |
| Cutover | 0.5 day |
| **Total** | **2–2.5 days** |

The font work is the only part that touches application code, and the only
part with a real chance of surprising anyone.

---

## Recommendation

**Worth doing, and the timing is good.** The move to a cloud VM has already
cost the Hosting Bundle discovery twice; doing it a third time on the next
machine is avoidable. Containers also make the backup story explicit rather
than implied, which matters more now that the data lives somewhere other than
a desk.

If it is not done now, the one thing worth taking from this document anyway is
**D2** — the Arial dependency is a latent trap for any non-Windows host,
including a future managed platform, and is cheap to fix while nothing depends
on it.

---

## Related

- [DEPLOYMENT.md](DEPLOYMENT.md) — the current Windows/IIS deployment this
  would replace
- [POSTGRES-MIGRATION.md](POSTGRES-MIGRATION.md) — the database move, and the
  runbook whose restore procedure still applies for loading a dump into the
  container's database
- [ANDROID-APP.md](ANDROID-APP.md) — the other open deployment question;
  independent of this one
