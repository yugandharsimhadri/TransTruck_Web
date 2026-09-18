# TransTrack.PgMigrate

One-shot copy of every row from the SQLite database this app used to run on
into a fresh PostgreSQL database. This is the tool that moved production on
2026-09-18. It is not part of the running application, and it is not meant
to be run more than once against a given Postgres database.

Now that production is on Postgres, it is only needed for standing up a
new environment from a SQLite file, or reloading one — the procedure around
it is in [../../POSTGRES-MIGRATION.md](../../POSTGRES-MIGRATION.md).

It is published standalone (no .NET SDK or source needed where it runs):

```powershell
dotnet publish tools\TransTrack.PgMigrate\TransTrack.PgMigrate.csproj -c Release -o C:\TransTruckWeb-Postgres\pgmigrate
```

## What it does

1. Snapshots the SQLite file (the `.db`, `-wal` and `-shm` files together,
   so nothing written since the last checkpoint is missed) into a temp
   folder and reads only from that copy — it never opens the live file,
   so it's safe to run while the app is up.
2. Self-heals two known schema gaps in an older SQLite file
   (`BulkSettlement`, `PlaceActiveFlag`) if the source predates them.
3. Copies every table into Postgres in FK-safe order, re-stamping every
   instant `DateTime` column to `Kind=Utc` on the way through (SQLite
   never preserves `DateTime.Kind`, and Npgsql refuses anything else for
   `timestamp with time zone`).
4. Runs the whole copy in one transaction — it either all lands, or none
   of it does.
5. Refuses to run if the target already has any rows, across every
   table it's about to write to (not just `Companies` — `CompanyId` is
   not an enforced foreign key, so a `Companies`-only truncate leaves
   `States`/`Cities`/etc. behind and the next run collides on them).
6. Prints a `=== Migration summary ===` block at the end either way —
   source, target, duration, every schema fix applied, a per-table row
   count, and a running total. On failure the same block names the exact
   table it stopped on and the database error, instead of a raw .NET
   stack trace.

## Reading the summary

A clean run ends with:

```
=== Migration summary ===
Source:   C:/TransTruckWeb/DB/TransTruckWeb.db
Target:   Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=***
Duration: 2.4s

Schema fixes applied (3):
  - Source predates BulkSettlement (2026-09-06): added Settlements table and TripTransactions.SettlementId.
  - Source predates PlaceActiveFlag (2026-09-06): added States.IsActive.
  - Source predates PlaceActiveFlag (2026-09-06): added Cities.IsActive.

Tables copied (21 of 21 attempted):
  Company                       6 rows
  ...
  TOTAL                       291

Result: SUCCESS — every table copied, transaction committed.
```

"Schema fixes applied" is the honest list of gaps found between the source
file and the current model, not a static description — it's empty when
the source is already current. A failure ends the same block with
`Result: FAILED`, which table it stopped on, and the database's own error
message; because everything runs in one transaction, every table listed
above the failed one was still rolled back — the summary's row counts
describe what was *attempted*, not what's left in the target.

## Usage

On the server, from the published folder:

```powershell
$env:TRANSTRUCKWEB_PG_CONNECTION = "Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=..."
.\TransTrack.PgMigrate.exe --sqlite "C:\TransTruckWeb\DB\TransTruckWeb.db" --yes
```

Or from source on a dev machine:

```powershell
dotnet run --project tools\TransTrack.PgMigrate -- --sqlite "<path to the .db>" --yes
```

Both `--sqlite` and `--pg` are optional — they default to
`TRANSTRUCKWEB_DB` / `TRANSTRUCKWEB_PG_CONNECTION`. Drop `--yes` for an
interactive `[y/N]` confirmation.

**Point `--sqlite` at the real file.** The target Postgres must be reachable
from wherever this runs, and the SQLite path must be the actual database
the API was using — on production that is the file on the production
server. A dev machine may well have a `C:\TransTruckWeb\DB\TransTruckWeb.db`
of its own that is an old local copy, not production; the one on this
project's dev machine had 22 trips when production had 132.

The target's schema must already exist: start the API once against the
empty database first (it creates every table on startup), then run this.
It only copies rows *into* tables — it does not create them.

## Resetting the target for a re-run

Truncating `Companies` alone is not enough:

```sql
TRUNCATE TABLE
  "AuditLogs","Cities","Companies","Counters","Documents","DriverLedgerEntries",
  "Drivers","ExpenseCategories","MaintenanceCategories","Owners","Parties",
  "Settlements","States","TripExpenses","TripTransactions","Trips","Users",
  "VehicleExpenseSchedules","VehicleExpenses","VehicleMaintenances","Vehicles"
CASCADE;
```
