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
import { Plus } from "lucide-react";

export function CitiesStatesTab() {
  const queryClient = useQueryClient();
  const [addingState, setAddingState] = useState(false);
  const [addingCity, setAddingCity] = useState(false);

  const statesQuery = useQuery({ queryKey: ["states"], queryFn: () => api.get<State[]>("/api/masters/states") });
  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => api.get<City[]>("/api/masters/cities") });

  return (
    <div className="grid gap-6 sm:grid-cols-2">
      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">States</h3>
          <Button size="sm" variant="outline" onClick={() => setAddingState(true)}>
            <Plus className="h-4 w-4" /> Add
          </Button>
        </div>
        <div className="space-y-1 rounded-lg border p-2">
          {statesQuery.data?.map((s) => (
            <div key={s.id} className="rounded px-2 py-1.5 text-sm">{s.name}</div>
          ))}
          {statesQuery.data?.length === 0 && <p className="p-2 text-sm text-muted-foreground">No states yet.</p>}
        </div>
      </div>

      <div className="space-y-3">
        <div className="flex items-center justify-between">
          <h3 className="text-sm font-semibold">Cities</h3>
          <Button size="sm" variant="outline" onClick={() => setAddingCity(true)}>
            <Plus className="h-4 w-4" /> Add
          </Button>
        </div>
        <div className="space-y-1 rounded-lg border p-2">
          {citiesQuery.data?.map((c) => (
            <div key={c.id} className="rounded px-2 py-1.5 text-sm">{c.display}</div>
          ))}
          {citiesQuery.data?.length === 0 && <p className="p-2 text-sm text-muted-foreground">No cities yet.</p>}
        </div>
      </div>

      <AddStateDialog
        open={addingState}
        onOpenChange={setAddingState}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["states"] })}
      />
      <AddCityDialog
        open={addingCity}
        onOpenChange={setAddingCity}
        states={statesQuery.data ?? []}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["cities"] })}
      />
    </div>
  );
}

function AddStateDialog({
  open,
  onOpenChange,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () => api.post("/api/masters/states", { id: "00000000-0000-0000-0000-000000000000", name }),
    onSuccess: () => {
      toast.success("State added.");
      setName("");
      onSaved();
      onOpenChange(false);
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader><DialogTitle>Add state</DialogTitle></DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
          <div className="space-y-2">
            <Label htmlFor="stateName">Name</Label>
            <Input id="stateName" value={name} onChange={(e) => setName(e.target.value)} required autoFocus />
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending} className="w-full">Save</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function AddCityDialog({
  open,
  onOpenChange,
  states,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  states: State[];
  onSaved: () => void;
}) {
  const [name, setName] = useState("");
  const [stateId, setStateId] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/masters/cities", { id: "00000000-0000-0000-0000-000000000000", name, stateId }),
    onSuccess: () => {
      toast.success("City added.");
      setName("");
      setStateId("");
      onSaved();
      onOpenChange(false);
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader><DialogTitle>Add city</DialogTitle></DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
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
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending || !stateId} className="w-full">Save</Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
