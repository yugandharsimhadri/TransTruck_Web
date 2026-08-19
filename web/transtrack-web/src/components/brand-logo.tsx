/* eslint-disable @next/next/no-img-element */

/**
 * The LorryOwner brand artwork. Two shapes, because a logo that works on a
 * sign-in card does not work in a 40px sidebar chip:
 *
 * - <BrandLogo> — the full horizontal logo, mark plus wordmark. Needs room,
 *   so it's used where there is some: the login and registration screens.
 * - <BrandMark> — just the mark. The artwork is transparent, so it sits
 *   directly on whatever surface it lands on, light or dark, with no tile
 *   behind it. (It used to need a white tile: the earlier art had a baked-in
 *   white background that could not be keyed out, since the lorry's cab is
 *   white too.)
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
    <img
      src="/lorryowner-mark.png"
      alt=""
      aria-hidden="true"
      className={`shrink-0 object-contain ${className ?? ""}`}
    />
  );
}
