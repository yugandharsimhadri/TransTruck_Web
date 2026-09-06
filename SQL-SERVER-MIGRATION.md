# Moving from SQLite to SQL Server / SQL Express

**Written:** 2026-08-31
**Status:** plan only — nothing implemented.
**Scope:** the API's database provider and the one-time move of existing data.

This is a working checklist, not a one-off write-up. Tick items as they land and
add new ones the same way.

---

## Is this worth doing?

The database is **372 KB across 18 tables**. SQLite handles this app comfortably
for years, so this move is not about capacity. The reasons that do justify it:

- more than one machine needs to reach the same database
- genuinely concurrent writers, rather than one API process
- proper backup/restore tooling and point-in-time recovery
- a stepping stone to a hosted/cloud database later

If none of those are pressing, this is optional work and deferring it costs
nothing. `IDocumentStorage` already models the "swap one seam" pattern for file
storage; the database engine is the seam that doesn't have that treatment yet.

## What does *not* change

Every service, controller, query, the global tenant filter, the audit trail, and
all the LINQ. EF Core abstracts it. There is **no raw SQL anywhere in the app**
except two SQLite `PRAGMA` statements (see blocker 4).

---

## Blockers found in the code

- [ ] **B1 — 🔴 No string length limits anywhere. This hard-fails on SQL Server.**
  `HasMaxLength` / `StringLength` / `HasColumnType` appear nowhere in the model.
  Every `string` therefore maps to `nvarchar(max)` on SQL Server, and
  **`nvarchar(max)` cannot be an index key column**. There are 12 indexes on
  string columns: `State.Name`, `City.Name`, `Owner.Name`, `Party.Name`,
  `ExpenseCategory.Name`, `MaintenanceCategory.Name`, `User.Username` (unique),
  `Trip.TripNo` (unique, composite), `Vehicle.RegNo` (unique, composite,
  filtered), `Driver.EmployeeCode` (unique, composite), `Counter.Name` (unique,
  composite), and `AuditLog.EntityType`.
  Migration *generation* fails until each gets a bounded length. Mechanical, but
  it is the largest single change — about a dozen lines in `OnModelCreating`.
  *Evidence:* `src/TransTrack.Data/AppDbContext.cs` lines 68–199.

- [ ] **B2 — 🟠 The filtered unique index uses SQLite identifier quoting.**
  `HasFilter("\"IsDeleted\" = 0")` needs to become `[IsDeleted] = 0`. One line,
  but silent — it only fails when the migration actually runs.
  *Evidence:* `src/TransTrack.Data/AppDbContext.cs:92`.

- [ ] **B3 — 🟠 All 14 existing migrations are SQLite-native and cannot be replayed.**
  They carry 274 `type: "TEXT"` and 45 `type: "INTEGER"` column declarations.
  Do **not** attempt to run these against SQL Server. Because this is a one-way
  move, generate a **fresh squashed `InitialCreate`** for SQL Server instead.
  Leave the SQLite migrations untouched — they describe the source file, which
  never needs migrating again.

- [ ] **B4 — 🟡 Two SQLite `PRAGMA` statements.**
  WAL and `synchronous` — meaningless on SQL Server. The whole
  `EnableConcurrentAccessAsync` method goes away.
  *Evidence:* `src/TransTrack.Data/DbBootstrapper.cs:85-86`.

---

## Schema strategy

- [ ] Add `Microsoft.EntityFrameworkCore.SqlServer`; drop the SQLite package
      (or keep both briefly through cutover).
- [ ] Apply B1 and B2.
- [ ] On a branch, remove the SQLite `Migrations/` folder and generate one fresh
      `InitialCreate` against the SQL Server provider. Read the generated SQL
      before trusting it.
- [ ] Swap `UseSqlite` → `UseSqlServer` in the three places it appears:
      `src/TransTrack.Api/Program.cs:39`,
      `src/TransTrack.Data/DesignTimeDbContextFactory.cs:23`,
      `tests/TransTrack.Tests/TestWorld.cs:51`.
- [ ] Consider `EnableRetryOnFailure()` — a network database can fail
      transiently in ways a local file never did.

**Open decision — the test suite.** All 152 tests currently spin up a throwaway
`.db` file per test. Against SQL Server they need a real instance (LocalDB or a
container). Either keep tests on SQLite (fast, but they stop exercising the real
provider — and B1 proves provider differences genuinely bite here), or move them
to LocalDB (slower, truer). Leaning LocalDB for correctness.

---

## Moving the data

The database is tiny, so a **one-shot EF console app** is the right tool: it
reuses the existing model, preserves Guids exactly, and avoids hand-rolled type
conversion between SQLite's loose typing and SQL Server's strict typing.

A naive copy through `AppDbContext` corrupts the result three ways, all specific
to this codebase:

| Landmine | What actually happens | Where |
| --- | --- | --- |
| Global tenant filter | Reads return **zero rows** — `CurrentCompanyId` is `Guid.Empty` in a console app. Every read needs `IgnoreQueryFilters()`. | `AppDbContext.cs:212` |
| Audit trail in `SaveChanges` | Every copied row writes a **spurious audit entry** — thousands of invented "created" events. | `AddAuditEntries()` |
| `Stamp()` on Added entities | **Overwrites `CreatedAt` / `CreatedByUserId`** on every row — original timestamps and authorship are lost. | `Stamp()` |

- [ ] Write the target side through a **bare `DbContext`** without those
      overrides, or add an explicit migration-mode flag that short-circuits both.
- [ ] Insert in FK-safe order: Companies → States → Cities →
      Owners / Parties / ExpenseCategories / MaintenanceCategories → Vehicles →
      Drivers → Users → Counters → Trips → TripExpenses / TripTransactions →
      VehicleMaintenances → DriverLedgerEntries → StoredDocuments → AuditLogs.

### Pre-flight data check (do this first)

SQLite's default text comparison is **case-sensitive**; SQL Server's default
collation (`SQL_Latin1_General_CP1_CI_AS`) is **case-insensitive**. Any values
differing only by case are distinct today and will **collide on a unique index**
during the pump.

- [ ] Check for case-only duplicates in: `User.Username`, `Vehicle.RegNo`,
      `Trip.TripNo`, `Driver.EmployeeCode`, `Counter.Name`. Five-minute query;
      otherwise it surfaces as a confusing mid-migration failure.

Note this collation change is arguably a *fix* — `AuthService`'s own comment says
"Owner and owner are the same account" — but it has to be a deliberate decision,
not a surprise.

---

## The operational change people forget: backups

The entire current backup story is **file copies** — daily, pre-upgrade, and
pruning (`DbBootstrapper.cs:152-218`). **All of it stops working.** A live `.mdf`
cannot be copied.

Worth knowing: `BackupNow` and `LastBackup` are currently **dead code** — nothing
calls them. Only the automatic daily and pre-upgrade copies inside
`InitialiseAsync` actually run, and those are what is lost.

**SQL Express has no SQL Server Agent**, so there is no built-in scheduler.

- [ ] Replace with `BACKUP DATABASE` via `sqlcmd`, driven by Windows Task
      Scheduler (or Ola Hallengren's maintenance scripts).
- [ ] **Prove a restore** into a scratch database. An unrestored backup is a
      hope, not a backup.
- [ ] Update `deploy/DEPLOYMENT.md` and the `DataRoot` documentation:
      `DatabasePath` stops being a filesystem path and becomes a connection
      string. `BackupDirectory` and `VehicleDocumentDirectory` stay as they are.

**SQL Express limits:** 10 GB per database, 1 GB buffer pool, 4 cores. At 372 KB
there is enormous headroom.

---

## Two gotchas that otherwise cost an afternoon

- **`TrustServerCertificate=True`** — `Microsoft.Data.SqlClient` now defaults to
  `Encrypt=true`. Without this (or a real certificate) a default Express install
  fails to connect with an opaque error.
- **Guid clustered primary keys.** EF makes the Guid PK clustered by default, and
  the code calls `Guid.NewGuid()` explicitly in several places. Random Guids
  scatter inserts across the clustered index and cause page splits — a real SQL
  Server problem that does not exist in SQLite. Negligible at this scale; if
  volume grows, switch to `Guid.CreateVersion7()` (.NET 9+) or make the primary
  key non-clustered.

---

## Phased cutover

1. - [ ] **Schema work on a branch** — B1, B2, provider swap, fresh migration.
     *Verify:* the migration generates and applies cleanly to an empty local
     SQL Express.
2. - [ ] **Transfer tool**, with all three landmines handled.
     *Verify:* run against a **copy**; compare per-table row counts, money
     totals, audit-row count, and `CreatedAt` values against the source.
3. - [ ] **Backup replacement**, proven by an actual restore. Before cutover.
4. - [ ] **Dry-run cutover** — full pump into a scratch SQL Express, point a
     local API at it, exercise the real flows: book a trip, add an advance,
     approve it, print the LR, export both reports.
5. - [ ] **Real cutover** — stop the API, final pump, switch the connection
     string, start, verify.

**Keep the SQLite file.** It is the rollback, and reverting is just switching the
connection string back. That stays true right up until someone writes new data
into SQL Server — which is the point of no return, so schedule the cutover for a
quiet window.

---

## Effort

| Phase | Estimate |
| --- | --- |
| Schema work (B1–B4, fresh migration) | ~1 day |
| Transfer tool + verification | ~1 day |
| Backup scripting + proven restore | ~0.5 day |
| Test suite → LocalDB (if chosen) | ~1 day |

**Roughly 3–4 days** with real verification, not counting a monitoring period
afterwards.

**Sequencing caution:** do this on a branch and land it during a feature lull. A
half-migrated schema plus in-flight feature migrations is exactly where data gets
lost.

---

## Related

- [AUDIT.md](AUDIT.md) — the system audit; its "SQLite growth plan" item under
  Phase 4 is what this document answers.
- `deploy/DEPLOYMENT.md` — needs updating at step 3.
