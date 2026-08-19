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

/** The mark, trimmed of its transparent margin and centred on a square.
 *
 * The trim matters: the source art carries a wide transparent border, and
 * fitting that whole canvas into the tile left the lorry floating small in
 * the middle with a ring of dead space. Trimming to the ink first means
 * `inset` measures the artwork itself, so the mark actually fills the icon.
 *
 * Backgrounds are painted rather than left transparent — a launcher that
 * composites a transparent icon onto a dark wallpaper turns the lorry's dark
 * outlines into mud, and Android's maskable slot requires opaque corners. */
async function square(size, { inset = 0.86, background = WHITE } = {}) {
  const art = await sharp(mark)
    .trim()
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
  await sharp(await square(size, { inset: 0.86 })).toFile(`public/${name}`);
}

// Maskable: Android crops to its own shape (circle, squircle, ...), so the art
// must sit inside the 80% safe zone and the background must bleed to the edge.
await sharp(await square(512, { inset: 0.68 })).toFile('public/icon-maskable-512.png');

// Web copies of the artwork itself. The originals are ~1.2 MB, which is real
// money on a phone connection — these are the sizes actually displayed.
await sharp(logo).trim().resize({ width: 900 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-logo.png');
await sharp(mark).trim().resize({ width: 320 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-mark.png');

console.log('Brand assets written to public/');
