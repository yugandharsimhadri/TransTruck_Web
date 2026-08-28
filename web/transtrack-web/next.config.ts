import type { NextConfig } from "next";

// Baseline security headers, applied here (rather than a Cloudflare Pages
// `_headers` file) so they cover the actual page responses. This app's
// pages are rendered by the OpenNext worker, not served as static files —
// Cloudflare's `_headers` mechanism only rewrites headers on responses its
// own static-asset layer serves, so it would have silently missed every
// page load and only touched the hashed JS/CSS under _next/static.
//
// script-src needs 'unsafe-inline': next-themes (src/components/theme-provider.tsx)
// injects a small inline script before hydration to set the theme class
// early and avoid a flash of the wrong theme, and there is no nonce wired
// through this app's rendering path to allow it more narrowly. That is a
// real gap in this CSP, not a full mitigation — it still blocks the more
// common case of an attacker's *own* script being loaded from somewhere
// else, which matters here because nothing in this app currently uses
// dangerouslySetInnerHTML, so there is no built-in HTML-injection path for
// a stored value to turn into a script tag in the first place.
// React's own dev build uses eval() for its debugging tools (reconstructing
// component stacks across HMR boundaries) — never in production, by React's
// own doc comment when it hits this. Allowing it only outside production
// keeps `next dev` console-clean without weakening the policy an attacker
// could actually reach.
const scriptSrc = process.env.NODE_ENV === "production"
  ? "script-src 'self' 'unsafe-inline'"
  : "script-src 'self' 'unsafe-inline' 'unsafe-eval'";

// connect-src has to name the API's origin, because it is on a different host
// from the pages. Taking that name from NEXT_PUBLIC_API_URL — the same setting
// the client actually calls (src/lib/api.ts) — rather than from a fixed list is
// what keeps the two from drifting apart: a build pointed at a staging API, or
// at the UAT harness on :5311, is otherwise blocked by its own CSP with nothing
// in the UI to say why. It surfaces as "Something went wrong", because a CSP
// refusal looks to fetch() exactly like a network failure.
//
// The two defaults stay listed so an unconfigured build still works: production
// deploys against loapi, and a plain `next dev` falls back to :5034, which is
// the same fallback api.ts uses.
const apiOrigins = (() => {
  const origins = new Set(["https://loapi.lorryowner.com", "http://localhost:5034"]);

  const configured = process.env.NEXT_PUBLIC_API_URL;
  if (configured) {
    try {
      origins.add(new URL(configured).origin);
    } catch {
      // A malformed URL is the client's problem to report, not the CSP's to crash on.
    }
  }

  return [...origins].join(" ");
})();

const securityHeaders = [
  { key: "X-Content-Type-Options", value: "nosniff" },
  // DENY rather than SAMEORIGIN: this app is never meant to be framed,
  // including by itself.
  { key: "X-Frame-Options", value: "DENY" },
  { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
  // No device API this app uses today (camera, mic, geolocation, ...) —
  // deny all of them so a future dependency can't quietly start asking.
  { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=()" },
  {
    key: "Content-Security-Policy",
    value: [
      "default-src 'self'",
      scriptSrc,
      // Tailwind's utility classes are plain classes, not inline styles, but
      // React itself sets a handful of inline style attributes (e.g. the
      // truck-animation components), so style-src needs the same allowance.
      "style-src 'self' 'unsafe-inline'",
      "img-src 'self' data: blob:",
      "font-src 'self'",
      // The API lives on a different host (loapi.lorryowner.com in
      // production) — fetch/XHR to it is a connect-src concern, not
      // script-src, and cookies already scope what it's trusted with.
      `connect-src 'self' ${apiOrigins}`,
      "frame-ancestors 'none'",
      "base-uri 'self'",
      "form-action 'self'",
    ].join("; "),
  },
];

const nextConfig: NextConfig = {
  async headers() {
    return [{ source: "/:path*", headers: securityHeaders }];
  },
};

export default nextConfig;

// Gives `next dev` access to the same Cloudflare bindings the deployed
// Worker sees (this app declares none today — no KV/R2 — but this is what
// lets `wrangler.jsonc` stay the single source of truth if that changes,
// rather than local dev silently drifting from production).
import { initOpenNextCloudflareForDev } from "@opennextjs/cloudflare";
initOpenNextCloudflareForDev();
