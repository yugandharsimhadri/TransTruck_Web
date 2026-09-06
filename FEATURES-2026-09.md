# Feature batch — September 2026

**Spec given:** 2026-09-05, 15 items.
**Status:** in progress. Tick items as they land.

## Decisions taken (asked and answered before starting)

1. **`Trip.Amount` stays freight only** (weight × rate). Extras are separate
   fields; `TotalBeforeTax`, `GstAmount` and `GrandTotal` are computed on top.
   **`BalanceReceivable` becomes `GrandTotal − approved received`** — the party
   owes the whole invoice, not just the freight. This is a real change to
   existing balance maths and is the reason the extras aren't folded into
   `Amount`: freight has to stay recoverable.
2. **GST rate is snapshotted onto the trip at booking.** Changing a party's rate
   later never rewrites a bill that has already been issued.
3. **Recurring vehicle expenses generate real rows** at a chosen frequency
   (monthly / quarterly / yearly) from start date to end date.

---

## Phase 1 — Data model + one migration

- [x] **P1.1 — Vehicle loan fields** (item 5): `LoanAmount`, `LoanStartDate`,
      `LoanEndDate`, `EmiAmount`, `EmiDayOfMonth`. All nullable — most vehicles
      won't have a loan.
- [x] **P1.2 — Party GST** (item 9): `IsGstEnabled`, `GstPercentage`.
      `Gstin` already exists.
- [x] **P1.3 — Trip extras + GST snapshot** (items 10–12): `WaymentCharge`,
      `LoadingCharge`, `UnloadingCharge`, `GstPercentage` (snapshot, nullable).
- [x] **P1.4 — Trip computed money** — `TotalExtras`, `TotalBeforeTax`,
      `GstAmount`, `GrandTotal`; `BalanceReceivable` re-based on `GrandTotal`.
      Every one `Ignore()`d in the model like the existing computed properties.
- [x] **P1.5 — New `VehicleExpense` entity** (item 6): vehicle, kind
      (Insurance / RoadTax / Other), amount, date, recurrence (None / Monthly /
      Quarterly / Yearly), start + end date, and a link back to the schedule row
      that generated it so a schedule can be revised without orphaning history.
- [x] **P1.6 — One migration** covering all of the above, additive only.

## Phase 2 — Services

- [x] **P2.1 — Driver duplicates** (item 7): refuse a duplicate name or phone
      within one company. Deactivation already exists (`Driver.IsActive`) —
      confirm it's exposed in the UI.
- [x] **P2.2 — `VehicleExpenseService`** — CRUD plus generating the rows for a
      recurring schedule, and regenerating safely when one is edited.
- [x] **P2.3 — Dashboard rework** (items 1–3):
      - month selector, minimum last 6 months (item 2)
      - six figures for the selected month (item 3): trip earnings, trip
        expenses, maintenance, expected salaries, recurring/paperwork expenses,
        and net profit = 1 − 2 − 3 − 4 − 5
      - **"Still to collect" must NOT become month-filtered** (item 1) — it
        stays from inception across every trip that isn't closed.
      - Expected salaries is a pure calculation off active drivers' monthly
        salary. Never read from the driver ledger, never "paid vs unpaid".

## Phase 3 — Documents

- [x] **P3.1 — Header title centring** (item 14): `PdfHelpers.CompanyHeader`
      puts the logo in a 140pt left column, so the "centred" title is only
      centred in what's left. Add a matching empty 140pt spacer on the right so
      the centre is true; long titles then wrap immediately after the logo
      rather than overlapping it.
- [x] **P3.2 — Bill shows extras + GST** (items 12, 13): freight, each extra
      named separately, total before tax, GST at the snapshotted rate, grand
      total.
- [x] **P3.3 — Party Bills document** (item 15): same letterhead and title
      treatment, one row per trip over a date range, extras and GST applied the
      same way, grand total at the foot.

## Phase 4 — Frontend

- [x] **P4.1 — Navigation split** (item 4): "Vehicles" as its own item;
      "Drivers & Parties" as the other. *Open: Places/Cities currently lives in
      the same screen — defaulting it to sit with Drivers & Parties unless told
      otherwise.*
- [x] **P4.2 — Vehicle screen**: loan fields, plus a vehicle-expenses section
      (item 6) with the recurrence controls.
- [x] **P4.3 — Party screen**: GST enabled + percentage.
- [x] **P4.4 — Trip booking**: wayment / loading / unloading, with the running
      total showing freight → extras → GST → grand total as it's typed.
- [x] **P4.5 — Dashboard**: month picker and the six figures.
- [x] **P4.6 — Party Bills screen** (item 15): party + from/to date → bill.
- [x] **P4.7 — Reports** (items 8, 13): month-wise reporting, and extras shown
      as their own columns rather than folded into a total.

---

## Verification standard for this batch

The money maths is the risk. Every phase ends with:
- unit tests for the new calculations, including the invariant that
  `TotalBeforeTax + GstAmount == GrandTotal` and
  `GrandTotal − received == BalanceReceivable`
- a live check against a **copy of the production database**, taken *with its
  `-wal` file* (see the note in the audit — copying only the `.db` silently
  misses recent writes)
- the production database is never used for testing

## Related

- [AUDIT.md](AUDIT.md) — open findings; F7 (dashboard round-trips) is worth
  revisiting while the dashboard is being reworked in P2.3.
- [SQL-SERVER-MIGRATION.md](SQL-SERVER-MIGRATION.md) — do not overlap this batch
  with that migration; in-flight feature migrations plus a provider move is
  exactly where data gets lost.

---

## Found while verifying

Three defects the spec didn't ask about but the work exposed. All fixed, each
with a test that fails without the fix.

- **Vehicle loan fields never saved.** `VehicleService.SaveVehicleAsync` copies
  field by field onto the tracked row, and the five new loan columns were not
  in that list — so the API returned 200 and silently discarded them. Caught
  only by reading the values back; the save itself reports success either way.
  Covered now by `VehicleFinanceTests`.
- **The trips list disagreed with the trip it opened.** `TripListItem` computed
  `BalanceReceivable` as `Amount − received`, the pre-batch formula, while the
  trip itself had been re-based on `GrandTotal` (decision 1) — and the list
  showed freight beside that grand-total balance. Both list sorts ordered on
  freight too. The row now carries the extras and the rate, and the rounding
  rule moved to `TripMath` so entity and row cannot drift again.
- **Every date default was a day early for part of the day.**
  `toISOString().slice(0, 10)` converts to UTC first, so midnight on the 1st in
  IST is the 31st in ISO — and between midnight and 05:30 IST "today" resolved
  to yesterday. On Party Bills that quietly pulled in the previous month; on
  the entry forms it back-dated whatever was being recorded. `toDateInput` and
  `today` in `lib/format.ts` read the local date instead, and every screen that
  defaults a date now uses them: Party Bills, vehicle expense schedules, driver
  ledger, maintenance, trip detail, add-amount and add-expense.

### Left alone, deliberately

- `dashboard/page.tsx` calls `Date.now()` during render, which the React
  compiler flags as impure. Pre-existing (it is in `HEAD`), and fixing it
  properly means moving the clock out of render rather than a one-liner.
