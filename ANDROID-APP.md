# Shipping LorryOwner as an Android app

**Written:** 2026-10-13
**Status: plan only — nothing implemented.**
**Scope:** getting the existing web app onto Android phones, and onto the Play
Store if that turns out to be wanted.

The headline: **no rewrite is needed, and no second codebase.** The web app is
already a valid, installable PWA. Everything below is packaging.

---

## What already exists

Checked in `web/transtrack-web`, 2026-10-13:

| Requirement for installability | Status |
|---|---|
| `public/manifest.json` with `name`, `start_url`, `scope`, `id` | present (`start_url: /dashboard`) |
| `display: standalone`, `orientation: portrait-primary` | present |
| 192px and 512px icons | `icon-192.png`, `icon-512.png` |
| A **maskable** icon (Android crops to its own shape) | `icon-maskable-512.png` |
| Service worker with a `fetch` handler | `public/sw.js` — install, activate, fetch |
| Registration + install prompt | `src/components/sw-register.tsx`, `src/lib/use-pwa-install.ts` |
| HTTPS | `lorryowner.com` via Cloudflare |

That is every box Chrome checks before it will offer to install a site. There
is nothing to add for phase 0.

---

## Phase 0 — Installable today, no work, no cost

Users open `lorryowner.com` in Chrome on Android and choose **Add to Home
Screen** (or accept the prompt `use-pwa-install.ts` raises). They get a real
launcher icon, no browser chrome, portrait lock, and the offline shell.

Worth being clear about what this already buys, because it is most of what a
store app would:

- Updates reach every user the moment Cloudflare Pages is redeployed — no
  review queue, no "please update the app" support calls.
- One codebase, one deploy, one set of bugs.
- Nothing to sign, nothing to renew.

For an app sold to known customers rather than discovered by browsing a store,
this is frequently the whole answer. **Start here.**

### Its limits, honestly

- No Play Store presence, so no store search discovery.
- Push notifications work on Android Chrome but are a separate piece of work
  (web push + a server to send them) and do not work on iOS the same way.
- Some customers simply expect a Play Store listing and judge a product by it.
  That is a real reason to do phase 1, and it is a business reason rather than
  a technical one.

---

## Phase 1 — Play Store, via a Trusted Web Activity

A TWA is a thin native wrapper that opens the PWA full-screen with no browser
UI. The web app stays exactly as it is; the wrapper is generated.

```bash
npm install -g @bubblewrap/cli
bubblewrap init --manifest https://lorryowner.com/manifest.json
bubblewrap build        # produces app-release-bundle.aab for the Play Console
```

[PWABuilder](https://www.pwabuilder.com) does the same thing through a web UI
if the CLI is awkward on Windows.

### What has to be arranged

| Item | Detail |
|---|---|
| Play Console account | $25, one-off, personal or company |
| Signing keystore | Bubblewrap generates one. **Back it up somewhere offline.** Lose it and the listing can never be updated again — a new key means a new app |
| `assetlinks.json` | Must be reachable at `https://lorryowner.com/.well-known/assetlinks.json`, carrying the signing certificate's SHA-256 fingerprint |
| Privacy policy | A public URL. Play rejects listings without one |
| Listing assets | 512px icon, 1024x500 feature graphic, phone screenshots |
| Data safety form | Declare what the app collects. This app holds business records and logins — answer it honestly |

### The one that catches everyone

**`assetlinks.json`.** It is what proves the website and the app belong to the
same owner. Without it the TWA still runs, but Chrome shows its address bar
across the top and the result looks like a browser, not an app.

It is a static file, so it belongs in the Next app and deploys with the normal
Pages package:

```
web/transtrack-web/public/.well-known/assetlinks.json
```

Bubblewrap prints the exact contents, including the fingerprint, during
`build`. Verify after deploying with:

```
https://lorryowner.com/.well-known/assetlinks.json
```

If Play re-signs the app (Play App Signing, which is the default), the
fingerprint to publish is **Play's**, from Play Console → Setup → App signing —
not the local keystore's. Getting this wrong is the usual cause of "it worked
in testing and shows a URL bar in production".

### Effort

Half a day of work, then Play review — usually 1–3 days for a first
submission, and longer if the data safety form raises questions.

---

## Phase 2 — Only when a native capability is actually needed

Reach for [Capacitor](https://capacitorjs.com) if a real device feature becomes
necessary:

- push notifications on both platforms
- camera capture for vehicle documents that works offline
- queuing writes while out of signal and syncing later
- biometric unlock

This is a genuine project, not a wrapper: a native shell, a plugin layer, two
store pipelines, and a build that can break independently of the web app.
**Do not pre-empt it.** Nothing in the current product needs it.

---

## iOS, briefly

iPhone users can **Add to Home Screen** today and get a standalone app, which
is worth telling them. But Apple does not support install prompts, web push is
restricted, and a TWA is Android-only. An App Store presence would mean
phase 2 (Capacitor) and an Apple Developer account at $99/year. Out of scope
until someone asks.

---

## Recommendation

1. **Now:** tell customers to Add to Home Screen. Costs nothing, works today.
2. **When asked for:** phase 1 TWA. Budget half a day plus review.
3. **Only on a concrete need:** phase 2.

One sequencing note: a store app points at the same API as the website, so it
inherits whatever state the API is in. Do not submit to Play while production
is unstable — a rejected or broken first release is expensive to live down,
and unlike the PWA it cannot be fixed by redeploying in five minutes.

---

## Related

- [DEPLOYMENT.md](DEPLOYMENT.md) — how the frontend is built and deployed
  (Cloudflare Pages, `make-pages-package.ps1`)
- `web/transtrack-web/README.md` — where the icons come from and how they are
  regenerated (`gen-icons.mjs`); the same sources feed the Play listing
