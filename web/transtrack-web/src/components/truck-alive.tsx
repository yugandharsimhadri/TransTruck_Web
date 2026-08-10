"use client";

import { useEffect, useRef, useState } from "react";
import { useIsFetching, useIsMutating } from "@tanstack/react-query";
import { Truck, truckVariants } from "@/components/trucks";

/**
 * The app's heartbeat: a truck driving slowly along the very bottom of the
 * screen, always there, saying "we're running".
 *
 * It ambles quietly by default and picks up pace whenever a request is
 * actually in flight, so the same strip doubles as the global busy
 * indicator — no spinner overlay, no layout shift, nothing to dismiss.
 *
 * A fresh truck is chosen each lap so the fleet rotates rather than one
 * vehicle looping forever.
 *
 * Sits above the mobile tab bar (and the home indicator) so it never
 * collides with navigation, and disappears entirely for anyone who has
 * asked for reduced motion — it carries no information a user could miss.
 */
export function TruckAlive() {
  const isFetching = useIsFetching();
  const isMutating = useIsMutating();
  const busy = isFetching + isMutating > 0;

  const [lap, setLap] = useState(0);
  const [reduced, setReduced] = useState(false);
  const laneRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const query = window.matchMedia("(prefers-reduced-motion: reduce)");
    const update = () => setReduced(query.matches);
    update();
    query.addEventListener("change", update);
    return () => query.removeEventListener("change", update);
  }, []);

  // Swap in the next truck at the end of each lap, off-screen, so the change
  // is never visible mid-drive.
  useEffect(() => {
    const runner = laneRef.current?.querySelector(".truck-runner");
    if (!runner) return;
    const onIteration = () => setLap((n) => n + 1);
    runner.addEventListener("animationiteration", onIteration);
    return () => runner.removeEventListener("animationiteration", onIteration);
  }, []);

  if (reduced) return null;

  const variant = truckVariants[lap % truckVariants.length];

  return (
    <div
      ref={laneRef}
      className="truck-lane pointer-events-none fixed inset-x-0 bottom-[calc(4.75rem+env(safe-area-inset-bottom))] z-20 h-7 md:bottom-0 md:h-8"
      style={{ ["--truck-duration" as string]: busy ? "9s" : "26s" }}
      aria-hidden="true"
    >
      <div className="truck-runner">
        <div className="truck-body">
          <Truck
            variant={variant}
            className={
              busy
                ? "h-6 w-12 text-primary/70 transition-colors duration-500"
                : "h-6 w-12 text-muted-foreground/30 transition-colors duration-500"
            }
          />
        </div>
      </div>
    </div>
  );
}
