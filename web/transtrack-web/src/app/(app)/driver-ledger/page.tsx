"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PageContainer } from "@/components/shell/page-container";
import { Sheet, SheetTrigger, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Driver, DriverLedgerEntry, DriverLedgerEntryType } from "@/lib/types";
import { Plus, Trash2, Wallet, HandCoins, MinusCircle } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";
import { cn } from "@/lib/utils";

const empty = "00000000-0000-0000-0000-000000000000";

const entryTypes: { value: DriverLedgerEntryType; label: string; icon: typeof Wallet }[] = [
  { value: "SalaryPaid", label: "Salary paid", icon: Wallet },
  { value: "AdvanceGiven", label: "Advance given", icon: HandCoins },
  { value: "Deduction", label: "Deduction", icon: MinusCircle },
];

export default function DriverLedgerPage() {
  const [driverId, setDriverId] = useState("");
  const [open, setOpen] = useState(false);
  const queryClient = useQueryClient();

  const driversQuery = useQuery({ queryKey: ["drivers"], queryFn: () => api.get<Driver[]>("/api/drivers") });

  const entriesQuery = useQuery({
    queryKey: ["driver-ledger", driverId],
    queryFn: () => api.get<DriverLedgerEntry[]>(`/api/driver-ledger/driver/${driverId}`),
    enabled: !!driverId,
  });

  const outstandingQuery = useQuery({
    queryKey: ["driver-ledger", driverId, "outstanding"],
    queryFn: () => api.get<number>(`/api/driver-ledger/driver/${driverId}/advance-outstanding`),
    enabled: !!driverId,
  });

  return (
    <PageContainer className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Driver Ledger</h1>
        {driverId && (
          <Sheet open={open} onOpenChange={setOpen}>
            <SheetTrigger className="flex h-9 items-center gap-1.5 rounded-full bg-primary px-4 text-sm font-medium text-primary-foreground">
              <Plus className="h-4 w-4" /> Add
            </SheetTrigger>
            <SheetContent side="bottom" className="rounded-t-3xl pb-[calc(2rem+env(safe-area-inset-bottom))]">
              <SheetHeader>
                <SheetTitle>Add ledger entry</SheetTitle>
              </SheetHeader>
              <AddLedgerEntryForm
                driverId={driverId}
                onSaved={() => {
                  setOpen(false);
                  queryClient.invalidateQueries({ queryKey: ["driver-ledger", driverId] });
                }}
              />
            </SheetContent>
          </Sheet>
        )}
      </div>

      <div className="space-y-2">
        <Label className="text-base">Driver</Label>
        <Select value={driverId} onValueChange={(v) => setDriverId(v ?? "")}>
          <SelectTrigger className="h-12 w-full text-base">
            <SelectValue placeholder="Choose a driver">
              {(v: string) => driversQuery.data?.find((x) => x.id === v)?.display ?? "Choose a driver"}
            </SelectValue>
          </SelectTrigger>
          <SelectContent>
            {driversQuery.data?.map((d) => <SelectItem key={d.id} value={d.id}>{d.display}</SelectItem>)}
          </SelectContent>
        </Select>
      </div>

      {driverId && (
        <>
          <Card>
            <CardContent className="flex items-center justify-between p-4">
              <span className="text-sm text-muted-foreground">Advance outstanding</span>
              <span className="text-lg font-semibold">{formatCurrency(outstandingQuery.data ?? 0)}</span>
            </CardContent>
          </Card>

          <div className="space-y-2">
            {entriesQuery.data?.map((e) => (
              <LedgerEntryRow key={e.id} driverId={driverId} entry={e} />
            ))}
            {entriesQuery.data?.length === 0 && (
              <TruckEmpty
                variant="lorry"
                title="No ledger entries yet"
                hint="Salary, advances and deductions for this driver will appear here."
              />
            )}
          </div>
        </>
      )}
    </PageContainer>
  );
}

function LedgerEntryRow({ driverId, entry }: { driverId: string; entry: DriverLedgerEntry }) {
  const queryClient = useQueryClient();
  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/driver-ledger/${entry.id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["driver-ledger", driverId] });
      queryClient.invalidateQueries({ queryKey: ["driver-ledger", driverId, "outstanding"] });
    },
  });

  const meta = entryTypes.find((t) => t.value === entry.type)!;

  return (
    <div className="flex items-center gap-3 rounded-2xl border p-3 text-sm">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
        <meta.icon className="h-4 w-4" />
      </div>
      <div className="flex-1">
        <p className="font-medium">{formatCurrency(entry.amount)}</p>
        <p className="text-xs text-muted-foreground">
          {formatDate(entry.date)}
          {entry.forMonth ? ` · ${entry.forMonth}` : ""}
        </p>
      </div>
      <Badge
        variant={entry.type === "SalaryPaid" ? "success" : entry.type === "AdvanceGiven" ? "warning" : "destructive"}
        className={cn("shrink-0")}
      >
        {meta.label}
      </Badge>
      <Button variant="ghost" size="icon" onClick={() => deleteMutation.mutate()} disabled={deleteMutation.isPending}>
        <Trash2 className="h-4 w-4 text-destructive" />
      </Button>
    </div>
  );
}

function AddLedgerEntryForm({ driverId, onSaved }: { driverId: string; onSaved: () => void }) {
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [type, setType] = useState<DriverLedgerEntryType>("SalaryPaid");
  const [amount, setAmount] = useState("");
  const [forMonth, setForMonth] = useState(new Date().toISOString().slice(0, 7));
  const [remarks, setRemarks] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/driver-ledger", {
        id: empty,
        driverId,
        date,
        type,
        amount: Number(amount) || 0,
        forMonth: type === "SalaryPaid" ? forMonth : null,
        remarks: remarks || null,
      }),
    onSuccess: () => {
      toast.success("Ledger entry added.");
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
        <Label>Type</Label>
        <div className="grid grid-cols-3 gap-2">
          {entryTypes.map((t) => (
            <button
              key={t.value}
              type="button"
              onClick={() => setType(t.value)}
              className={cn(
                "flex flex-col items-center gap-1.5 rounded-2xl border-2 p-3 text-center transition",
                type === t.value ? "border-primary bg-accent text-accent-foreground" : "border-border bg-card hover:bg-accent/50",
              )}
            >
              <t.icon className="h-5 w-5" />
              <span className="text-xs font-medium">{t.label}</span>
            </button>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <Label>Date</Label>
        <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} className="h-12 text-base" />
      </div>

      <div className="space-y-2">
        <Label>Amount</Label>
        <Input
          type="number"
          inputMode="decimal"
          value={amount}
          onChange={(e) => setAmount(e.target.value)}
          className="h-14 text-2xl font-semibold"
          required
        />
      </div>

      {type === "SalaryPaid" && (
        <div className="space-y-2">
          <Label>For month</Label>
          <Input type="month" value={forMonth} onChange={(e) => setForMonth(e.target.value)} className="h-12 text-base" />
        </div>
      )}

      <div className="space-y-2">
        <Label>Remarks</Label>
        <Textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} />
      </div>

      {error && <p className="text-sm font-medium text-destructive">{error}</p>}

      <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending || !amount}>
        {mutation.isPending ? "Adding…" : "Add entry"}
      </Button>
    </form>
  );
}
