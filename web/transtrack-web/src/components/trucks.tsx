/**
 * A small fleet of side-profile truck glyphs, all drawn on the same 64×32
 * grid so they're interchangeable wherever a driving truck is shown — same
 * wheelbase (cx 13 / cx 49, y 26), same ground line, so swapping variants
 * never shifts the road.
 *
 * Same drawing language as TruckMark: rounded, friendly, currentColor only,
 * translucent fills over a solid stroke. Inline SVG rather than image assets
 * — a few hundred bytes each, crisp at any size, no network request, and
 * they inherit the theme colour for free in both light and dark mode.
 */

export type TruckVariant = "box" | "tanker" | "lorry" | "container" | "pickup";

export const truckVariants: TruckVariant[] = ["box", "tanker", "lorry", "container", "pickup"];

interface TruckProps {
  className?: string;
}

/** Shared wheel pair — one place to keep every truck rolling on the same axles. */
function Wheels({ front = 49, rear = 13 }: { front?: number; rear?: number }) {
  return (
    <g className="truck-wheels">
      <circle cx={rear} cy="26" r="4.5" fill="currentColor" fillOpacity="0.9" />
      <circle cx={rear} cy="26" r="4.5" stroke="currentColor" strokeWidth="1.8" />
      <circle cx={rear} cy="26" r="1.4" fill="currentColor" />
      <circle cx={front} cy="26" r="4.5" fill="currentColor" fillOpacity="0.9" />
      <circle cx={front} cy="26" r="4.5" stroke="currentColor" strokeWidth="1.8" />
      <circle cx={front} cy="26" r="1.4" fill="currentColor" />
    </g>
  );
}

/** The cab every forward-control truck shares — bonnet, windscreen, door line. */
function Cab({ x = 40 }: { x?: number }) {
  return (
    <g>
      <path
        d={`M${x} 12h9.5a2 2 0 0 1 1.7 1l3.6 5.6a2 2 0 0 1 .3 1.1V25a2 2 0 0 1-2 2H${x}V12Z`}
        fill="currentColor"
        fillOpacity="0.28"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      {/* windscreen */}
      <path
        d={`M${x + 2.5} 14.5h6.2l2.9 4.4h-9.1v-4.4Z`}
        fill="currentColor"
        fillOpacity="0.55"
      />
    </g>
  );
}

function Ground() {
  // A stub of road under the wheels so the truck never looks like it floats.
  return <path d="M3 30.5h52" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" strokeOpacity="0.35" />;
}

/** Closed box body — the everyday goods carrier. */
export function BoxTruck({ className }: TruckProps) {
  return (
    <svg viewBox="0 0 64 32" fill="none" className={className} aria-hidden="true">
      <rect x="3" y="7" width="36" height="20" rx="2.5" fill="currentColor" fillOpacity="0.16" stroke="currentColor" strokeWidth="1.8" />
      <path d="M9 12h18M9 17h13" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeOpacity="0.6" />
      <Cab />
      <Wheels />
      <Ground />
    </svg>
  );
}

/** Fuel tanker — the rounded cylinder reads instantly as "diesel". */
export function TankerTruck({ className }: TruckProps) {
  return (
    <svg viewBox="0 0 64 32" fill="none" className={className} aria-hidden="true">
      <rect x="3" y="11" width="36" height="15" rx="7.5" fill="currentColor" fillOpacity="0.16" stroke="currentColor" strokeWidth="1.8" />
      <path d="M14 11.5v14M25 11.5v14" stroke="currentColor" strokeWidth="1.5" strokeOpacity="0.55" />
      <rect x="17" y="8" width="6" height="3.5" rx="1.2" fill="currentColor" fillOpacity="0.5" />
      <Cab />
      <Wheels />
      <Ground />
    </svg>
  );
}

/** High-sided open lorry — the classic Indian goods carrier silhouette. */
export function LorryTruck({ className }: TruckProps) {
  return (
    <svg viewBox="0 0 64 32" fill="none" className={className} aria-hidden="true">
      <path
        d="M3 8.5a2 2 0 0 1 2-2h32a2 2 0 0 1 2 2V27H5a2 2 0 0 1-2-2V8.5Z"
        fill="currentColor"
        fillOpacity="0.16"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      {/* side slats */}
      <path d="M11 7v20M19 7v20M27 7v20M34 7v20" stroke="currentColor" strokeWidth="1.4" strokeOpacity="0.5" />
      <path d="M3 20h36" stroke="currentColor" strokeWidth="1.6" strokeOpacity="0.65" />
      <Cab />
      <Wheels />
      <Ground />
    </svg>
  );
}

/** Articulated container trailer — the long-haul run. */
export function ContainerTruck({ className }: TruckProps) {
  return (
    <svg viewBox="0 0 64 32" fill="none" className={className} aria-hidden="true">
      <rect x="2" y="9" width="34" height="16" rx="1.5" fill="currentColor" fillOpacity="0.16" stroke="currentColor" strokeWidth="1.8" />
      {/* corrugation */}
      <path d="M8 10v14M13 10v14M18 10v14M23 10v14M28 10v14" stroke="currentColor" strokeWidth="1.2" strokeOpacity="0.45" />
      {/* coupling between trailer and cab */}
      <path d="M36 22h4" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      <Cab x={40} />
      <Wheels rear={11} front={49} />
      <circle cx="19" cy="26" r="4.5" fill="currentColor" fillOpacity="0.9" />
      <circle cx="19" cy="26" r="4.5" stroke="currentColor" strokeWidth="1.8" />
      <circle cx="19" cy="26" r="1.4" fill="currentColor" />
      <Ground />
    </svg>
  );
}

/** Small pickup — the short local run. */
export function PickupTruck({ className }: TruckProps) {
  return (
    <svg viewBox="0 0 64 32" fill="none" className={className} aria-hidden="true">
      <path
        d="M12 17h26a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H12v-10Z"
        fill="currentColor"
        fillOpacity="0.16"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <path
        d="M12 27V13.5a2 2 0 0 1 2-2h8.5a2 2 0 0 1 1.7 1l3.3 4.5"
        fill="currentColor"
        fillOpacity="0.3"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <path d="M14.5 14h7l2.4 3h-9.4v-3Z" fill="currentColor" fillOpacity="0.55" />
      <Wheels rear={19} front={35} />
      <Ground />
    </svg>
  );
}

const byVariant: Record<TruckVariant, (p: TruckProps) => React.JSX.Element> = {
  box: BoxTruck,
  tanker: TankerTruck,
  lorry: LorryTruck,
  container: ContainerTruck,
  pickup: PickupTruck,
};

export function Truck({ variant, className }: { variant: TruckVariant; className?: string }) {
  const Component = byVariant[variant];
  return <Component className={className} />;
}
