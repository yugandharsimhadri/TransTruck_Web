"use client";

import { useMemo } from "react";
import { Truck, truckVariants, type TruckVariant } from "@/components/trucks";
import { cn } from "@/lib/utils";

/**
 * The driving-truck strip: a truck ambles slowly left→right along a stretch
 * of road. Used as the app's "still alive" signal wherever the user is
 * waiting, and parked in empty states.
 *
 * The animation itself lives in globals.css (.truck-lane / .truck-runner) so
 * it stays compositor-only — this file just picks a truck and sets the pace.
 */

/** Rotates through the fleet so different screens get different trucks
 *  without every caller having to choose one by hand. */
function pickVariant(seed?: string | number): TruckVariant {
  if (typeof seed === "string") {
    let hash = 0;
    for (let i = 0; i < seed.length; i++) hash = (hash * 31 + seed.charCodeAt(i)) | 0;
    return truckVariants[Math.abs(hash) % truckVariants.length];
  }
  if (typeof seed === "number") return truckVariants[seed % truckVariants.length];
  return truckVariants[Math.floor(Math.random() * truckVariants.length)];
}

export function TruckDrive({
  variant,
  seed,
  speed = "normal",
  size = "md",
  showRoad = true,
  className,
}: {
  variant?: TruckVariant;
  /** Give the same string on a screen and it always gets the same truck. */
  seed?: string | number;
  speed?: "slow" | "normal" | "brisk";
  size?: "sm" | "md" | "lg";
  showRoad?: boolean;
  className?: string;
}) {
  const chosen = useMemo(() => variant ?? pickVariant(seed), [variant, seed]);

  const duration = { slow: "30s", normal: "22s", brisk: "14s" }[speed];
  const truckSize = { sm: "h-6 w-12", md: "h-8 w-16", lg: "h-11 w-22" }[size];
  const laneHeight = { sm: "h-8", md: "h-11", lg: "h-14" }[size];

  return (
    <div
      className={cn("truck-lane w-full", laneHeight, className)}
      style={{ ["--truck-duration" as string]: duration }}
      aria-hidden="true"
    >
      {showRoad && (
        <div className="truck-road absolute inset-x-0 bottom-0 h-px text-muted-foreground/40" />
      )}
      <div className="truck-runner">
        <div className="truck-body">
          <Truck variant={chosen} className={cn(truckSize, "text-primary")} />
        </div>
      </div>
    </div>
  );
}

/**
 * Full-screen waiting state — the truck plus a line of text. Replaces the
 * bare "Loading…" that used to sit in the middle of a blank screen.
 */
export function TruckLoading({
  message = "Loading…",
  seed,
  className,
}: {
  message?: string;
  seed?: string | number;
  className?: string;
}) {
  return (
    <div className={cn("flex min-h-[60vh] flex-col items-center justify-center gap-4 px-6", className)}>
      <div className="w-full max-w-xs">
        <TruckDrive seed={seed} speed="brisk" size="lg" />
      </div>
      <p className="text-sm text-muted-foreground" role="status">
        {message}
      </p>
    </div>
  );
}

/**
 * Empty state — a parked truck, a headline and an optional hint. Reads as
 * "nothing here yet" rather than "something went wrong", which grey text on
 * its own never quite manages.
 */
export function TruckEmpty({
  title,
  hint,
  variant,
  seed,
  action,
  className,
}: {
  title: string;
  hint?: string;
  variant?: TruckVariant;
  seed?: string | number;
  action?: React.ReactNode;
  className?: string;
}) {
  const chosen = useMemo(() => variant ?? pickVariant(seed), [variant, seed]);

  return (
    <div className={cn("flex flex-col items-center justify-center gap-3 px-6 py-12 text-center", className)}>
      <div className="relative">
        <Truck variant={chosen} className="h-14 w-28 text-muted-foreground/50" />
      </div>
      <div className="space-y-1">
        <p className="text-sm font-medium text-foreground">{title}</p>
        {hint && <p className="max-w-xs text-sm text-muted-foreground">{hint}</p>}
      </div>
      {action}
    </div>
  );
}
