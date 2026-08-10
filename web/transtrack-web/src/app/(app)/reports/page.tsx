"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Card, CardContent } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import { shareFile } from "@/lib/share";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Vehicle, Driver, Trip, VehicleMaintenance, LedgerRow, VehicleOwnership } from "@/lib/types";
import { FileDown, FileSpreadsheet } from "lucide-react";

export default function ReportsPage() {
  const [vehicleId, setVehicleId] = useState<string>("");
  const [driverId, setDriverId] = useState<string>("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [ownership, setOwnership] = useState<VehicleOwnership | "">("");

  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });
  const driversQuery = useQuery({ queryKey: ["drivers"], queryFn: () => api.get<Driver[]>("/api/drivers") });

  const params = new URLSearchParams();
  if (vehicleId) params.set("vehicleId", vehicleId);
  if (driverId) params.set("driverId", driverId);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (ownership) params.set("ownership", ownership);
  const qs = params.toString();

  return (
    <div className="space-y-4 p-4 sm:p-6">
      <h1 className="text-xl font-semibold">Reports</h1>

      <Card>
        <CardContent className="grid grid-cols-2 gap-3 p-4 sm:grid-cols-4">
          <div className="space-y-1.5">
            <Label className="text-xs">Vehicle</Label>
            <Select value={vehicleId} onValueChange={(v) => setVehicleId(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="All">
                  {(v: string) => vehiclesQuery.data?.find((x) => x.id === v)?.regNo ?? "All"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {vehiclesQuery.data?.map((v) => <SelectItem key={v.id} value={v.id}>{v.regNo}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs">Driver</Label>
            <Select value={driverId} onValueChange={(v) => setDriverId(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="All">
                  {(v: string) => driversQuery.data?.find((x) => x.id === v)?.name ?? "All"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {driversQuery.data?.map((d) => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs">From</Label>
            <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label className="text-xs">To</Label>
            <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          </div>
          <div className="col-span-2 space-y-1.5 sm:col-span-4">
            <Label className="text-xs">Ownership</Label>
            <Select value={ownership} onValueChange={(v) => setOwnership((v as VehicleOwnership) ?? "")}>
              <SelectTrigger className="w-full sm:w-56">
                <SelectValue placeholder="All vehicles">{(v: string) => (v ? v : "All vehicles")}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="Own">Own fleet only</SelectItem>
                <SelectItem value="Other">Other-owner only</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </CardContent>
      </Card>

      <Tabs defaultValue="trips">
        <TabsList>
          <TabsTrigger value="trips">Trips</TabsTrigger>
          <TabsTrigger value="maintenance">Maintenance</TabsTrigger>
          <TabsTrigger value="ledger">Transactions</TabsTrigger>
        </TabsList>
        <TabsContent value="trips"><TripsReport qs={qs} /></TabsContent>
        <TabsContent value="maintenance"><MaintenanceReport qs={qs} /></TabsContent>
        <TabsContent value="ledger"><LedgerReport qs={qs} /></TabsContent>
      </Tabs>
    </div>
  );
}

function TripsReport({ qs }: { qs: string }) {
  const query = useQuery({
    queryKey: ["reports", "trips", qs],
    queryFn: () => api.get<Trip[]>(`/api/reports/trips?${qs}`),
  });

  const revenueTotal = query.data?.reduce((s, t) => s + t.totalApprovedReceived, 0) ?? 0;
  const balanceTotal = query.data?.reduce((s, t) => s + t.balanceReceivable, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <div className="grid grid-cols-2 gap-3">
        <Totals label="Received" value={formatCurrency(revenueTotal)} />
        <Totals label="Balance outstanding" value={formatCurrency(balanceTotal)} />
      </div>
      <ExportButtons type="trips" qs={qs} />
      <div className="space-y-2">
        {query.data?.map((t) => (
          <Card key={t.id}>
            <CardContent className="p-3 text-sm">
              <p className="font-medium">{t.tripNo} · {t.vehicle?.regNo} · {formatDate(t.date)}</p>
              <p className="text-muted-foreground">{t.fromCity?.name} → {t.toCity?.name} · {t.party?.name}</p>
              <p className="mt-1">{formatCurrency(t.amount)} · Balance {formatCurrency(t.balanceReceivable)} · {t.status}</p>
            </CardContent>
          </Card>
        ))}
        {query.data?.length === 0 && <p className="text-sm text-muted-foreground">No trips match these filters.</p>}
      </div>
    </div>
  );
}

function MaintenanceReport({ qs }: { qs: string }) {
  const query = useQuery({
    queryKey: ["reports", "maintenance", qs],
    queryFn: () => api.get<VehicleMaintenance[]>(`/api/reports/maintenance?${qs}`),
  });

  const total = query.data?.reduce((s, m) => s + m.amount, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <Totals label="Total spent" value={formatCurrency(total)} />
      <ExportButtons type="maintenance" qs={qs} />
      <div className="space-y-2">
        {query.data?.map((m) => (
          <Card key={m.id}>
            <CardContent className="p-3 text-sm">
              <p className="font-medium">{m.vehicle?.regNo} · {m.maintenanceCategory?.name} · {formatCurrency(m.amount)}</p>
              <p className="text-muted-foreground">
                {formatDate(m.date)}{m.vendorName ? ` · ${m.vendorName}` : ""}
              </p>
            </CardContent>
          </Card>
        ))}
        {query.data?.length === 0 && <p className="text-sm text-muted-foreground">No maintenance records match these filters.</p>}
      </div>
    </div>
  );
}

function LedgerReport({ qs }: { qs: string }) {
  const query = useQuery({
    queryKey: ["reports", "ledger", qs],
    queryFn: () => api.get<LedgerRow[]>(`/api/reports/ledger?${qs}`),
  });

  const income = query.data?.filter((r) => r.countsInCompanyAccounts && r.kind === "Income")
    .reduce((s, r) => s + r.amount, 0) ?? 0;
  const expense = query.data?.filter((r) => r.countsInCompanyAccounts && r.kind === "Expense")
    .reduce((s, r) => s + r.amount, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <div className="grid grid-cols-3 gap-3">
        <Totals label="Income" value={formatCurrency(income)} />
        <Totals label="Expense" value={formatCurrency(expense)} />
        <Totals label="Net" value={formatCurrency(income - expense)} />
      </div>
      <ExportButtons type="ledger" qs={qs} />
      <div className="space-y-2">
        {query.data?.map((r, i) => (
          <Card key={i}>
            <CardContent className="flex items-center justify-between p-3 text-sm">
              <div>
                <p className="font-medium">{r.tripNo} · {r.vehicleRegNo} · {r.detail}</p>
                <p className="text-muted-foreground">{formatDate(r.date)} · {r.driverName}</p>
              </div>
              <p className={r.kind === "Income" ? "font-semibold text-success" : "font-semibold text-destructive"}>
                {r.kind === "Income" ? "+" : "−"}{formatCurrency(r.amount)}
              </p>
            </CardContent>
          </Card>
        ))}
        {query.data?.length === 0 && <p className="text-sm text-muted-foreground">No transactions match these filters.</p>}
      </div>
    </div>
  );
}

function ExportButtons({ type, qs }: { type: "trips" | "maintenance" | "ledger"; qs: string }) {
  const [busy, setBusy] = useState<"pdf" | "xlsx" | null>(null);

  const download = async (kind: "pdf" | "xlsx") => {
    setBusy(kind);
    try {
      const file = await api.getFile(`/api/reports/${type}/export.${kind}?${qs}`, `${type}-report.${kind}`);
      const outcome = await shareFile(file, { title: `${type} report` });
      if (outcome === "downloaded") toast.success("Report downloaded.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Couldn't export the report.");
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="grid grid-cols-2 gap-3">
      <Button variant="outline" disabled={busy !== null} onClick={() => download("pdf")}>
        <FileDown className="h-4 w-4" /> {busy === "pdf" ? "Preparing…" : "Export PDF"}
      </Button>
      <Button variant="outline" disabled={busy !== null} onClick={() => download("xlsx")}>
        <FileSpreadsheet className="h-4 w-4" /> {busy === "xlsx" ? "Preparing…" : "Export Excel"}
      </Button>
    </div>
  );
}

function Totals({ label, value }: { label: string; value: string }) {
  return (
    <Card>
      <CardContent className="p-3">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="text-lg font-semibold">{value}</p>
      </CardContent>
    </Card>
  );
}
