# LorryOwner — web app

The mobile-first front end for LorryOwner. Next.js (App Router) + Tailwind,
talking to the ASP.NET Core API in [`../../src/TransTrack.Api`](../../src/TransTrack.Api).

> The folder, package and namespace names still say `transtrack` /
> `transtruck`. That is deliberate: those are internal identifiers, invisible
> to users, and renaming them would mean moving the live database and secrets
> folders by hand for no user-facing gain. Only the *branding* is LorryOwner.

## Running it

```bash
npm install
npm run dev
```

Starts on <http://localhost:3000> and expects the API on
<http://localhost:5034> (see `.env.local`). Start the API separately:

```bash
dotnet run --project ../../src/TransTrack.Api --urls http://localhost:5034
```

## Checks

```bash
npx tsc --noEmit
npm run build
```

## Brand assets

Everything the app serves under `public/` — the app icons, the maskable
Android icon, the apple-touch-icon, the favicon and the web-sized logo — is
**generated**, not hand-edited. The sources are the two files in
[`../../brand`](../../brand):

| Source | What it is |
|---|---|
| `brand/lorryowner-logo.png` | Full horizontal logo: mark + wordmark + tagline |
| `brand/lorryowner-mark.png` | Just the mark, cropped from the logo |

To change the branding, replace those two files and regenerate:

```bash
node gen-icons.mjs
```

Two things that script encodes, worth knowing before you change it:

- **The app icon uses the mark only.** A launcher renders an icon at roughly
  48px, where the wordmark becomes an unreadable smear. The wordmark still
  appears on the login screen, which has room for it.
- **iOS and Android need different files.** iOS ignores SVG for the
  home-screen icon (it would screenshot the page instead), so
  `apple-touch-icon.png` must be a real PNG. Android crops the maskable icon
  to its own shape, so that variant is full-bleed with the art inside the
  80% safe zone.

If you add a file to `public/` that the offline shell needs, add it to
`SHELL_URLS` in `public/sw.js` — and only if it certainly exists, because
`cache.addAll` is all-or-nothing and one 404 fails the whole install.

## Deploying

See [`../../DEPLOYMENT.md`](../../DEPLOYMENT.md). The live deployment goes
out through Cloudflare Pages; `deploy/frontend-artifacts/` holds the
ready-to-upload package and its instructions.
