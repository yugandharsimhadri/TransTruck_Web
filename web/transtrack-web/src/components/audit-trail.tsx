"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";
import { formatDateTime } from "@/lib/format";
import type { AuditEntry, AuditFieldChange } from "@/lib/types";
import { Plus, Pencil, Trash2, History } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * A record's history, rendered as a simple timeline. Deliberately plain: this
 * is something people read when they're checking who changed a number, so it
 * favours "when, who, what" over decoration.
 */

const actionStyles = {
  Created: { icon: Plus, tone: "bg-success/15 text-success", label: "Added" },
  Updated: { icon: Pencil, tone: "bg-warning/15 text-warning", label: "Changed" },
  Deleted: { icon: Trash2, tone: "bg-destructive/15 text-destructive", label: "Deleted" },
} as const;

/** Turns EF's property names into something readable, e.g. VendorName -> Vendor name. */
function fieldLabel(field: string): string {
  const spaced = field.replace(/([a-z])([A-Z])/g, "$1 $2");
  const cleaned = spaced.replace(/ Id$/, "").replace(/^Approval /, "");
  return cleaned.charAt(0).toUpperCase() + cleaned.slice(1).toLowerCase();
}

function parseChanges(raw?: string | null): AuditFieldChange[] {
  if (!raw) return [];
  try {
    return JSON.parse(raw) as AuditFieldChange[];
  } catch {
    // A malformed row must never take the screen down — showing the summary
    // without its detail is far better than an error boundary.
    return [];
  }
}

export function AuditTrail({ entries, emptyText = "No changes recorded yet." }: {
  entries: AuditEntry[];
  emptyText?: string;
}) {
  if (entries.length === 0) {
    return <p className="text-sm text-muted-foreground">{emptyText}</p>;
  }

  return (
    <ol className="space-y-3">
      {entries.map((entry) => {
        const style = actionStyles[entry.action] ?? actionStyles.Updated;
        const Icon = style.icon;
        const changes = parseChanges(entry.changes);

        return (
          <li key={entry.id} className="flex gap-3">
            <div className={cn("mt-0.5 flex h-8 w-8 shrink-0 items-center justify-center rounded-full", style.tone)}>
              <Icon className="h-4 w-4" />
            </div>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium">{entry.summary}</p>
              <p className="text-xs text-muted-foreground">
                {entry.changedBy} · {formatDateTime(entry.changedOn)}
              </p>
              {changes.length > 0 && (
                <ul className="mt-1.5 space-y-0.5">
                  {changes.map((change) => (
                    <li key={change.field} className="text-xs text-muted-foreground">
                      <span className="font-medium text-foreground/80">{fieldLabel(change.field)}</span>{" "}
                      <span className="line-through opacity-70">{change.from ?? "—"}</span>
                      {" → "}
                      <span className="font-medium text-foreground">{change.to ?? "—"}</span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          </li>
        );
      })}
    </ol>
  );
}

/** The history of one trip — everything that happened to it and to its
 *  expenses and amounts, in one list. */
export function TripAuditTrail({ tripId }: { tripId: string }) {
  const query = useQuery({
    queryKey: ["audit", "trip", tripId],
    queryFn: () => api.get<AuditEntry[]>(`/api/audit/trip/${tripId}`),
  });

  if (query.isLoading) {
    return <p className="text-sm text-muted-foreground">Loading history…</p>;
  }

  return (
    <AuditTrail
      entries={query.data ?? []}
      emptyText="Nothing has changed on this trip yet."
    />
  );
}

export { History as HistoryIcon };
