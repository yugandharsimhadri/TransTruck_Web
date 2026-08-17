/**
 * The LorryOwner brand mark — a lorry inside the "O" of Owner, which doubles
 * as a route loop. Drawn in the current text color as inline SVG (a few
 * hundred bytes, no network request, crisp at any size) so it inherits
 * whatever surface it sits on, light or dark.
 *
 * Kept at this filename/export so every existing usage — sidebar, mobile top
 * bar, login card — picks up the new mark without a rename touching them.
 * The full-colour version of the same artwork is brand/logo-a-ring.svg, which
 * generates the installable app icons via web/transtrack-web/gen-icons.mjs.
 */
export function TruckMark({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 48 48" fill="none" className={className} aria-hidden="true">
      <circle cx="24" cy="24" r="21" stroke="currentColor" strokeWidth="2.5" strokeOpacity="0.85" />

      <rect x="12" y="18" width="12.5" height="9" rx="1.6" fill="currentColor" />
      <path
        d="M25.6 20.8h4.2a1.5 1.5 0 0 1 1.2.6l2.8 3.6a1.5 1.5 0 0 1 .3.9v.7a.9.9 0 0 1-.9.9h-7.6a.9.9 0 0 1-.9-.9v-4.9a.9.9 0 0 1 .9-.9Z"
        fill="currentColor"
      />

      {/* Wheels are knocked out of the body rather than stroked, so the mark
          stays readable at 16px where a hairline stroke would disappear. */}
      <circle cx="16.5" cy="29.5" r="3.1" fill="currentColor" />
      <circle cx="16.5" cy="29.5" r="1.2" fill="var(--background, #fff)" />
      <circle cx="30.5" cy="29.5" r="3.1" fill="currentColor" />
      <circle cx="30.5" cy="29.5" r="1.2" fill="var(--background, #fff)" />
    </svg>
  );
}
