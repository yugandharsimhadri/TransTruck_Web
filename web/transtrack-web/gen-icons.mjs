// Regenerates every brand asset the app serves, from the two source files in
// brand/. Run from web/transtrack-web:  node gen-icons.mjs
//
// Sources (edit/replace these, never the generated files in public/):
//   ../../brand/lorryowner-logo.png  — full horizontal logo, mark + wordmark
//   ../../brand/lorryowner-mark.png  — the square app-icon badge
import sharp from 'sharp';

const BRAND = '../../brand';
const mark = `${BRAND}/lorryowner-mark.png`;
const logo = `${BRAND}/lorryowner-logo.png`;

/**
 * The supplied badge was exported with its transparency flattened to **black**,
 * so the area outside its rounded corners is solid 0,0,0. Left alone, every
 * icon carries black slivers — most visible on iOS, which applies its own
 * rounded mask at a slightly different radius than the artwork's.
 *
 * Flood-filling inward from the four corners fixes only that outside region.
 * A blanket "replace near-black with white" would have been simpler and wrong:
 * the lorry's outlines and the wordmark are near-black too, and would have been
 * punched out along with the background.
 */
async function whitenFlattenedCorners(source) {
  const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width, height, channels } = info;

  const isDark = (i) => data[i] < 24 && data[i + 1] < 24 && data[i + 2] < 24;
  const seen = new Uint8Array(width * height);
  const queue = [];

  const push = (x, y) => {
    if (x < 0 || y < 0 || x >= width || y >= height) return;
    const p = y * width + x;
    if (seen[p]) return;
    if (!isDark(p * channels)) return;
    seen[p] = 1;
    queue.push(p);
  };

  for (const [x, y] of [[0, 0], [width - 1, 0], [0, height - 1], [width - 1, height - 1]]) push(x, y);

  while (queue.length) {
    const p = queue.pop();
    const i = p * channels;
    data[i] = 255; data[i + 1] = 255; data[i + 2] = 255;
    if (channels === 4) data[i + 3] = 255;

    const x = p % width, y = (p / width) | 0;
    push(x + 1, y); push(x - 1, y); push(x, y + 1); push(x, y - 1);
  }

  return sharp(data, { raw: { width, height, channels } }).png().toBuffer();
}

const cleanedMark = await whitenFlattenedCorners(mark);

// The badge is already a finished app icon — its own background, border and
// rounded corners — so it fills the tile rather than being inset on a
// generated background the way a bare mark would be.
for (const [name, size] of Object.entries({
  'icon-192.png': 192,
  'icon-512.png': 512,
  'apple-touch-icon.png': 180,
  'favicon-32.png': 32,
})) {
  await sharp(cleanedMark).resize(size, size, { fit: 'cover' }).flatten({ background: '#ffffff' })
    .png().toFile(`public/${name}`);
}

// Maskable: Android crops to its own shape, so the badge is inset into the
// 80% safe zone on a white bleed — otherwise the crop clips its border.
await sharp({ create: { width: 512, height: 512, channels: 4, background: '#ffffff' } })
  .composite([{ input: await sharp(cleanedMark).resize(410, 410, { fit: 'inside' }).toBuffer(), top: 51, left: 51 }])
  .png().toFile('public/icon-maskable-512.png');

// Web copies of the artwork itself. The originals are over a megabyte, which
// is real money on a phone connection — these are the sizes actually shown.
await sharp(cleanedMark).resize({ width: 320 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-mark.png');

await sharp(logo).trim().resize({ width: 900 }).png({ quality: 90, compressionLevel: 9 })
  .toFile('public/lorryowner-logo.png');

console.log('Brand assets written to public/');
