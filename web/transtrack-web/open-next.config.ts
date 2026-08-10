import { defineCloudflareConfig } from "@opennextjs/cloudflare";

// Minimal config: this app has no ISR/revalidation and no next/image usage
// (the company logo renders as a plain <img>), so none of the optional
// R2/KV-backed incremental-cache or image-loader overrides apply here.
export default defineCloudflareConfig();
