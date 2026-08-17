// Regenerates every brand asset the app serves, from the two source files in
// brand/. Run from web/transtrack-web:  node gen-icons.mjs
//
// Sources (edit/replace these, never the generated files in public/):
//   ../../brand/lorryowner-logo.png  — full horizontal logo, mark + wordmark
//   ../../brand/lorryowner-mark.png  — just the mark, cropped from the logo
//
// The app icon deliberately uses the MARK ONLY. The wordmark is unreadable
// once a launcher scales the icon to ~48px, and a logo you can't read is
// worse than no logo — the wordmark still appears on the login screen, where
// there is room for it.
import sharp from 'sharp';

const BRAND = '../../brand';
const mark = `${BRAND}/lorryowner-mark.png`;
const logo = `${BRAND}/lorryowner-logo.png`;

const NAVY = '#1E3A8A';
const WHITE = '#ffffff';

/** The mark centred on a square, with breathing room round it. */
async function square(size, { inset = 0.78, background = WHITE } = {}) {
  const art = await sharp(mark)
    .resize({ width: Math.round(size * inset), height: Math.round(size * inset), fit: 'inside' })
    .toBuffer();
  const { width, height } = await sharp(art).metadata();

  return sharp({ create: { width: size, height: size, channels: 4, background } })
    .composite([{ input: art, top: Math.round((size - height) / 2), left: Math.round((size - width) / 2) }])
    .png()
    .toBuffer();
}

// Standard icons. iOS ignores SVG for the home-screen icon and applies its own
// rounded mask, so a plain square PNG is exactly what it wants.
for (const [name, size] of Object.entries({
  'icon-192.png': 192,
  'icon-512.png': 512,
  'apple-touch-icon.png': 180,
  'favicon-32.png': 32,
})) {
  // The favicon is tiny, so the mark gets nearly the whole tile or it turns to mush.
  await sharp(await square(size, { inset: 0.94 })).toFile(`public/${name}`);
}

// Maskable: Android crops to its own shape (circle, squircle, ...), so the art
// must sit inside the 80% safe zone and the background must bleed to the edge.
await sharp(await square(512, { inset: 0.74 })).toFile('public/icon-maskable-512.png');

// Web copies of the artwork itself. The originals are ~1.2 MB, which is real
// money on a phone connection — these are the sizes actually displayed.
await sharp(logo).resize({ width: 900 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-logo.png');
await sharp(mark).resize({ width: 320 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-mark.png');

console.log('Brand assets written to public/');
