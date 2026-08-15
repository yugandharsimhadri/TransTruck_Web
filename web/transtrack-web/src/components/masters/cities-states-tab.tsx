"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { api, ApiError } from "@/lib/api";
import type { City, State } from "@/lib/types";
import { Plus, Pencil } from "lucide-react";

const empty = "00000000-0000-0000-0000-000000000000";

export function CitiesStatesTab() {
  const queryClient = useQueryClient();
  // "new" opens the dialog empty; a row opens it populated for editing.
  const [editingState, setEditingState] = useState<State | "new" | null>(null);
  const [editingCity, setEditingCity] = useState<City | "new" | null>(null);

  const statesQuery = useQuery({ queryKey: ["states"], queryFn: () => api.get<State[]>("/api/masters/states") });
  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => api.get<City[]>("/api/masters/cities") });

  return (
    <div className="grid gap-6 sm:grid-cols-2">
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">States</h3>
          <Button size="sm" variant="outline" onClick={() => setEditingState("new")}>
            <Plus className="h-4 w-4" /> Add
          </Button>
        </div>
        <div className="space-y-1 rounded-lg border p-2">
          {statesQuery.data?.map((s) => (
            <button
              key={s.id}
              type="button"
              onClick={() => setEditingState(s)}
              className="flex min-h-11 w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm transition hover:bg-accent desktop:min-h-0"
            >
              <span className="flex-1 truncate">{s.name}</span>
              <Pencil className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            </button>
          ))}
          {statesQuery.data?.length === 0 && <p className="p-2 text-sm text-muted-foreground">No states yet.</p>}
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Cities</h3>
          <Button size="sm" variant="outline" onClick={() => setEditingCity("new")}>
            <Plus className="h-4 w-4" /> Add
          </Button>
        </div>
        <div className="space-y-1 rounded-lg border p-2">
          {citiesQuery.data?.map((c) => (
            <button
              key={c.id}
              type="button"
              onClick={() => setEditingCity(c)}
              className="flex min-h-11 w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm transition hover:bg-accent desktop:min-h-0"
            >
              <span className="flex-1 truncate">{c.display}</span>
              <Pencil className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
            </button>
          ))}
          {citiesQuery.data?.length === 0 && <p className="p-2 text-sm text-muted-foreground">No cities yet.</p>}
        </div>
      </div>

      <StateDialog
        state={editingState}
        onClose={() => setEditingState(null)}
        onSaved={() => {
          queryClient.invalidateQueries({ queryKey: ["states"] });
          // A renamed state changes how every one of its cities reads.
          queryClient.invalidateQueries({ queryKey: ["cities"] });
        }}
      />
      <CityDialog
        city={editingCity}
        states={statesQuery.data ?? []}
        onClose={() => setEditingCity(null)}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["cities"] })}
      />
    </div>
  );
}

function StateDialog({
  state,
  onClose,
  onSaved,
}: {
  state: State | "new" | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = state === "new";
  const existing = isNew ? null : state;

  const [name, setName] = useState("");
  const [error, setError] = useState("");
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  // Re-seed the fields whenever the dialog is pointed at a different row,
  // rather than on every render — this is the same pattern the other master
  // dialogs use.
  const [openFor, setOpenFor] = useState(state);
  if (openFor !== state) {
    setOpenFor(state);
    setName(existing?.name ?? "");
    setError("");
    setConfirmingDelete(false);
  }

  const saveMutation = useMutation({
    mutationFn: () => api.post("/api/masters/states", { id: existing?.id ?? empty, name }),
    onSuccess: () => {
      toast.success(isNew ? "State added." : "State saved.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/masters/states/${existing!.id}`),
    onSuccess: () => {
      toast.success("State removed.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Couldn't remove that state."),
  });

  return (
    <Dialog open={state !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? "Add state" : `Edit ${existing?.name}`}</DialogTitle>
        </DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}>
          <div className="space-y-2">
            <Label htmlFor="stateName">Name</Label>
            <Input id="stateName" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter className="flex-col gap-2">
            <Button type="submit" disabled={saveMutation.isPending} className="w-full">
              {saveMutation.isPending ? "Saving…" : "Save"}
            </Button>
            {!isNew && (
              <DeleteAction
                label="Remove state"
                question="Remove this state? Cities already using it keep working."
                confirming={confirmingDelete}
                setConfirming={setConfirmingDelete}
                pending={deleteMutation.isPending}
                onConfirm={() => deleteMutation.mutate()}
              />
            )}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CityDialog({
  city,
  states,
  onClose,
  onSaved,
}: {
  city: City | "new" | null;
  states: State[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = city === "new";
  const existing = isNew ? null : city;

  const [name, setName] = useState("");
  const [stateId, setStateId] = useState("");
  const [error, setError] = useState("");
  const [confirmingDelete, setConfirmingDelete] = useState(false);

  const [openFor, setOpenFor] = useState(city);
  if (openFor !== city) {
    setOpenFor(city);
    setName(existing?.name ?? "");
    setStateId(existing?.stateId ?? "");
    setError("");
    setConfirmingDelete(false);
  }

  const saveMutation = useMutation({
    mutationFn: () => api.post("/api/masters/cities", { id: existing?.id ?? empty, name, stateId }),
    onSuccess: () => {
      toast.success(isNew ? "City added." : "City saved.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/masters/cities/${existing!.id}`),
    onSuccess: () => {
      toast.success("City removed.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Couldn't remove that city."),
  });

  return (
    <Dialog open={city !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? "Add city" : `Edit ${existing?.name}`}</DialogTitle>
        </DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }}>
          <div className="space-y-2">
            <Label htmlFor="cityName">Name</Label>
            <Input id="cityName" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
          </div>
          <div className="space-y-2">
            <Label>State</Label>
            <Select value={stateId} onValueChange={(v) => setStateId(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Choose a state">
                  {(v: string) => states.find((x) => x.id === v)?.name ?? "Choose a state"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {states.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter className="flex-col gap-2">
            <Button type="submit" disabled={saveMutation.isPending || !stateId} className="w-full">
              {saveMutation.isPending ? "Saving…" : "Save"}
            </Button>
            {!isNew && (
              <DeleteAction
                label="Remove city"
                question="Remove this city? Trips already using it are untouched and keep showing it."
                confirming={confirmingDelete}
                setConfirming={setConfirmingDelete}
                pending={deleteMutation.isPending}
                onConfirm={() => deleteMutation.mutate()}
              />
            )}
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/// Removal is a soft delete on the server — the row stays put so any trip or
/// maintenance record already pointing at it keeps reading correctly; it just
/// stops being offered for new entries. The wording says so, because
/// "delete" otherwise reads as "this will break my old trips".
function DeleteAction({
  label,
  question,
  confirming,
  setConfirming,
  pending,
  onConfirm,
}: {
  label: string;
  question: string;
  confirming: boolean;
  setConfirming: (value: boolean) => void;
  pending: boolean;
  onConfirm: () => void;
}) {
  if (!confirming) {
    return (
      <Button type="button" variant="ghost" className="w-full text-destructive" onClick={() => setConfirming(true)}>
        {label}
      </Button>
    );
  }

  return (
    <div className="w-full space-y-2 rounded-lg bg-accent p-3">
      <p className="text-xs">{question}</p>
      <div className="flex gap-2">
        <Button type="button" variant="outline" className="flex-1" onClick={() => setConfirming(false)}>
          Keep
        </Button>
        <Button type="button" variant="destructive" className="flex-1" disabled={pending} onClick={onConfirm}>
          {pending ? "Removing…" : "Remove"}
        </Button>
      </div>
    </div>
  );
}
