# LorryOwner — System Audit & Remediation Plan

**Audit date:** 2026-08-28
**Scope:** `TransTrack.Core`, `TransTrack.Data`, `TransTrack.Api`, `web/transtrack-web` — full read-only review (architecture, frontend, backend, database, API integration, performance, security, code quality). No code was changed during the audit itself; this document is the write-up plus the plan for acting on it.

This file is meant to be a living checklist — tick items off as they land, and add new ones the same way rather than starting a fresh document each time.

## How to read this

Each finding has a checkbox, a severity, and enough evidence to re-find it without re-deriving it. Severities:

- 🟠 **High** — real exposure, cheap for an attacker to exploit, cheap for us to close
- 🟡 **Medium** — real gap, not urgent, worth doing before it matters
- 🟢 **Low** — correctness/cleanliness, no user-facing risk

Nothing rated 🔴 Critical was found — no SQL injection surface, no XSS vector, no committed secret, no confirmed cross-tenant data leak.

---

## Findings

### 🟠 High

- [x] **F1 — No rate limiting on authentication endpoints.** *(fixed)*
  Added ASP.NET Core's built-in rate limiter: a fixed-window policy named `"auth"`, 10 requests/minute per client IP, no queueing (an over-limit request is rejected immediately with `429 {"message": "Too many attempts. Wait a minute and try again."}`, not delayed). Applied via `[EnableRateLimiting("auth")]` to `Login`, `Register`, and `ChangePassword` — they share one budget per IP, so an attacker can't reset the clock by switching endpoints. Keyed off `HttpContext.Connection.RemoteIpAddress`, which is reliable here because `UseForwardedHeaders()` already rewrites it from `X-Forwarded-For`, and nothing reaches this process except through the Cloudflare Tunnel.
  *Verified live* (scratch database, not production): 10 consecutive bad logins from one IP returned `401`; the 11th through 15th returned `429`. A concurrent request to a non-auth endpoint (`/api/documents/limits`) was unaffected. `Register` was throttled by the same budget the login attempts had already spent, confirming the shared-bucket design.
  *Follow-up, not blocking:* no automated regression test yet — would need a `WebApplicationFactory<Program>` test host, which doesn't exist in this repo today. Worth adding if this project ever wants HTTP-level integration tests generally, not just for this one policy.
  *Evidence:* `src/TransTrack.Api/Program.cs` (rate limiter registration + `UseRateLimiter()`), `src/TransTrack.Api/Controllers/AuthController.cs`.

- [x] **F2 — Stale authorization claims; no per-request revalidation of user state.** *(fixed)*
  Extended the existing per-request middleware in `Program.cs` (previously company-license-only) to also look up the calling user by the `sub` claim and confirm they're still active and still hold the role their token claims — same shape as the company check already there, one more indexed point-lookup. A mismatch returns `401 {"message": "Your account no longer has access. Sign in again."}`.
  *Decision (asked, answered):* `Jwt:FullSessionHours` stays at 12h — the per-request check closes the actual gap, so shortening the session on top of it would just mean more re-logins for no added safety.
  *Verified live* (scratch database): registered a company, signed in as Owner, created and signed in as an Accountant — a plain authenticated call worked normally (regression check). Owner then deactivated the Accountant from a separate session; the Accountant's still-cached, still-unexpired token was rejected with 401 on its very next request. Reactivated, signed in fresh, confirmed that token worked — then the Owner changed the Accountant's role to CoOwner; the Accountant's **old** token (still claiming `role=Accountant`) was rejected the same way, proving a promotion invalidates a stale token exactly like a demotion does. The Owner's own session was unaffected throughout.
  *Evidence:* `src/TransTrack.Api/Program.cs` (the per-request middleware, now checking both `Companies` and `Users`).

### 🟡 Medium

- [x] **F3 — No application-level security headers.** *(fixed)*
  Added `headers()` in `next.config.ts`: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: strict-origin-when-cross-origin`, a `Permissions-Policy` denying camera/mic/geolocation (nothing in the app uses them today), and a CSP.
  Deliberately set in `next.config.ts` rather than a Cloudflare Pages `_headers` file — this app's pages are rendered by the OpenNext worker, not served as static files, and Cloudflare's `_headers` mechanism only rewrites headers on its own static-asset responses, so it would have silently missed every page load.
  *Known, documented tradeoff:* `script-src` needs `'unsafe-inline'` because `next-themes` injects a small inline script before hydration to avoid a flash of the wrong theme, and there's no nonce wired through this app's rendering path. Not a full mitigation, but the app has zero `dangerouslySetInnerHTML` (confirmed in the original audit sweep), so there's no built-in HTML-injection path for a stored value to become a script tag in the first place — this CSP's main value is blocking a *foreign* script from ever being loaded, which it does.
  `'unsafe-eval'` is added only outside production (`NODE_ENV !== "production"`), for React's own dev-mode debugging tools — React's build explicitly logs that it never uses `eval()` in production, so this doesn't weaken what a real attacker could reach.
  *Verified live*: full register → login → force-password-change → dashboard flow completed with zero console errors in a clean browser tab; response headers confirmed present via `fetch()` from the page itself; theme toggle (the one thing relying on `'unsafe-inline'`) worked correctly.
  *Evidence:* `web/transtrack-web/next.config.ts`.

- [x] **F4 — Self-registration leaks account existence.** *(fixed, partially — see note)*
  Merged the two distinct collision messages ("This company is already registered" for a name+phone match, "That phone number is already registered" for a phone-only match) into one identical message for both. Before this, an anonymous caller could tell "this phone has an account under a *different* company name" apart from "this exact company+phone pair is registered" just by reading which message came back — effectively a one-field-at-a-time way to probe arbitrary phone numbers.
  *Honest limitation, not silently glossed over:* this closes that specific oracle, not the weaker one underneath it — a caller can still tell "registration failed" from "registration succeeded," and that alone is a fainter version of the same signal. Fully closing it means never letting registration fail visibly (e.g. always report success, reveal a collision only by SMS/email) — a real redesign, not a wording fix, and not proportionate here: this is a B2B tool for transport companies, not a context where "is this phone a customer" is sensitive on its own, and F1's rate limit already makes bulk enumeration slow and expensive rather than free.
  *Tested:* two existing tests updated for the new message text, plus a new one — `Both_kinds_of_collision_produce_the_same_message` — asserting the two collision paths are now byte-identical, which is the actual property being fixed. 147/147 tests pass.
  *Verified live*: registered a company, then tried the same name+phone and a different-name-same-phone — both returned the identical message.
  *Evidence:* `src/TransTrack.Data/RegistrationService.cs`; `tests/TransTrack.Tests/RegistrationTests.cs`.

- [x] **F5 — Inconsistent guard-railing on unbounded queries.** *(fixed)*
  Extracted the count-then-refuse logic `GetTripsAsync` already had into a shared `EnsureUnderRowLimit(int, string)`, and applied it to `GetLedgerAsync` (counting both source tables combined, since both end up in memory together) and `GetPartyReportAsync`. Applied the same pattern to `GetVehicleSavingsAsync` (trips + maintenance combined).
  `TripTransactionService.GetPendingAsync` got a different shape deliberately: **capped at `MaxPending = 1000`, not refused.** Unlike a report export, an Owner facing an oversized approvals queue has no date range to narrow — a flat refusal would strand them, unable to see or clear any of it. Capped and ordered oldest-first instead, so the longest-waiting items are always what shows.
  *Tested:* two new xUnit tests seed rows directly (bypassing the slow trip-booking service path) to prove the boundary — `Ledger_report_refuses_when_the_combined_row_count_is_too_large` (proves the guard sums *both* source tables, not just one) and `An_oversized_pending_queue_is_capped_at_the_oldest_entries` (proves the cap-not-refuse shape and the ordering). 146/146 tests pass, +5s total suite time.
  *Verified live* (scratch database): registered a company, created a party, hit `vehicle-savings`, `party`, and `ledger` reports and `approvals/pending` — all returned `200` with the correct shape for the normal (under-limit) case.
  *Evidence:* `src/TransTrack.Data/ReportsService.cs`; `src/TransTrack.Data/TripTransactionService.cs`; `tests/TransTrack.Tests/ReportAccuracyTests.cs`; `tests/TransTrack.Tests/ApprovalTests.cs`.

- [x] **F6 — Duplicate exception handling across controllers.** *(fixed)*
  `ApiExceptionHandler.cs`'s own doc comment explains it replaced per-controller `catch (InvalidOperationException)` blocks. 25 such blocks still exist across 11 of 12 controllers. Not a functional bug — same response either way — but real, acknowledged-in-comments duplication.
  *Evidence:* `grep -rn "catch (InvalidOperationException" src/TransTrack.Api/Controllers/` → 25 hits.

- [ ] **F7 — Dashboard summary is five sequential round-trips with in-memory aggregation.**
  `DashboardService.GetSummaryAsync` opens the DB five times and pulls whole `Trip`/`TripExpense` collections into memory to sum computed C# properties that can't translate to SQL. Fine at current volumes; will show up as latency once trip counts grow into the thousands.
  *Evidence:* `src/TransTrack.Data/DashboardService.cs`.

### 🟢 Low

- [x] **F8 — Stale comment references the pre-rebrand domain.** *(fixed)*
  `Program.cs:15` still says `https://ttapi.sivayaantechnologies.com`; every actual config now uses `loapi.lorryowner.com`.

- [x] **F9 — Dead code: `TripService.GetTripsAsync()`.** *(fixed — corrected in the doing: it wasn't actually caller-free, four tests used it. Migrated those to the already-paginated `GetTripListAsync`, which proves the same behaviour, then deleted the method.)*

- [x] **F10 — `AuthController.Me()` fetches the whole user list to find one row.** *(fixed — added `AuthService.GetUserAsync(Guid)`.)*

- [ ] **F11 — Case-insensitive username lookups can't use the existing index.**
  `Username.ToLower() == x.ToLower()` is a full scan against the plain `Username` unique index. Negligible today.

---

## Next-Iteration Plan

Phased so the highest-value, lowest-risk items land first. Nothing here changes an API contract, database schema, or existing behaviour for a signed-in user going about their day — every item is additive or internal cleanup.

### Phase 1 — Quick wins (do first, same sitting)
1. **F6** — delete the 25 redundant controller-level catches; let `ApiExceptionHandler` do the job it was written for.
2. **F8** — fix the stale domain comment in `Program.cs`.
3. **F9** — delete the dead `GetTripsAsync()` method.
4. **F10** — targeted lookup in `AuthController.Me()` instead of fetching the whole user list.

*No open questions — straightforward cleanup, safe to do without asking anything first.*

### Phase 2 — High-severity security
5. **F1** — add ASP.NET Core's built-in rate limiter (`AddRateLimiter`), scoped to `/api/auth/login`, `/api/auth/register`, `/api/auth/change-password`, keyed by IP (and by username for login specifically, so one attacker IP can't lock out unrelated accounts under a stricter policy while a laxer per-IP policy still bounds total cost).
6. **F2** — extend the existing per-request middleware check (it already queries the DB once per authenticated request for the company) to also confirm the calling user is still active and still holds the role their token claims. Same cost pattern already accepted for the company check, so no new performance tradeoff to weigh.

*Open question before starting:* do you want `Jwt:FullSessionHours` (currently 12h) shortened as a second layer of mitigation, or is the per-request DB check alone sufficient? I'd lean toward leaving it at 12h once F2 lands — the DB check closes the actual gap, and a shorter session just means more re-logins for no added safety.

### Phase 3 — Medium-severity hardening
7. **F3** — baseline security headers, either via `next.config.ts` `headers()` or a Cloudflare Pages `_headers` file (the latter needs no rebuild to adjust and is probably the better fit given the deploy-by-ZIP workflow already in place).
8. **F5** — apply the `MaxRows` pattern to `GetLedgerAsync`, `GetVehicleSavingsAsync`, `GetPartyReportAsync`; paginate `GetPendingAsync` the same way the trips list was paginated.
9. **F4** — generic message on the registration collision path (low priority; flagging for completeness).

### Phase 4 — Longer-term, not urgent
10. **F7** — dashboard query consolidation, only if it becomes visibly slow.
11. **F11** — a functional index or a stored normalized-username column, only if the user table grows enough to matter (it won't for a long time at this app's scale).
12. **SQLite growth plan** — not a defect, but worth a written decision point: at what company/trip-count does a move to Postgres/SQL Server get scheduled, so it's a planned migration rather than a forced one. `IDocumentStorage` already models the "swap one line" pattern the DB engine doesn't have yet.

---

## What to preserve

Noted so a future refactor doesn't accidentally undo something deliberate:

- The global `ITenantEntity` query filter in `AppDbContext` — one filter covers every tenant-scoped table automatically; don't replace with per-repository filtering.
- The audit trail built into `SaveChanges` — same transaction as the change itself, covers every write path with no per-service step to remember.
- httpOnly + Secure + SameSite=Strict cookie, no token in `localStorage` — don't move auth state into client-readable storage for convenience.
- `ReportsService.MaxRows` pattern — the model to extend in Phase 3, not replace.
- `IDocumentStorage`'s "swap one line" seam for storage backends — the template for how the DB engine should eventually get the same treatment.

---

*Full findings detail — evidence, impact, and reasoning per item — is in the audit conversation. This document is the tracked, actionable summary.*
