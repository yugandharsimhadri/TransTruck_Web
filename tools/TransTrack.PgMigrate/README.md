# TransTrack.PgMigrate

One-shot copy of every row from the SQLite database this app has been
running on into a fresh PostgreSQL database. Built for the
`postgres-migration` branch — not part of the running application, and
not meant to be run more than once against a given Postgres database.

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

## Usage

```bash
export TRANSTRUCKWEB_PG_CONNECTION='Host=localhost;Database=transtruckweb;Username=transtrack_app;Password=...'

dotnet run --project tools/TransTrack.PgMigrate -- \
  --sqlite "C:/TransTruckWeb/DB/TransTruckWeb.db" \
  --yes
```

Both `--sqlite` and `--pg` are optional — they default to
`TRANSTRUCKWEB_DB` / `TRANSTRUCKWEB_PG_CONNECTION`, the same environment
variables the application itself reads. Drop `--yes` to get an
interactive `[y/N]` confirmation instead.

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
