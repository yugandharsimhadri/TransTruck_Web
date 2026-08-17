// Regenerates the installable icon set from the brand artwork.
// Run from web/transtrack-web:  node gen-icons.mjs
//
// Sources (edit these, not the PNGs):
//   ../../brand/logo-a-ring.svg           — the app icon
//   ../../brand/logo-a-ring-maskable.svg  — full-bleed variant for Android
import sharp from 'sharp';
import { readFileSync, copyFileSync } from 'fs';

const BRAND = '../../brand';
copyFileSync(`${BRAND}/logo-a-ring.svg`, 'public/icon.svg');

const icon = readFileSync('public/icon.svg');
const maskable = readFileSync(`${BRAND}/logo-a-ring-maskable.svg`);

// iOS ignores SVG for the home-screen icon, so apple-touch-icon must be a
// real PNG or "Add to Home Screen" falls back to a screenshot of the page.
const sizes = {
  'icon-192.png': 192,
  'icon-512.png': 512,
  'apple-touch-icon.png': 180,
  'favicon-32.png': 32,
};

for (const [name, size] of Object.entries(sizes))
  await sharp(icon, { density: 400 }).resize(size, size).png().toFile(`public/${name}`);

await sharp(maskable, { density: 400 }).resize(512, 512).png().toFile('public/icon-maskable-512.png');

console.log('Icons written to public/');
