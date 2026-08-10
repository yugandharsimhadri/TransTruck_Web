/**
 * The app's own truck mark — a friendly, rounded delivery-truck glyph in
 * the current text color, not a photo or bitmap. Kept as inline SVG
 * (a few hundred bytes, no network request, crisp at any size) rather than
 * an image asset, and simple enough to read instantly at a glance — the
 * whole point for people who don't want to parse a busy icon.
 */
export function TruckMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" fill="none" className={className} aria-hidden="true">
      <path
        d="M4 12a2 2 0 0 1 2-2h18a2 2 0 0 1 2 2v18H6a2 2 0 0 1-2-2V12Z"
        fill="currentColor"
        fillOpacity="0.16"
      />
      <path
        d="M4 12a2 2 0 0 1 2-2h18a2 2 0 0 1 2 2v18H6a2 2 0 0 1-2-2V12Z"
        stroke="currentColor"
        strokeWidth="2.25"
        strokeLinejoin="round"
      />
      <path
        d="M26 19h8.17a2 2 0 0 1 1.6.8l4.83 6.43a2 2 0 0 1 .4 1.2V30a2 2 0 0 1-2 2H26v-13Z"
        fill="currentColor"
        fillOpacity="0.28"
        stroke="currentColor"
        strokeWidth="2.25"
        strokeLinejoin="round"
      />
      <circle cx="14" cy="34" r="4" fill="currentColor" fillOpacity="0.9" />
      <circle cx="14" cy="34" r="4" stroke="currentColor" strokeWidth="2" />
      <circle cx="34" cy="34" r="4" fill="currentColor" fillOpacity="0.9" />
      <circle cx="34" cy="34" r="4" stroke="currentColor" strokeWidth="2" />
      <path d="M18 34h12" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" />
      <path d="M2 30h2M2 25h2" stroke="currentColor" strokeWidth="2.25" strokeLinecap="round" />
    </svg>
  );
}
