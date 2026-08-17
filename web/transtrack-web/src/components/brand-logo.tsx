/* eslint-disable @next/next/no-img-element */

/**
 * The LorryOwner brand artwork. Two shapes, because a logo that works on a
 * sign-in card does not work in a 40px sidebar chip:
 *
 * - <BrandLogo> — the full horizontal logo, mark plus wordmark. Needs room,
 *   so it's used where there is some: the login and registration screens.
 * - <BrandMark> — just the mark, on a white tile. The artwork has a white
 *   background (the lorry's cab is white, so the background can't simply be
 *   keyed out), and the app shell is dark — so rather than fight that, the
 *   white is made deliberate: a rounded tile that reads as a badge.
 *
 * Both are plain <img> rather than next/image: these are fixed-size, already
 * optimised by gen-icons.mjs, and served from the same origin, so the loader
 * would add machinery for nothing.
 */
export function BrandLogo({ className }: { className?: string }) {
  return (
    <img
      src="/lorryowner-logo.png"
      alt="LorryOwner — Drive, Manage, Grow"
      className={className}
      width={900}
      height={600}
    />
  );
}

export function BrandMark({ className }: { className?: string }) {
  return (
    <span
      className={`flex items-center justify-center overflow-hidden rounded-2xl bg-white ${className ?? ""}`}
    >
      <img src="/lorryowner-mark.png" alt="" aria-hidden="true" className="h-full w-full object-contain p-1" />
    </span>
  );
}
