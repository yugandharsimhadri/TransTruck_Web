/**
 * Gets a picked file ready to upload — in practice, shrinking a phone photo.
 *
 * A photo straight off an iPhone is 2-5 MB, so with any sane size limit most
 * of them would be rejected the moment someone photographs a permit. That
 * reads as "upload is broken", and telling the user to go and re-take the
 * photo at a lower resolution is not a real answer on a phone.
 *
 * So an image is re-encoded before it is sent: scaled down to a long edge that
 * is still comfortably readable for a scanned document, then saved as JPEG at
 * a quality stepped down until it fits. A permit photographed at 4032x3024
 * lands around 300-600 KB this way and stays perfectly legible.
 *
 * Two useful side effects on iOS specifically:
 *  - HEIC becomes JPEG. Safari decodes HEIC natively, so the conversion
 *    happens on the device that produced it; the stored file is then readable
 *    on Android and desktop, which cannot decode HEIC at all.
 *  - EXIF rotation is baked in, so a photo taken sideways is stored upright
 *    rather than relying on every future viewer honouring the orientation tag.
 *
 * PDFs are passed through untouched — there is nothing safe to re-encode.
 * Anything that fails to decode is also passed through untouched and left to
 * the server to accept or refuse; a broken optimisation must never become a
 * blocked upload.
 */

/** Long edge, in pixels, of the re-encoded image. Generous enough that small
 *  print on a permit stays readable, small enough to cut a 4 MB photo to well
 *  under a megabyte. Also keeps the canvas far below iOS Safari's ~16.7M pixel
 *  ceiling, above which it silently hands back a blank image. */
const MAX_EDGE = 2000;

/** Tried in order until one fits the limit. Below ~0.5 a photographed document
 *  starts to smear, so the last resort is a smaller image rather than a worse
 *  one. */
const QUALITIES = [0.85, 0.7, 0.55];

export type PrepareResult =
  | { ok: true; file: File; shrunkFrom?: number }
  | { ok: false; reason: string };

export async function prepareUpload(file: File, maxBytes: number): Promise<PrepareResult> {
  const isPdf = file.type === "application/pdf" || /\.pdf$/i.test(file.name);

  // A PDF can only be checked, not shrunk.
  if (isPdf) {
    return file.size <= maxBytes
      ? { ok: true, file }
      : { ok: false, reason: tooBig(file.size, maxBytes, "PDF") };
  }

  const original = file.size;

  const shrunk = await reencode(file, maxBytes);
  if (shrunk) {
    return shrunk.size < original
      ? { ok: true, file: shrunk, shrunkFrom: original }
      : { ok: true, file: shrunk };
  }

  // Couldn't decode it — an unusual format, or a file that isn't really an
  // image. Send the original if it fits and let the server have the last word
  // on whether the format is acceptable.
  return file.size <= maxBytes
    ? { ok: true, file }
    : { ok: false, reason: tooBig(file.size, maxBytes) };
}

function tooBig(size: number, maxBytes: number, kind = "file"): string {
  return (
    `That ${kind} is ${mb(size)} MB and the limit is ${mb(maxBytes)} MB. ` +
    (kind === "PDF"
      ? "Scan it at a lower quality, or photograph the pages instead."
      : "Try photographing it again.")
  );
}

const mb = (bytes: number) => (bytes / (1024 * 1024)).toFixed(1).replace(/\.0$/, "");

/** Decodes the image and re-encodes it as JPEG small enough to send, or null
 *  when it cannot be decoded at all. */
async function reencode(file: File, maxBytes: number): Promise<File | null> {
  const source = await decode(file);
  if (!source) return null;

  try {
    let edge = MAX_EDGE;

    // Two passes: quality first (cheap, invisible), then a smaller image if
    // even the lowest quality is still too heavy — which happens with a very
    // detailed photo, rarely with a document.
    for (let attempt = 0; attempt < 3; attempt++) {
      const canvas = draw(source, edge);
      if (!canvas) return null;

      for (const quality of QUALITIES) {
        const blob = await toBlob(canvas, quality);
        if (!blob) return null;
        if (blob.size <= maxBytes) return asJpeg(blob, file.name);
      }

      edge = Math.round(edge * 0.7);
    }

    return null;
  } finally {
    if ("close" in source) source.close();
  }
}

async function decode(file: File): Promise<ImageBitmap | HTMLImageElement | null> {
  // createImageBitmap handles HEIC on Safari and applies EXIF orientation
  // without a round trip through the DOM.
  if (typeof createImageBitmap === "function") {
    try {
      return await createImageBitmap(file, { imageOrientation: "from-image" });
    } catch {
      // Older Safari rejects the options bag rather than ignoring it.
      try {
        return await createImageBitmap(file);
      } catch {
        // Fall through to the <img> path.
      }
    }
  }

  const url = URL.createObjectURL(file);
  try {
    return await new Promise<HTMLImageElement | null>((resolve) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => resolve(null);
      img.src = url;
    });
  } finally {
    URL.revokeObjectURL(url);
  }
}

function draw(source: ImageBitmap | HTMLImageElement, maxEdge: number): HTMLCanvasElement | null {
  const sw = "naturalWidth" in source ? source.naturalWidth : source.width;
  const sh = "naturalHeight" in source ? source.naturalHeight : source.height;
  if (!sw || !sh) return null;

  // Never scale up — a small photo stays its own size.
  const scale = Math.min(1, maxEdge / Math.max(sw, sh));
  const canvas = document.createElement("canvas");
  canvas.width = Math.max(1, Math.round(sw * scale));
  canvas.height = Math.max(1, Math.round(sh * scale));

  const ctx = canvas.getContext("2d");
  if (!ctx) return null;

  // A JPEG has no transparency, so anything transparent would otherwise come
  // out black. Paint white behind it first.
  ctx.fillStyle = "#fff";
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  ctx.drawImage(source, 0, 0, canvas.width, canvas.height);
  return canvas;
}

const toBlob = (canvas: HTMLCanvasElement, quality: number) =>
  new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, "image/jpeg", quality));

function asJpeg(blob: Blob, originalName: string): File {
  const base = originalName.replace(/\.[^.]+$/, "") || "document";
  return new File([blob], `${base}.jpg`, { type: "image/jpeg", lastModified: Date.now() });
}
