# Moving from SQLite to PostgreSQL

**Status: plan only. Nothing here is implemented.**
Written 2026-09-09 against `main` @ `b0fac14`, by reading the code rather than
from general advice. Every claim below cites where it came from, so it can be
re-checked when the code has moved on.

Companion to [SQL-SERVER-MIGRATION.md](SQL-SERVER-MIGRATION.md), which plans
the same move to SQL Server. **Read the verdict below before reading either:
the two are alternatives, and today neither is worth doing.**

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
