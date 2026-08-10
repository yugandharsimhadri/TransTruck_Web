import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  /* config options here */
};

export default nextConfig;

// Gives `next dev` access to the same Cloudflare bindings the deployed
// Worker sees (this app declares none today — no KV/R2 — but this is what
// lets `wrangler.jsonc` stay the single source of truth if that changes,
// rather than local dev silently drifting from production).
import { initOpenNextCloudflareForDev } from "@opennextjs/cloudflare";
initOpenNextCloudflareForDev();
