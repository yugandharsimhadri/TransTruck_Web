/**
 * A thin wrapper over the Web Share API — the mechanism behind "Share" on
 * a phone bringing up WhatsApp/Mail/etc. directly, rather than a manual
 * download-then-attach. Two entry points:
 *
 *  - shareText: a short text summary (works on effectively every mobile
 *    browser today).
 *  - shareFile: an actual file (the LR/Bill/report PDFs once those exist)
 *    — falls back to a normal download when the browser or device doesn't
 *    support sharing files (most desktop browsers), so it's always safe
 *    to call.
 *
 * Both resolve to what actually happened so the caller can show the right
 * feedback — the native share sheet already gives its own confirmation, so
 * only the fallback paths need a toast from the caller.
 */

export type ShareOutcome = "shared" | "cancelled" | "copied" | "downloaded" | "unavailable";

export async function shareText(title: string, text: string, url?: string): Promise<ShareOutcome> {
  if (typeof navigator !== "undefined" && navigator.share) {
    try {
      await navigator.share({ title, text, url });
      return "shared";
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") return "cancelled";
      throw err;
    }
  }

  // No Web Share API (most desktop browsers) — copy to clipboard instead
  // of failing silently, so there's still something useful to paste into
  // WhatsApp Web or wherever. Clipboard access itself can still be denied
  // (no secure context, unfocused document, permission blocked) — that's
  // a real "nothing happened" case for the caller, not an uncaught crash.
  try {
    await navigator.clipboard.writeText(url ? `${text}\n${url}` : text);
    return "copied";
  } catch {
    return "unavailable";
  }
}

export async function shareFile(file: File, opts?: { title?: string; text?: string }): Promise<ShareOutcome> {
  if (typeof navigator !== "undefined" && navigator.canShare?.({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: opts?.title, text: opts?.text });
      return "shared";
    } catch (err) {
      if (err instanceof Error && err.name === "AbortError") return "cancelled";
      // Fall through to a plain download if sharing the file itself failed
      // for some other reason.
    }
  }

  downloadFile(file);
  return "downloaded";
}

function downloadFile(file: File): void {
  const url = URL.createObjectURL(file);
  const link = document.createElement("a");
  link.href = url;
  link.download = file.name;
  // Safari ignores the download attribute for a blob and navigates instead, so
  // without a target the current page — the form the user was filling in —
  // would be replaced by the PDF.
  link.target = "_blank";
  link.rel = "noopener";
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);

  // Revoking immediately after click() is a race: Safari has not started
  // fetching the blob yet at that point, so the URL dies first and nothing
  // opens. Held briefly instead, then released so the file doesn't sit in
  // memory for the rest of the session.
  setTimeout(() => URL.revokeObjectURL(url), 60_000);
}
