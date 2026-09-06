"use client";

import { cn } from "@/lib/utils";
import { Eye, EyeOff } from "lucide-react";

/**
 * "Also show the retired ones."
 *
 * Master lists open on what is in service, because that is what you are
 * choosing from ninety-nine times out of a hundred — a fleet of eight lorries
 * reads as eight, not as eight plus four sold years ago. But retired records
 * are never deleted (their trips, ledgers and papers still refer to them), so
 * there has to be a way back to them: to check one, or to put it back in
 * service.
 *
 * A toggle rather than a filter dropdown: there are only two states, and the
 * one you are in should be readable without opening anything.
 */
export function ShowInactiveToggle({
  value,
  onChange,
  noun,
}: {
  value: boolean;
  onChange: (next: boolean) => void;
  /** Plural, lower case — "drivers", "vehicles", "places". */
  noun: string;
}) {
  const Icon = value ? Eye : EyeOff;

  return (
    <button
      type="button"
      onClick={() => onChange(!value)}
      aria-pressed={value}
      className={cn(
        "flex h-9 items-center gap-1.5 rounded-full border px-3 text-xs font-medium transition",
        value ? "border-primary bg-accent text-accent-foreground" : "text-muted-foreground hover:bg-accent/50",
      )}
    >
      <Icon className="h-3.5 w-3.5" />
      {value ? `Showing retired ${noun}` : `Show retired ${noun}`}
    </button>
  );
}
