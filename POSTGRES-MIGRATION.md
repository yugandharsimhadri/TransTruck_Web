# Moving from SQLite to PostgreSQL

**Status: implemented and merged to `main`** (commit `9134099`, 2026-09-13).
Everything below "Is this worth doing?" was written 2026-09-09 as a plan, before
any of it existed — kept as-is for the reasoning behind each decision. The
recommendation at the bottom ("do not do this now") was the honest answer
*at that time*; the user chose to proceed anyway, so read that section as
history, not current advice. **For how to actually run this, see the runbook
immediately below.**

---
## Runbook: SQLite → PostgreSQL cutover

Everything below has been **rehearsed end to end** against a copy of the real
production database (1276 rows, 132 trips, 8 companies) on 2026-09-17. Where a
step says what you should see, that is what it actually printed in the
rehearsal, not what it ought to print.

Run every step **on the production server** (`C:\server\loapi`'s machine).
Nothing here needs the .NET SDK or this repo on that machine — both tools are
published as self-contained folders you copy over.

---

### Before you start

**What this changes.** The API moves from the SQLite file to PostgreSQL. The
frontend does not change at all — it keeps calling the same URL, and nothing
about the API's shape changes.

**What is already true on that server:** PostgreSQL is installed, with no
database or role yet, and a previous attempt this week was rolled back — so
there may be a half-migrated `transtruckweb` database still sitting there.
Step 2 wipes it.

**Two folders to copy over first**, both published from this repo:

| Folder | What it is |
|---|---|
| `C:\TransTruckWeb-Postgres\publish` | the new API build |
| `C:\TransTruckWeb-Postgres\pgmigrate` | the one-time data-copy tool |

Put them anywhere on the server; the steps below assume the same paths.

**Prerequisite:** the ASP.NET Core 10 runtime, which the current API already
needs. Confirm with:

```powershell
dotnet --list-runtimes | Select-String "AspNetCore"
```

---

### 1. Generate a JWT signing key

Do this first, because step 4 needs the value.

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
$jwtKey = [Convert]::ToBase64String($bytes)
$jwtKey
```

Copy what it prints. This replaces the placeholder key that ships in the
repo — which is published on GitHub, so anyone can currently forge a token
(including an EnterpriseAdmin recovery token, which needs no user id and
would let them reset any company's password). Changing it signs everyone out
once, which is the only downtime it causes.

Keep this value. If you ever reinstall, reuse the **same** key or every
session is invalidated again.

### 2. Wipe the half-migrated database and create it fresh

This is the "clear out what we rolled back" step. It drops the database
entirely rather than truncating tables, so nothing survives from the earlier
attempt — not stale rows, not a partial schema.

```powershell
$env:PGPASSWORD = 'your-postgres-superuser-password'
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"

# Drops the database whether or not it exists, disconnecting anything using it.
& $psql -h localhost -U postgres -c "DROP DATABASE IF EXISTS transtruckweb WITH (FORCE);"

# Create the login the app uses. If it says the role already exists, that is
# fine — the next line sets its password either way.
& $psql -h localhost -U postgres -c "CREATE ROLE transtrack_app LOGIN PASSWORD 'a-strong-password';"
& $psql -h localhost -U postgres -c "ALTER ROLE transtrack_app WITH PASSWORD 'a-strong-password';"

& $psql -h localhost -U postgres -c "CREATE DATABASE transtruckweb OWNER transtrack_app;"
```

Pick your own value for `a-strong-password` — the same value goes into the
config in step 4. Use only letters and digits if you want to avoid quoting
surprises in either file.

Confirm it is empty:

```powershell
$env:PGPASSWORD = 'a-strong-password'
& $psql -h localhost -U transtrack_app -d transtruckweb -c "\dt"
```
Expect: `Did not find any tables.`

### 3. Back up what is running now

```powershell
Copy-Item -Path C:\server\loapi -Destination C:\server\loapi-sqlite-backup -Recurse
```

This folder is the rollback. The SQLite database file is not touched by
anything in this runbook, so the old build plus that file is a complete,
working system to return to.

### 4. Stop the API, and configure the new one

Downtime starts here.

Stop the running `TransTrack.Api.exe` (however it is normally started —
Task Scheduler, a service, or a console window).

Copy the new build in:

```powershell
Copy-Item -Path C:\TransTruckWeb-Postgres\publish\* -Destination C:\server\loapi -Recurse -Force
```

Now edit **`C:\server\loapi\appsettings.json`** — this one file is the entire
configuration; there is nothing else to set and no environment variable
required:

```jsonc
"Urls": "http://localhost:6041",

"PostgresConnectionString": "Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=a-strong-password",

"Jwt": {
  "Key": "<the value printed in step 1>",
  ...
},

"Cors": {
  "AllowedOrigins": [ "https://lorryowner.com", "https://www.lorryowner.com", "http://localhost:3000" ]
}
```

Three things to check before saving, each of which has bitten this migration
already:

- `Urls` is `6041` — the port the Cloudflare Tunnel forwards to.
- `Cors.AllowedOrigins` contains `https://lorryowner.com`. This is why the
  last attempt failed: the real origins used to live only in
  `appsettings.Production.json`, so an API started without
  `ASPNETCORE_ENVIRONMENT=Production` silently allowed localhost only, and
  the browser blocked every login with nothing logged on the server.
  They are in the base file now, so this works regardless.
- `Jwt.Key` is no longer `dev-only-signing-key-...`.

### 5. Create the schema

The database from step 2 is empty. The API builds its own schema the first
time it starts against an empty Postgres database. The copy tool in step 6
only moves rows **into** existing tables — it cannot create them, and fails
with `relation "Companies" does not exist` if you skip this.

```powershell
cd C:\server\loapi
.\TransTrack.Api.exe
```

**The first thing the console prints looks like an error. It is not.**
Expect two of these, right at the top:

```
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand ... SELECT "MigrationId", "ProductVersion" FROM "__EFMigrationsHistory" ...
```

That is EF Core asking the migrations-history table what has been applied
*before* it has created that table. On an empty database the table is not
there yet, so the query fails, EF catches it, creates the table, and
carries on. It happens on every fresh database and it happened in the
rehearsal. Then expect:

```
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260913152646_InitialCreate'.
info: Microsoft.EntityFrameworkCore.Migrations[20402]
      Applying migration '20260917033654_TenantLeadingIndexes'.
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:6041
```

**Do not judge this step by the console.** Confirm the result directly —
this is the check that actually matters:

```powershell
$env:PGPASSWORD = 'a-strong-password'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U transtrack_app -d transtruckweb -c "SELECT ""MigrationId"" FROM ""__EFMigrationsHistory"" ORDER BY 1;"
```

Expect exactly two rows:

```
 20260913152646_InitialCreate
 20260917033654_TenantLeadingIndexes
```

Then press `Ctrl+C` to stop the API. This start was only to create the
schema — do not leave it serving an empty database.

(The fuller messages — "Applying migrations to PostgreSQL: …",
"PostgreSQL database ready." — are written to the **log file**, not the
console. Read them any time with `/api/health/logs`, or open the newest
`transtrack-*.log` under `C:\ProgramData\TransTrack\logs`.)

### 6. Copy the data

```powershell
cd C:\TransTruckWeb-Postgres\pgmigrate
$env:TRANSTRUCKWEB_PG_CONNECTION = "Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=a-strong-password"
.\TransTrack.PgMigrate.exe --sqlite "C:\TransTruckWeb\DB\TransTruckWeb.db" --yes
```

Point `--sqlite` at whatever path that server's API is actually using — it is
the live file, and it must be the one on **this** machine, not a copy taken
earlier (it has kept changing while production ran on SQLite).

It prints a summary. Check three things:

- `Result: SUCCESS - every table copied, transaction committed.`
- The `TOTAL` looks like your data (the rehearsal moved 1276 rows).
- `Schema: source already current, no fixes needed.` — if it instead says it
  added something, that is fine; it means the source file predated a
  migration and the tool healed its own copy.

If it fails, nothing was written — the whole copy is one transaction — and
the summary names the exact table it stopped on.

### 7. Start the API for real

```powershell
cd C:\server\loapi
.\TransTrack.Api.exe
```

This time expect `No migrations were applied. The database is already up to
date.` followed by `PostgreSQL database ready.` Start it the way it is
normally started (service / Task Scheduler) rather than a console window if
that is how it runs day to day.

### 8. Verify before calling it done

```powershell
Invoke-RestMethod http://localhost:6041/api/health
```

Expect:

```
status            : Healthy
database          : PostgreSQL
databaseConnected : True
```

`Degraded` or a 503 means it started but cannot reach the database — check
the connection string in step 4.

Then, in a browser at `https://lorryowner.com`:

1. Sign in as a real user.
2. Open the dashboard — the figures should match what they were.
3. Open Trips and confirm the list loads.
4. Open one trip.

Downtime ends when this passes.

### 9. If something is wrong — rollback

```powershell
# Stop the new API, then:
Remove-Item C:\server\loapi\* -Recurse -Force
Copy-Item -Path C:\server\loapi-sqlite-backup\* -Destination C:\server\loapi -Recurse
```

Start the old executable. It reads the SQLite file, which nothing in this
runbook modified, so this is a complete revert.

Note: **the new build cannot fall back to SQLite** — clearing
`PostgresConnectionString` does not work, it refuses to start and says so.
Rollback means running the old build from the backup folder.

---

### Re-running the data copy

If production runs on SQLite again for a while (a rollback, a postponed
cutover), the rows in Postgres go stale. The copy tool refuses to run against
a database that already has data, so clear it first:

```powershell
$env:PGPASSWORD = 'a-strong-password'
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -h localhost -U transtrack_app -d transtruckweb -c '
TRUNCATE TABLE
  "AuditLogs","Cities","Companies","Counters","Documents","DriverLedgerEntries",
  "Drivers","ExpenseCategories","MaintenanceCategories","Owners","Parties",
  "Settlements","States","TripExpenses","TripTransactions","Trips","Users",
  "VehicleExpenseSchedules","VehicleExpenses","VehicleMaintenances","Vehicles"
CASCADE;'
```

That clears the rows and leaves the schema, so step 5 does not need repeating —
go straight back to step 6 with the current SQLite file.

---

### What was tested, and what the numbers were

Rehearsed on 2026-09-17 against a copy of the live database:

- **Data fidelity** — all 21 tables copied, 1276 rows, counts identical to the
  source, in one transaction.
- **Login** — the real production credential signs in and reaches the
  dashboard; trips, reports and master lists all load their real values.
- **Endpoint timings** (warm, real data): dashboard 13 ms, trips list 9 ms,
  audit feed 5 ms, trips report 28 ms, party report 9 ms. Nothing needs
  tuning at this data size.
- **Indexes** — all 15 tenant-leading indexes present after migration.

**One optimisation came out of this and is now in the schema.** `AuditLogs` is
the fastest-growing table, and its index was on `ChangedOn` alone while every
read of it also filters by `CompanyId`. Loaded to 700,000 rows with a
realistic skew (one busy company holding the most recent activity), a quieter
company's activity feed had to scan **300,696 rows to return 100**, taking
45.7 ms. With the index changed to `(CompanyId, ChangedOn)` the same query
touches only the rows it returns: **0.245 ms**, 186× faster, and 66× fewer
buffer reads. The same shape was applied to the six master tables whose
`Name`-only indexes had the same defect — the same number of indexes, just
with the tenant column leading. That is migration
`20260917033654_TenantLeadingIndexes`, applied automatically in step 5.

---
## Is this worth doing?

Honest answer for *this* app, today: **no, not yet** — but Postgres is the
better of the two candidates when the day comes.

What is actually in the live database right now:

| Table | Rows |
|---|---|
| Companies | 6 |
| Users | 12 |
| Trips | 22 |
| TripTransactions | 11 |
| TripExpenses | 11 |
| AuditLogs | 41 |
| Documents | 9 |

The whole file is **0.36 MB**. SQLite in WAL mode, which this app already
uses, is comfortable to *hundreds of thousands* of rows and many concurrent
readers. At 22 trips the database is not the constraint on anything, and a
migration spends real effort buying headroom that is years away.

### What would actually justify it

Not size. One of these:

1. **The API needs to run on more than one machine, or somewhere with no
   persistent disk.** This is the real trigger. SQLite is a file, so the API
   and its data must live on the same box — which is why the API is tunnelled
   out of a Windows machine today. Postgres is what lets the API move to a
   host that can be redeployed, scaled, or replaced.
2. **Someone other than the app needs to read the data** — a BI tool, a
   reporting service, an accountant's export job. Concurrent external readers
   against a SQLite file on a Windows share is where this stops being fun.
3. **Backups must be someone else's problem.** Today they are a `File.Copy`
   on this machine — and one with a real flaw, see below.

If none of those is true, the money and risk are better spent elsewhere.

### Why Postgres over SQL Server, if it is going to be one of them

- **No string-length work.** SQL Server needs `HasMaxLength` on a dozen indexed
  string columns before a migration will even generate, because
  `nvarchar(max)` cannot be an index key — blocker B1 in the SQL Server plan,
  and its single largest change. Postgres indexes `text` natively, so **that
  entire blocker disappears.**
- **The filtered index nearly works already.** `HasFilter("\"IsDeleted\" = 0")`
  uses double-quoted identifiers, which is Postgres syntax. Only the literal
  needs changing (`= 0` → `= false`). SQL Server needs the quoting changed too.
- **Free, and hostable anywhere.** Neon, Supabase and Railway all have usable
  free tiers; SQL Express means a Windows box. Given the Cloudflare Pages
  decision was made on cost, this matters.
- **`numeric(12,2)` and `uuid` are native**, so the existing money precision
  convention and every `Guid` key map across without thought.

---

## What does *not* change

- **Every service, controller and query.** They are LINQ over EF Core; the
  provider swap is `UseSqlite` → `UseNpgsql` in one line of `Program.cs:39`.
- **The multi-tenant global query filter**, the audit trail written inside
  `SaveChanges`, the soft-delete filters, the `Counter.LastNumber` concurrency
  token — all provider-agnostic.
- **The frontend.** It never sees the database.
- **The tests.** `TestWorld` builds a real SQLite file per test. It can stay on
  SQLite (fast, no service to run) even after production moves — with the
  caveat in G4 below.

---

## Blockers found in the code

Ordered by how much they cost, worst first.

- [ ] **P1 — 🔴 `DateTime.Now` / `DateTime.Today`, 35 uses. This is the Postgres
      migration.**
      Npgsql maps `DateTime` to `timestamp with time zone` and **throws** on any
      value whose `Kind` is not `Utc`. Sixteen `DateTime.Now` and nineteen
      `DateTime.Today` calls produce `Kind.Local` and `Kind.Unspecified`
      respectively, so a straight provider swap fails at the first write.
      *Evidence:* `DateTime.Now` ×16, `DateTime.Today` ×19, `DateTime.UtcNow`
      ×10 across `Entities.cs`, `AppDbContext.cs`, `AuthService.cs`,
      `DashboardService.cs`, `SettlementService.cs`, `TripService.cs`,
      `TripTransactionService.cs`, `AppLog.cs`, `DbBootstrapper.cs`.

      There are two ways out, and **the choice matters more than the effort**:

      **(a) Map by meaning — recommended.** The model already splits cleanly
      into two kinds, and they want different column types:

      - *Calendar days*, where the time of day is meaningless and the value must
        never shift across a timezone: `Trip.Date` and six other `Date`
        columns, the five vehicle `*Upto` expiries, `LoanStartDate`,
        `LoanEndDate`, `JoiningDate`, `NextDueDate`, schedule `StartDate` /
        `EndDate`, `LicenseStartsOn` / `LicenseExpiresOn`.
        → Postgres **`date`**.
      - *Instants*, where "when did this happen" is the question:
        `CreatedAt`, `UpdatedAt`, `ApprovedOn`, `ClosedOn`, `LastLoginOn`.
        → **`timestamp with time zone`**, written as UTC.

      This is a convention loop in `OnModelCreating`, next to the existing
      decimal one at `AppDbContext.cs:60-67`, plus changing the instant
      properties to `DateTime.UtcNow`.

      **(b) Map everything to `timestamp without time zone`.** One line, no
      code changes, Npgsql stops complaining. It works, and it leaves the
      ambiguity in place forever.

      **Why (a) is worth the extra day:** `Trip.Date` is a calendar day that
      drives every month boundary on the dashboard and every report period. As
      `timestamptz` in IST, midnight on the 1st is 18:30 on the *previous* day
      in UTC — so a trip booked on the 1st can be counted in the previous
      month depending on which end does the comparison. **This is the same
      class of bug as the `toISOString()` one already fixed in the frontend on
      2026-09-06** (see FEATURES-2026-09.md), and it would be considerably
      harder to spot in aggregate report totals than it was in a date picker.

- [ ] **P2 — 🟠 All 17 migrations are SQLite-native and cannot be replayed.**
      They are full of `type: "TEXT"` and `type: "INTEGER"`. Do not point them
      at Postgres. Because this is a one-way move, generate a **fresh squashed
      `InitialCreate`** against Npgsql in a separate migrations assembly or
      folder, and leave the SQLite migrations untouched — they describe the
      source file, which never needs migrating again.
      *Evidence:* `src/TransTrack.Data/Migrations/`, 17 migrations from
      `20260809110620_InitialCreate` to `20260906104418_PlaceActiveFlag`.

- [ ] **P3 — 🟡 The filtered unique index literal.**
      `HasFilter("\"IsDeleted\" = 0")` — the quoting is already right for
      Postgres, but `IsDeleted` is `boolean` there, not an integer, so it must
      become `"IsDeleted" = false`. One line. Silent until the migration runs.
      *Evidence:* `src/TransTrack.Data/AppDbContext.cs:95`.

- [ ] **P4 — 🟡 Two SQLite `PRAGMA` statements.**
      WAL and `synchronous`, meaningless on Postgres. The whole
      `EnableConcurrentAccessAsync` method goes away.
      *Evidence:* `src/TransTrack.Data/DbBootstrapper.cs:85-86`.

- [ ] **P5 — 🟡 `DbBootstrapper` is built around a file path.**
      `DatabasePath`, `TRANSTRUCKWEB_DB`, `AppConfig.DatabasePath`, the
      "is the drive present" check in `deploy/publish-api.ps1`, and the
      pre-upgrade backup all assume a file on disk. These become a connection
      string, and the backup becomes `pg_dump` (see below).
      *Evidence:* `src/TransTrack.Data/DbBootstrapper.cs`, `AppConfig.cs`,
      `deploy/publish-api.ps1`.

### Checked and *not* a problem

- **Case-insensitive lookups already translate.** The duplicate-driver check
  and the username lookups use `.ToLower()`, which EF renders as Postgres
  `lower()`. No `COLLATE` work needed.
  *Evidence:* `DriverService.cs:44-45`, `AuthService.cs:78,136`,
  `RegistrationService.cs:71`.
- **No raw SQL other than the two PRAGMAs.** Nothing else to port.
- **No string length limits needed** — the SQL Server blocker does not apply.
- **`decimal` precision** is set by convention for every money column and maps
  to `numeric(12,2)`. *Evidence:* `AppDbContext.cs:60-67`.

---

## Moving the data

At this volume the data move is the *easy* part — minutes, not hours. It is
also the part where a silent mistake is unrecoverable, so:

1. **Take a complete backup of the source first** — `.db`, **`.db-wal` and
   `.db-shm` together**. See the warning below; this is not optional.
2. **Stand up the empty Postgres schema** from the fresh `InitialCreate`.
3. **Copy the data.** Two options:
   - *Write a one-off copier* that reads with the SQLite EF context and writes
     with the Npgsql one, table by table in FK order. Slower to write, but it
     goes through the same entity model, so date `Kind` conversion happens in
     C# where P1's rules apply.
     **This is the one to use**, precisely because of P1 — the conversion is the
     risk, and a copier makes it explicit rather than implicit.
   - *`pgloader`*, which is a single command but will carry SQLite's TEXT dates
     across verbatim and leave every P1 question unanswered.
4. **Reconcile before switching anything over.** Row counts per table, and —
   more importantly — a handful of money totals computed both sides:
   outstanding balance across all open trips, a party bill's grand total for a
   known month, one trip's `BalanceReceivable`. If the dates were converted
   wrongly, the month-scoped figures are where it shows.
5. Only then repoint the API.

### Pre-flight check worth doing first

Run the existing checks against a *copy* on Postgres before trusting it:
`dotnet test` is 260 tests today, and `TransTrack.UatTests` drives the real
app end to end. Pointing the test suite at a throwaway Postgres instance is the
cheapest possible proof that the provider swap is sound — and it will surface
P1 immediately, because half the fixtures write `DateTime.Today`.

---

## The operational change people forget: backups

Today the API takes its own pre-upgrade backup before applying migrations, and
prunes routine ones. That machinery is SQLite-specific and goes away.

**It also has a live bug worth fixing whether or not this migration happens:**
`BackupBeforeUpgrade` is a `File.Copy` of the `.db` alone and does **not**
include the `-wal` sidecar, so anything sitting in the write-ahead log at that
moment is absent from the backup. The `.db`, `.db-wal` and `.db-shm` together
are the database.
*Evidence:* `src/TransTrack.Data/DbBootstrapper.cs:193`, inside `BackupBeforeUpgrade`.

On Postgres this becomes `pg_dump` on a schedule — or nothing at all to write,
if a managed host is used, which is one of the better arguments for going
managed rather than self-hosting Postgres on the same Windows box.

---

## Gotchas that otherwise cost an afternoon

- **G1 — Npgsql's timestamp behaviour changed in v6.** Most search results
  predate it. The `Kind`-must-be-UTC rule in P1 is current behaviour, not
  legacy advice.
- **G2 — Identifier case.** Npgsql quotes EF's PascalCase names, so tables are
  `"Trips"` not `trips`. Any hand-written SQL — psql sessions, a BI tool —
  must quote them or it will not find them.
- **G3 — `Guid` keys are `uuid`, and default to random.** Fine for this app,
  but random UUID primary keys fragment index inserts at scale. Not a problem
  at 22 trips; worth knowing before it is 22 million.
- **G4 — Keeping tests on SQLite means the tests no longer test the production
  provider.** That is a reasonable trade for speed, but it means P1-class bugs
  (date `Kind`, case sensitivity, collation) can pass CI and fail in
  production. Consider one integration test project pointed at Postgres, even
  if the bulk stay on SQLite.

---

## Phased plan

Each phase ends with something checkable, and the app keeps working throughout.

1. **Decide the date policy (P1).** Half a day of thinking, and the only
   decision here that is expensive to reverse later. Write it down before
   touching code.
2. **Make the code provider-neutral, still on SQLite.** Apply the date
   convention, switch instants to `DateTime.UtcNow`, fix the filter literal,
   lift the PRAGMAs behind a provider check. *Verify:* the full 260-test suite
   still passes on SQLite. Nothing has moved yet, and this is safely
   committable on its own.
3. **Add Npgsql alongside.** Fresh squashed `InitialCreate`, connection string
   config, `UseNpgsql`. *Verify:* the suite passes against a throwaway
   Postgres.
4. **Migrate a copy of production and reconcile.** The copier, then the
   figure-by-figure comparison above. *Verify:* the totals match on both.
5. **Cut over.** Repoint the API, keep the SQLite file untouched and readable
   as the rollback. *Verify:* sign in, book a trip, generate a party bill.
6. **Decommission** the SQLite path only after a week of real use.

---

## Effort

Rough, and stated as ranges because the date policy dominates:

| Phase | Effort |
|---|---|
| 1. Date policy decision | 0.5 day |
| 2. Provider-neutral on SQLite | 1–2 days |
| 3. Npgsql + fresh schema | 0.5–1 day |
| 4. Copier + reconciliation | 1 day |
| 5. Cutover | 0.5 day |
| 6. Decommission | — |
| **Total** | **3.5–5 days** |

Compare with the SQL Server plan's estimate, which carries the string-length
blocker on top of everything here.

---

## Recommendation

**Do not do this now.** Revisit when the API needs to leave this machine —
that is the trigger, not row counts. When that day comes, prefer Postgres over
SQL Express, and do phase 2 first: making the dates unambiguous is worth doing
on its own merits, migration or not, and it is most of the risk.

Meanwhile, two things from this analysis are worth acting on independently:

- The WAL-less backup bug above. It is a real hole in the current safety net.
- The `Trip.Date` timezone ambiguity is latent today because SQLite stores
  dates as text and never converts them. It becomes live the moment the
  provider changes.

---

## Related

- [SQL-SERVER-MIGRATION.md](SQL-SERVER-MIGRATION.md) — the same move to SQL
  Server. Postgres is the easier of the two; that document's blocker B1 has no
  Postgres equivalent.
- [AUDIT.md](AUDIT.md) — open findings.
- [FEATURES-2026-09.md](FEATURES-2026-09.md) — the September batch, including
  the frontend date bug that P1 is the database-side analogue of.
