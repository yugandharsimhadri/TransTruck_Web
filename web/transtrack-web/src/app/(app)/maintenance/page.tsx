"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Sheet, SheetTrigger, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Vehicle, VehicleMaintenance, MaintenanceCategory } from "@/lib/types";
import { Plus, Wrench, Trash2 } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";

const empty = "00000000-0000-0000-0000-000000000000";

export default function MaintenancePage() {
  const [vehicleId, setVehicleId] = useState("");
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();

  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });

  const recordsQuery = useQuery({
    queryKey: ["maintenance", vehicleId],
    queryFn: () => api.get<VehicleMaintenance[]>(`/api/maintenance/vehicle/${vehicleId}`),
    enabled: !!vehicleId,
  });

  const totalAmount = recordsQuery.data?.reduce((sum, r) => sum + r.amount, 0) ?? 0;

  return (
    <div className="space-y-4 p-4 sm:p-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Maintenance</h1>
        {vehicleId && (
          <Sheet open={open} onOpenChange={setOpen}>
            <SheetTrigger
              className="flex h-9 items-center gap-1.5 rounded-full bg-primary px-4 text-sm font-medium text-primary-foreground"
            >
              <Plus className="h-4 w-4" /> Add
            </SheetTrigger>
            <SheetContent side="bottom" className="rounded-t-3xl pb-[calc(2rem+env(safe-area-inset-bottom))]">
              <SheetHeader>
                <SheetTitle>Add maintenance record</SheetTitle>
              </SheetHeader>
              <AddMaintenanceForm
                vehicleId={vehicleId}
                onSaved={() => {
                  setOpen(false);
                  queryClient.invalidateQueries({ queryKey: ["maintenance", vehicleId] });
                }}
              />
            </SheetContent>
          </Sheet>
        )}
      </div>

      <div className="space-y-2">
        <Label className="text-base">Vehicle</Label>
        <Select value={vehicleId} onValueChange={(v) => setVehicleId(v ?? "")}>
          <SelectTrigger className="h-12 w-full text-base">
            <SelectValue placeholder="Choose a vehicle">
              {(v: string) => vehiclesQuery.data?.find((x) => x.id === v)?.display ?? "Choose a vehicle"}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {vehiclesQuery.data?.map((v) => (
              <SelectItem key={v.id} value={v.id}>{v.display}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>

      {vehicleId && (
        <>
          <Card>
            <CardContent className="flex items-center justify-between p-4">
              <span className="text-sm text-muted-foreground">Total spent</span>
              <span className="text-lg font-semibold">{formatCurrency(totalAmount)}</span>
            </CardContent>
          </Card>

          <div className="space-y-2">
            {recordsQuery.data?.map((r) => (
              <MaintenanceRow key={r.id} vehicleId={vehicleId} record={r} />
            ))}
            {recordsQuery.data?.length === 0 && (
              <TruckEmpty
                variant="pickup"
                title="No service records yet"
                hint="Tap Add to log the first service for this vehicle."
              />
            )}
          </div>
        </>
      )}
    </div>
  );
}

function MaintenanceRow({ vehicleId, record }: { vehicleId: string; record: VehicleMaintenance }) {
  const queryClient = useQueryClient();
  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/maintenance/${record.id}`),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["maintenance", vehicleId] }),
  });

  return (
    <div className="flex items-center gap-3 rounded-2xl border p-3 text-sm">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
        <Wrench className="h-4 w-4" />
      </div>
      <div className="flex-1">
        <p className="font-medium">{record.maintenanceCategory?.name} · {formatCurrency(record.amount)}</p>
        <p className="text-xs text-muted-foreground">
          {formatDate(record.date)}
          {record.vendorName ? ` · ${record.vendorName}` : ""}
          {record.odometerReading ? ` · ${record.odometerReading} km` : ""}
        </p>
        {record.nextDueDate && (
          <p className="text-xs text-muted-foreground">Next due: {formatDate(record.nextDueDate)}</p>
        )}
      </div>
      <Button variant="ghost" size="icon" onClick={() => deleteMutation.mutate()} disabled={deleteMutation.isPending}>
        <Trash2 className="h-4 w-4 text-destructive" />
      </Button>
    </div>
  );
}

function AddMaintenanceForm({ vehicleId, onSaved }: { vehicleId: string; onSaved: () => void }) {
  const categoriesQuery = useQuery({
    queryKey: ["maintenance-categories"],
    queryFn: () => api.get<MaintenanceCategory[]>("/api/masters/maintenance-categories"),
  });

  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [categoryId, setCategoryId] = useState("");
  const [odometerReading, setOdometerReading] = useState("");
  const [vendorName, setVendorName] = useState("");
  const [amount, setAmount] = useState("");
  const [nextDueDate, setNextDueDate] = useState("");
  const [nextDueOdometer, setNextDueOdometer] = useState("");
  const [remarks, setRemarks] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/maintenance", {
        id: empty,
        vehicleId,
        date,
        maintenanceCategoryId: categoryId,
        odometerReading: odometerReading ? Number(odometerReading) : null,
        vendorName: vendorName || null,
        amount: Number(amount) || 0,
        nextDueDate: nextDueDate || null,
        nextDueOdometer: nextDueOdometer ? Number(nextDueOdometer) : null,
        remarks: remarks || null,
      }),
    onSuccess: () => {
      toast.success("Maintenance record added.");
      onSaved();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <form
      className="space-y-4 px-4"
      onSubmit={(e) => {
        e.preventDefault();
        mutation.mutate();
      }}
    >
      <div className="space-y-2">
        <Label>Date</Label>
        <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} className="h-12 text-base" />
      </div>
      <div className="space-y-2">
        <Label>Category</Label>
        <Select value={categoryId} onValueChange={(v) => setCategoryId(v ?? "")}>
          <SelectTrigger className="h-12 w-full text-base">
            <SelectValue placeholder="Choose">
              {(v: string) => categoriesQuery.data?.find((x) => x.id === v)?.name ?? "Choose"}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {categoriesQuery.data?.map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label>Amount</Label>
          <Input type="number" inputMode="decimal" value={amount} onChange={(e) => setAmount(e.target.value)} required className="h-12 text-base" />
        </div>
        <div className="space-y-2">
          <Label>Odometer (km)</Label>
          <Input type="number" value={odometerReading} onChange={(e) => setOdometerReading(e.target.value)} className="h-12 text-base" />
        </div>
      </div>
      <div className="space-y-2">
        <Label>Vendor</Label>
        <Input value={vendorName} onChange={(e) => setVendorName(e.target.value)} className="h-12 text-base" />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label>Next due date</Label>
          <Input type="date" value={nextDueDate} onChange={(e) => setNextDueDate(e.target.value)} className="h-12 text-base" />
        </div>
        <div className="space-y-2">
          <Label>Next due (km)</Label>
          <Input type="number" value={nextDueOdometer} onChange={(e) => setNextDueOdometer(e.target.value)} className="h-12 text-base" />
        </div>
      </div>
      <div className="space-y-2">
        <Label>Remarks</Label>
        <Textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} />
      </div>
      {error && <p className="text-sm font-medium text-destructive">{error}</p>}
      <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending || !categoryId || !amount}>
        {mutation.isPending ? "Adding…" : "Add record"}
      </Button>
    </form>
  );
}
