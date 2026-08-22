"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import { DocumentPanel } from "@/components/masters/document-panel";
import { VEHICLE_DOCUMENT_TYPES, type Vehicle, type VehicleOwnership } from "@/lib/types";
import { Plus, Pencil } from "lucide-react";

export function VehiclesTab() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Vehicle | "new" | null>(null);

  const vehiclesQuery = useQuery({
    queryKey: ["vehicles"],
    queryFn: () => api.get<Vehicle[]>("/api/vehicles"),
  });

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setEditing("new")}>
          <Plus className="h-4 w-4" /> Add vehicle
        </Button>
      </div>

      {/* Cards on a phone, table on a wide screen. A five-column table is
          419px at its narrowest, so on a 320px screen a quarter of it sat off
          the edge behind a sideways scroll — while every other list in the app
          is a stacked card. The table earns its place above md, where the
          density genuinely helps scanning a long fleet. */}
      <div className="space-y-2 md:hidden">
        {vehiclesQuery.data?.map((v) => (
          <button
            key={v.id}
            type="button"
            onClick={() => setEditing(v)}
            className="flex w-full items-center gap-3 rounded-2xl border p-3 text-left transition active:scale-[0.99]"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">{v.regNo}</p>
              <p className="truncate text-xs text-muted-foreground">
                {v.vehicleType ?? "No type"} · {v.ownership === "Own" ? "Own" : v.owner?.name ?? "Other owner"}
              </p>
            </div>
            <Badge variant={v.isActive ? "success" : "secondary"} className="shrink-0">
              {v.isActive ? "Active" : "Inactive"}
            </Badge>
            <span
              aria-hidden="true"
              className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground"
            >
              <Pencil className="h-4 w-4" />
            </span>
          </button>
        ))}
        {vehiclesQuery.data?.length === 0 && (
          <p className="py-4 text-sm text-muted-foreground">No vehicles yet.</p>
        )}
      </div>

      <div className="hidden overflow-x-auto rounded-lg border md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Reg No</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Ownership</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {vehiclesQuery.data?.map((v) => (
              <TableRow key={v.id} className="cursor-pointer" onClick={() => setEditing(v)}>
                <TableCell className="font-medium">{v.regNo}</TableCell>
                <TableCell>{v.vehicleType ?? "—"}</TableCell>
                <TableCell>{v.ownership === "Own" ? "Own" : v.owner?.name ?? "Other"}</TableCell>
                <TableCell>
                  <Badge variant={v.isActive ? "success" : "secondary"}>{v.isActive ? "Active" : "Inactive"}</Badge>
                </TableCell>
                <TableCell><Pencil className="h-4 w-4 text-muted-foreground" /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {vehiclesQuery.data?.length === 0 && (
          <p className="p-4 text-sm text-muted-foreground">No vehicles yet.</p>
        )}
      </div>

      <VehicleDialog
        vehicle={editing}
        onClose={() => setEditing(null)}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["vehicles"] })}
        onSavedNew={async (id) => {
          // Refetch first, then point the dialog at the real saved row, so the
          // form is editing the vehicle that now exists rather than "new".
          const list = await queryClient.fetchQuery({
            queryKey: ["vehicles"],
            queryFn: () => api.get<Vehicle[]>("/api/vehicles"),
          });

          const saved = list.find((v) => v.id === id);
          if (saved) setEditing(saved);
        }}
      />
    </div>
  );
}

function VehicleDialog({
  vehicle,
  onClose,
  onSaved,
  onSavedNew,
}: {
  vehicle: Vehicle | "new" | null;
  onClose: () => void;
  onSaved: () => void;
  /** Hands the parent the id of a just-created vehicle so it can keep the
   *  dialog open on it, which is what makes the document upload reachable
   *  without closing and reopening the form. */
  onSavedNew: (id: string) => void;
}) {
  const isNew = vehicle === "new";
  const existing = isNew ? null : vehicle;

  const [regNo, setRegNo] = useState(existing?.regNo ?? "");
  const [vehicleType, setVehicleType] = useState(existing?.vehicleType ?? "");
  const [capacity, setCapacity] = useState(existing?.capacity?.toString() ?? "");
  const [ownership, setOwnership] = useState<VehicleOwnership>(existing?.ownership ?? "Own");
  const [ownerName, setOwnerName] = useState(existing?.owner?.name ?? "");
  const [ownerPhone, setOwnerPhone] = useState(existing?.owner?.phone ?? "");
  const [permitUpto, setPermitUpto] = useState(existing?.permitUpto?.slice(0, 10) ?? "");
  const [nationalPermitUpto, setNationalPermitUpto] = useState(existing?.nationalPermitUpto?.slice(0, 10) ?? "");
  const [insuranceUpto, setInsuranceUpto] = useState(existing?.insuranceUpto?.slice(0, 10) ?? "");
  const [fitnessUpto, setFitnessUpto] = useState(existing?.fitnessUpto?.slice(0, 10) ?? "");
  const [pollutionUpto, setPollutionUpto] = useState(existing?.pollutionUpto?.slice(0, 10) ?? "");
  const [error, setError] = useState("");

  // Re-seed local state whenever a different vehicle (or "new") is opened.
  const [openFor, setOpenFor] = useState(vehicle);
  if (openFor !== vehicle) {
    setOpenFor(vehicle);
    setRegNo(existing?.regNo ?? "");
    setVehicleType(existing?.vehicleType ?? "");
    setCapacity(existing?.capacity?.toString() ?? "");
    setOwnership(existing?.ownership ?? "Own");
    setOwnerName(existing?.owner?.name ?? "");
    setOwnerPhone(existing?.owner?.phone ?? "");
    setPermitUpto(existing?.permitUpto?.slice(0, 10) ?? "");
    setNationalPermitUpto(existing?.nationalPermitUpto?.slice(0, 10) ?? "");
    setInsuranceUpto(existing?.insuranceUpto?.slice(0, 10) ?? "");
    setFitnessUpto(existing?.fitnessUpto?.slice(0, 10) ?? "");
    setPollutionUpto(existing?.pollutionUpto?.slice(0, 10) ?? "");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: async () => {
      let ownerId: string | undefined = existing?.ownerId ?? undefined;
      if (ownership === "Other") {
        const result = await api.post<string>("/api/masters/owners/basic", {
          existingOwnerId: ownerId ?? null,
          name: ownerName,
          phone: ownerPhone,
        });
        ownerId = result;
      }

      return await api.post<string>("/api/vehicles", {
        id: existing?.id ?? "00000000-0000-0000-0000-000000000000",
        regNo,
        ownership,
        ownerId: ownership === "Other" ? ownerId : null,
        vehicleType: vehicleType || null,
        capacity: capacity ? Number(capacity) : null,
        permitUpto: permitUpto || null,
        nationalPermitUpto: nationalPermitUpto || null,
        insuranceUpto: insuranceUpto || null,
        fitnessUpto: fitnessUpto || null,
        pollutionUpto: pollutionUpto || null,
        isActive: existing?.isActive ?? true,
      });
    },
    onSuccess: (savedId) => {
      onSaved();

      // A document is stored against a vehicle id, so on a brand-new vehicle
      // there was nothing to attach one to and the upload simply wasn't drawn
      // — which read as the feature being missing rather than not-yet-possible.
      // Now the newly saved vehicle takes over the open dialog, so its upload
      // appears straight away instead of forcing a save-close-reopen round trip.
      if (isNew && savedId) {
        toast.success("Vehicle saved — you can attach its document now.");
        onSavedNew(savedId);
        return;
      }

      toast.success("Vehicle saved.");
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={vehicle !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          {/* The null case is not dead: closing sets the value to null while
              the content is still mounted for the close animation, so without a
              fallback the title renders "Edit undefined" on the way out. */}
          <DialogTitle>{isNew ? "Add vehicle" : existing ? `Edit ${existing.regNo}` : "Edit vehicle"}</DialogTitle>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            mutation.mutate();
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="regNo">Registration number</Label>
            <Input id="regNo" value={regNo} onChange={(e) => setRegNo(e.target.value)} required />
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label htmlFor="vehicleType">Type</Label>
              <Input id="vehicleType" value={vehicleType} onChange={(e) => setVehicleType(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label htmlFor="capacity">Capacity</Label>
              <Input id="capacity" type="number" value={capacity} onChange={(e) => setCapacity(e.target.value)} />
            </div>
          </div>
          <div className="space-y-2">
            <Label>Ownership</Label>
            <Select value={ownership} onValueChange={(v) => v && setOwnership(v as VehicleOwnership)}>
              <SelectTrigger className="w-full">
                <SelectValue>{(v: VehicleOwnership) => (v === "Own" ? "Own fleet" : "Other owner")}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Own">Own fleet</SelectItem>
                <SelectItem value="Other">Other owner</SelectItem>
              </SelectContent>
            </Select>
          </div>
          {ownership === "Other" && (
            <div className="grid grid-cols-2 gap-3 rounded-lg border p-3">
              <div className="space-y-2">
                <Label htmlFor="ownerName">Owner name</Label>
                <Input id="ownerName" value={ownerName} onChange={(e) => setOwnerName(e.target.value)} required />
              </div>
              <div className="space-y-2">
                <Label htmlFor="ownerPhone">Owner phone</Label>
                <Input id="ownerPhone" value={ownerPhone} onChange={(e) => setOwnerPhone(e.target.value)} required />
              </div>
            </div>
          )}
          <div className="space-y-2 rounded-lg border p-3">
            <Label className="text-xs text-muted-foreground">Document dates (optional)</Label>
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1">
                <Label htmlFor="permitUpto" className="text-xs">Permit</Label>
                <Input id="permitUpto" type="date" value={permitUpto} onChange={(e) => setPermitUpto(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="nationalPermitUpto" className="text-xs">National permit</Label>
                <Input id="nationalPermitUpto" type="date" value={nationalPermitUpto} onChange={(e) => setNationalPermitUpto(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="insuranceUpto" className="text-xs">Insurance</Label>
                <Input id="insuranceUpto" type="date" value={insuranceUpto} onChange={(e) => setInsuranceUpto(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="fitnessUpto" className="text-xs">Fitness</Label>
                <Input id="fitnessUpto" type="date" value={fitnessUpto} onChange={(e) => setFitnessUpto(e.target.value)} />
              </div>
              <div className="space-y-1">
                <Label htmlFor="pollutionUpto" className="text-xs">Pollution</Label>
                <Input id="pollutionUpto" type="date" value={pollutionUpto} onChange={(e) => setPollutionUpto(e.target.value)} />
              </div>
            </div>
          </div>
          {/* Shown while adding too, greyed out: documents are keyed by
              vehicle id, so there is nothing to attach them to until the row
              exists — but hiding the panel entirely made the feature look
              missing rather than not-yet-available. */}
          <DocumentPanel
            ownerPath="vehicles"
            ownerId={existing?.id ?? null}
            types={VEHICLE_DOCUMENT_TYPES}
            emptyText="No documents uploaded for this vehicle yet."
          />

          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending} className="w-full">
              {mutation.isPending ? "Saving…" : "Save"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
