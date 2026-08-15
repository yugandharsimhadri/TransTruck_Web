# TransTruck deployment guide

Covers the release that adds the way bill number, optional consignor/
consignee, company bank details on the Bill, Owner-only trip cancellation,
the party-wise and vehicle-savings reports, and company self-registration.

There is no CI/CD: both halves are built locally and deployed by hand.

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

Every existing row keeps its data. Existing trips get `WayBillNo = NULL` and
simply don't print a way bill line. Existing companies get bank details off,
so no company starts printing an account number it never entered.

Two safety notes worth knowing:

- **Automatic pre-upgrade backup.** Before applying a migration to an existing
  database, the API copies it to
  `C:\TransTruckWeb\DBBackup\pre-upgrade-<timestamp>.db`. This release's
  upgrade was verified against your real database and produced
  `pre-upgrade-20260815-135047.db`, with all four companies, five trips,
  thirty-two cities and six users intact afterwards.
- **Rollback.** If anything looks wrong, stop the API, restore that
  `pre-upgrade-*.db` over `C:\TransTruckWeb\DB\TransTruckWeb.db`, and run the
  previous API build. The new columns are additive, so the old build ignores
  them — but restoring the backup is the clean route.

---

## 2. API (backend)

Deployable: `C:\TransTruckWeb\publish` (already rebuilt for this release).

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
Tunnel exposes as `ttapi.sivayaantechnologies.com`. On startup it backs up,
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
   `transtruck.sivayaantechnologies.com`.

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
