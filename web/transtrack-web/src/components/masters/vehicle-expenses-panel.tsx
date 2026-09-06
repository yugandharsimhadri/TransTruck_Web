"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate, today } from "@/lib/format";
import {
  RECURRENCE_LABELS,
  VEHICLE_EXPENSE_KIND_LABELS,
  type ExpenseRecurrence,
  type VehicleExpense,
  type VehicleExpenseKind,
  type VehicleExpenseSchedule,
} from "@/lib/types";
import { Plus, Trash2 } from "lucide-react";

const empty = "00000000-0000-0000-0000-000000000000";

/**
 * A vehicle's paperwork costs — insurance, road tax and the like — set up as
 * a schedule that generates one real, dated entry per period.
 *
 * Shows the schedules rather than the hundreds of rows they can produce: the
 * commitment is what someone came here to change, and the individual
 * instalments are what the dashboard counts. The most recent few entries are
 * listed underneath purely as evidence the schedule did what was expected.
 */
export function VehicleExpensesPanel({ vehicleId }: { vehicleId: string | null }) {
  if (!vehicleId) {
    return (
      <div className="space-y-2 rounded-lg border border-dashed p-3 opacity-70">
        <Label className="text-xs text-muted-foreground">Insurance, road tax &amp; other papers</Label>
        <p className="text-sm text-muted-foreground">
          Save first — then you can set up what this vehicle costs in paperwork.
        </p>
      </div>
    );
  }

  return <Panel vehicleId={vehicleId} />;
}

function Panel({ vehicleId }: { vehicleId: string }) {
  const queryClient = useQueryClient();
  const [adding, setAdding] = useState(false);

  const schedulesKey = ["vehicle-expense-schedules", vehicleId];
  const entriesKey = ["vehicle-expenses", vehicleId];

  const schedulesQuery = useQuery({
    queryKey: schedulesKey,
    queryFn: async () =>
      (await api.get<VehicleExpenseSchedule[]>(`/api/vehicles/${vehicleId}/expense-schedules`)) ?? [],
  });

  const entriesQuery = useQuery({
    queryKey: entriesKey,
    queryFn: async () => (await api.get<VehicleExpense[]>(`/api/vehicles/${vehicleId}/expenses`)) ?? [],
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: schedulesKey });
    queryClient.invalidateQueries({ queryKey: entriesKey });
    // The dashboard's recurring-expenses figure counts these entries.
    queryClient.invalidateQueries({ queryKey: ["dashboard"] });
  };

  const removeMutation = useMutation({
    mutationFn: (id: string) => api.delete(`/api/vehicles/${vehicleId}/expense-schedules/${id}`),
    onSuccess: () => {
      toast.success("Removed.");
      refresh();
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Couldn't remove that."),
  });

  const schedules = schedulesQuery.data ?? [];
  const entries = entriesQuery.data ?? [];

  return (
    <div className="space-y-3 rounded-lg border p-3">
      <div className="flex items-center justify-between gap-2">
        <Label className="text-xs text-muted-foreground">Insurance, road tax &amp; other papers</Label>
        <Button type="button" variant="outline" size="sm" onClick={() => setAdding((a) => !a)}>
          <Plus className="h-4 w-4" /> Add
        </Button>
      </div>

      {adding && (
        <ScheduleForm
          vehicleId={vehicleId}
          onSaved={() => {
            setAdding(false);
            refresh();
          }}
          onCancel={() => setAdding(false)}
        />
      )}

      {schedules.length > 0 ? (
        <ul className="space-y-2">
          {schedules.map((s) => (
            <li key={s.id} className="flex items-center gap-2 rounded-lg bg-muted/50 p-2 text-sm">
              <div className="min-w-0 flex-1">
                <p className="truncate font-medium">
                  {s.description || VEHICLE_EXPENSE_KIND_LABELS[s.kind]} · {formatCurrency(s.amount)}
                </p>
                <p className="truncate text-xs text-muted-foreground">
                  {RECURRENCE_LABELS[s.recurrence]} · from {formatDate(s.startDate)}
                  {s.endDate ? ` to ${formatDate(s.endDate)}` : ""}
                </p>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Remove"
                disabled={removeMutation.isPending}
                onClick={() => removeMutation.mutate(s.id)}
              >
                <Trash2 className="h-4 w-4 text-destructive" />
              </Button>
            </li>
          ))}
        </ul>
      ) : (
        !schedulesQuery.isLoading && !adding && (
          <p className="text-sm text-muted-foreground">
            Nothing set up yet. Add insurance or road tax and each instalment is recorded for you.
          </p>
        )
      )}

      {/* Proof the schedule did what was expected, without listing every
          instalment a long-running one produces. */}
      {entries.length > 0 && (
        <details className="text-sm">
          <summary className="cursor-pointer text-xs text-muted-foreground">
            {entries.length} entr{entries.length === 1 ? "y" : "ies"} recorded
          </summary>
          <ul className="space-y-1 pt-2">
            {entries.slice(0, 12).map((e) => (
              <li key={e.id} className="flex justify-between gap-3 text-xs">
                <span className="truncate text-muted-foreground">
                  {formatDate(e.date)} · {e.description || VEHICLE_EXPENSE_KIND_LABELS[e.kind]}
                </span>
                <span className="shrink-0 tabular-nums">{formatCurrency(e.amount)}</span>
              </li>
            ))}
            {entries.length > 12 && (
              <li className="text-xs text-muted-foreground">…and {entries.length - 12} more</li>
            )}
          </ul>
        </details>
      )}
    </div>
  );
}

function ScheduleForm({
  vehicleId,
  onSaved,
  onCancel,
}: {
  vehicleId: string;
  onSaved: () => void;
  onCancel: () => void;
}) {
  const [kind, setKind] = useState<VehicleExpenseKind>("Insurance");
  const [description, setDescription] = useState("");
  const [amount, setAmount] = useState("");
  const [recurrence, setRecurrence] = useState<ExpenseRecurrence>("Yearly");
  const [startDate, setStartDate] = useState(today());
  const [endDate, setEndDate] = useState("");
  const [error, setError] = useState("");

  const repeats = recurrence !== "None";

  const mutation = useMutation({
    mutationFn: () =>
      api.post(`/api/vehicles/${vehicleId}/expense-schedules`, {
        id: empty,
        vehicleId,
        kind,
        description: description || null,
        amount: Number(amount) || 0,
        recurrence,
        startDate,
        endDate: repeats ? endDate || null : null,
      }),
    onSuccess: () => {
      toast.success("Saved.");
      onSaved();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <form
      className="space-y-3 rounded-lg border bg-card p-3"
      onSubmit={(e) => {
        e.preventDefault();
        setError("");
        mutation.mutate();
      }}
    >
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <Label className="text-xs">What for</Label>
          <select
            className="h-10 w-full rounded-lg border bg-card px-2 text-sm"
            value={kind}
            onChange={(e) => setKind(e.target.value as VehicleExpenseKind)}
          >
            {(Object.keys(VEHICLE_EXPENSE_KIND_LABELS) as VehicleExpenseKind[]).map((k) => (
              <option key={k} value={k}>{VEHICLE_EXPENSE_KIND_LABELS[k]}</option>
            ))}
          </select>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Amount each time</Label>
          <Input type="number" inputMode="decimal" value={amount}
            onChange={(e) => setAmount(e.target.value)} required />
        </div>
      </div>

      {kind === "Other" && (
        <div className="space-y-1">
          <Label className="text-xs">Description</Label>
          <Input value={description} onChange={(e) => setDescription(e.target.value)}
            placeholder="Fitness certificate, permit renewal…" />
        </div>
      )}

      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1">
          <Label className="text-xs">How often</Label>
          <select
            className="h-10 w-full rounded-lg border bg-card px-2 text-sm"
            value={recurrence}
            onChange={(e) => setRecurrence(e.target.value as ExpenseRecurrence)}
          >
            {(Object.keys(RECURRENCE_LABELS) as ExpenseRecurrence[]).map((r) => (
              <option key={r} value={r}>{RECURRENCE_LABELS[r]}</option>
            ))}
          </select>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">{repeats ? "First one on" : "Date"}</Label>
          <Input type="date" value={startDate} onChange={(e) => setStartDate(e.target.value)} required />
        </div>
      </div>

      {/* Only a repeating schedule needs an end — a one-off is its own date,
          and an open-ended repeat would generate entries forever. */}
      {repeats && (
        <div className="space-y-1">
          <Label className="text-xs">Repeat until</Label>
          <Input type="date" value={endDate} onChange={(e) => setEndDate(e.target.value)} required />
          <p className="text-xs text-muted-foreground">
            An entry is recorded {RECURRENCE_LABELS[recurrence].toLowerCase()} from the first date up to this one.
          </p>
        </div>
      )}

      {error && <p className="text-sm font-medium text-destructive">{error}</p>}

      <div className="flex gap-2">
        <Button type="submit" size="sm" disabled={mutation.isPending} className="flex-1">
          {mutation.isPending ? "Saving…" : "Save"}
        </Button>
        <Button type="button" size="sm" variant="ghost" onClick={onCancel}>Cancel</Button>
      </div>
    </form>
  );
}
