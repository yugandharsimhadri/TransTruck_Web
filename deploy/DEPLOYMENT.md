# LorryOwner deployment guide

Covers the releases that add the way bill number, optional consignor/
consignee, company bank details on the Bill, Owner-only trip cancellation,
the party-wise and vehicle-savings reports, company self-registration,
vehicle document upload, and the LorryOwner rebrand.

There is no CI/CD: both halves are built locally and deployed by hand.

> **Names in paths and code still say TransTrack / TransTruck.** That is
> deliberate. The rebrand changed what users see — the app name, logo, icons
> and the `lorryowner.com` / `loapi.lorryowner.com` domains — and left internal
> identifiers alone: the `src/TransTrack.*` projects, the
> `C:\TransTruckWeb` data folder, the `TRANSTRUCKWEB_*` environment
> variables, and the JWT issuer/audience. Renaming those would mean moving
> the live database and secrets by hand, and changing the JWT issuer would
> sign every user out, all for no user-facing gain.

---

## 1. Database — nothing to run by hand

**You do not run any SQL or migration command.** The API applies pending
migrations itself on startup (`DbBootstrapper.InitialiseAsync` →
`Database.MigrateAsync()`), and it takes a copy of the database *before*
touching the schema.

What this release adds — all additive, nothing rewritten or dropped:

| Table | Column | Type | Default |
|---|---|---|---|
| Trips | `WayBillNo` | TEXT | NULL |
| Companies | `BankAccountNo` | TEXT | NULL |
| Companies | `Ifsc` | TEXT | NULL |
| Companies | `ShowBankDetailsOnBill` | INTEGER | 0 (off) |
| Drivers | `JoiningDate` | TEXT | now nullable (values kept) |
| **VehicleDocuments** (new table) | one row per vehicle, holding a *reference* to the uploaded file | — | — |

Every existing row keeps its data. Existing trips get `WayBillNo = NULL` and
simply don't print a way bill line. Existing companies get bank details off,
so no company starts printing an account number it never entered. Drivers
keep whatever joining date they had — the column only stopped being
required, so new drivers can be added without one.

**`VehicleDocuments` stores the reference, not the file.** The uploaded
bytes live on disk under `C:\TransTruckWeb\VehicleDocs` (configurable via
`VehicleDocumentDirectory` in `appsettings.Production.json`). That folder
must be backed up alongside the database — restoring the database on its own
leaves rows pointing at files that aren't there. The app degrades quietly if
that happens (the vehicle reads as having no document, and can be
re-uploaded) rather than throwing, but the file is still lost.

Two safety notes worth knowing:

- **Automatic pre-upgrade backup.** Before applying a migration to an existing
  database, the API copies it to
  `C:\TransTruckWeb\DBBackup\pre-upgrade-<timestamp>.db`. Each of these
  migrations was applied to the real database during development and the
  existing companies, trips, cities and users were checked intact
  afterwards — but check the row counts yourself after upgrading, rather
  than trusting a number written here, since the data keeps growing.
- **Rollback.** If anything looks wrong, stop the API, restore that
  `pre-upgrade-*.db` over `C:\TransTruckWeb\DB\TransTruckWeb.db`, and run the
  previous API build. The new columns are additive, so the old build ignores
  them — but restoring the backup is the clean route.

---

## 1a. Moving the installation to another drive

Everything the app writes hangs off one root, `C:\TransTruckWeb` by default.
Point `TRANSTRUCKWEB_ROOT` at a different folder and the deploy scripts *and*
the API both follow it, so the executable and the data stay together:

| What | Where | Set by |
| --- | --- | --- |
| Executable | `<root>\publish` | `-Root` / `TRANSTRUCKWEB_ROOT` |
| JWT signing key | `<root>\secrets\jwt.key` | `-Root` / `TRANSTRUCKWEB_ROOT` |
| Database | `<root>\DB\TransTruckWeb.db` | `DataRoot`, or `DatabasePath` |
| Backups | `<root>\DBBackup` | `DataRoot`, or `BackupDirectory` |
| Documents | `<root>\VehicleDocs` | `DataRoot`, or `VehicleDocumentDirectory` |
| Logs | `C:\ProgramData\TransTrack\logs` | `LogDirectory` only — see below |

Logs deliberately do **not** move with the root. The log is what you read when
the data drive is the thing that failed, so it stays on the system drive
unless you set `LogDirectory` explicitly.

To move to `E:\LorryOwner`:

```powershell
# 1. Stop the API.

# 2. Copy the existing data across. Nothing moves it for you, and the
#    database and its documents must travel together — a database restored
#    without its documents leaves rows pointing at files that aren't there.
robocopy C:\TransTruckWeb E:\LorryOwner /E /COPYALL

# 3. Point everything at the new root, for this session and for good.
$env:TRANSTRUCKWEB_ROOT = "E:\LorryOwner"
[Environment]::SetEnvironmentVariable("TRANSTRUCKWEB_ROOT", "E:\LorryOwner", "Machine")

# 4. Republish and run against it.
.\deploy\publish-api.ps1 -Root E:\LorryOwner
.\deploy\run-api.ps1     -Root E:\LorryOwner
```

The startup line prints the root in use — check it says `E:\LorryOwner`
before signing in. Once you are satisfied, the old `C:\TransTruckWeb` can be
archived and removed.

Alternatively, set `"DataRoot": "E:\\LorryOwner"` in
`appsettings.Production.json` (note the doubled backslashes) — that moves the
database, backups and documents but *not* the executable or signing key, so
you would still pass `-Root` to the scripts. Setting one individual path
overrides the root for that folder only, which is how you put a large
documents folder on a second disk while the database stays on a fast one.

If the drive isn't mounted, both scripts stop with a plain message naming it
rather than failing partway through.

---

## 2. API (backend)

Deployable: `<root>\publish` — `C:\TransTruckWeb\publish` unless you moved it
(see 1a above). Already rebuilt for this release.

```powershell
# From the repo root, if you need to rebuild it:
.\deploy\publish-api.ps1
```

Deploy:

1. Stop the running API (close its window, or stop the service/task running
   `TransTrack.Api.exe`).
2. Copy `C:\TransTruckWeb\publish` to the server if it isn't already there.
   Leave `C:\TransTruckWeb\DB` and `C:\TransTruckWeb\secrets` alone — the
   database and the JWT signing key live outside the publish folder precisely
   so a redeploy never overwrites them.
3. Start it:

```powershell
.\deploy\run-api.ps1
```

That runs it in Production on `http://localhost:6041`, which the Cloudflare
Tunnel exposes as `loapi.lorryowner.com`. On startup it backs up,
migrates, then serves.

**Do not delete `C:\TransTruckWeb\secrets\jwt.key`.** Every signed-in session
is signed with it; replacing it logs everyone out.

Check it came up:

```bash
curl -o /dev/null -w "%{http_code}\n" http://localhost:6041/api/auth/me
```

`401` is correct — it means auth is running and rejecting an unauthenticated
call.

---

## 3. Frontend (Cloudflare Pages)

Deployable: `transtruck-web-pages-deploy.zip` (rebuilt for this release).

Its contents are laid out for Pages "Advanced Mode": `_worker.js` at the zip
root, next to the static files it serves.

1. Cloudflare dashboard → your Pages project → **Create deployment** (or
   drag the zip onto the existing project).
2. Upload the zip's **contents** — `_worker.js` and `_next/` must be at the
   top level, not nested inside a folder.
3. First deployment only: **Settings → Functions** → compatibility date
   `2026-08-10` or later, flags `nodejs_compat` and
   `global_fetch_strictly_public` (production *and* preview), then redeploy so
   the flags take effect.
4. First deployment only: **Settings → Custom domains** → add
   `lorryowner.com`.

To rebuild after future changes:

```bash
cd web/transtrack-web
npx opennextjs-cloudflare build
npx wrangler deploy --dry-run --outdir=.deploy-dryrun
```

Then zip `.open-next/assets/*` plus `.deploy-dryrun/worker.js` renamed to
`_worker.js`, all at the zip root.

> On Windows, stop the `next dev` server before building — it holds a
> `workerd.exe` child process that locks `.open-next` and makes the build fail
> with `EPERM`. Killing `workerd` alone doesn't help; it respawns.

---

## 4. Order of deployment

Deploy the **API first**, then the frontend.

The new frontend sends `wayBillNo` and the bank fields; an old API ignores
unknown JSON properties rather than failing, so a brief mismatch is survivable
either way. But the party-wise and vehicle-savings report tabs call endpoints
that only exist in the new API, so a new frontend against an old API would
show those two tabs failing to load. API first avoids that window entirely.

---

## 5. Post-deployment check

1. Sign in as an existing owner — existing users, passwords and data are
   untouched by this release.
2. Open an existing trip: it still loads, with the new **Way bill no.** field
   empty and consignor/consignee marked optional.
3. Settings → confirm the new **Print bank details on the Bill** toggle is
   **off** for existing companies.
4. Reports → the two new tabs (**Party-wise**, **Vehicle savings**) load.
5. Print an LR for any trip — it now carries the company logo. A trip with no
   way bill number simply doesn't show that line.

If a company wants bank details on its Bill, they turn the toggle on and fill
in the two fields in Settings; until then their Bill is unchanged.
