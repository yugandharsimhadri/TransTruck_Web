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
import { PageContainer } from "@/components/shell/page-container";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import { shareFile } from "@/lib/share";
import { formatCurrency, formatDate } from "@/lib/format";
import type {
  Vehicle,
  Driver,
  Party,
  Trip,
  VehicleMaintenance,
  LedgerRow,
  PartyReport,
  VehicleMonthlySaving,
  VehicleOwnership,
} from "@/lib/types";
import { FileDown, FileSpreadsheet } from "lucide-react";

export default function ReportsPage() {
  const [vehicleId, setVehicleId] = useState<string>("");
  const [driverId, setDriverId] = useState<string>("");
  const [partyId, setPartyId] = useState<string>("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [ownership, setOwnership] = useState<VehicleOwnership | "">("");

  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });
  const driversQuery = useQuery({ queryKey: ["drivers"], queryFn: () => api.get<Driver[]>("/api/drivers") });
  const partiesQuery = useQuery({ queryKey: ["parties"], queryFn: () => api.get<Party[]>("/api/masters/parties") });

  const params = new URLSearchParams();
  if (vehicleId) params.set("vehicleId", vehicleId);
  if (driverId) params.set("driverId", driverId);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (ownership) params.set("ownership", ownership);
  const qs = params.toString();

  // The party report takes its own filter set — a driver or ownership filter
  // means nothing on a statement addressed to one party.
  const partyParams = new URLSearchParams();
  if (partyId) partyParams.set("partyId", partyId);
  if (from) partyParams.set("from", from);
  if (to) partyParams.set("to", to);
  const partyQs = partyParams.toString();

  const savingsParams = new URLSearchParams();
  if (vehicleId) savingsParams.set("vehicleId", vehicleId);
  if (from) savingsParams.set("from", from);
  if (to) savingsParams.set("to", to);
  const savingsQs = savingsParams.toString();

  return (
    <PageContainer className="space-y-4">
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
          <div className="space-y-1.5">
            <Label className="text-xs">Party (party report)</Label>
            <Select value={partyId} onValueChange={(v) => setPartyId(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Choose">
                  {(v: string) => partiesQuery.data?.find((x) => x.id === v)?.name ?? "Choose"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {partiesQuery.data?.map((p) => <SelectItem key={p.id} value={p.id}>{p.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="col-span-2 space-y-1.5 sm:col-span-3">
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
        <TabsList className="flex-wrap">
          <TabsTrigger value="trips">Trips</TabsTrigger>
          <TabsTrigger value="party">Party-wise</TabsTrigger>
          <TabsTrigger value="savings">Vehicle savings</TabsTrigger>
          <TabsTrigger value="maintenance">Maintenance</TabsTrigger>
          <TabsTrigger value="ledger">Transactions</TabsTrigger>
        </TabsList>
        <TabsContent value="trips"><TripsReport qs={qs} /></TabsContent>
        <TabsContent value="party"><PartyWiseReport qs={partyQs} hasParty={partyId !== ""} /></TabsContent>
        <TabsContent value="savings"><VehicleSavingsReport qs={savingsQs} /></TabsContent>
        <TabsContent value="maintenance"><MaintenanceReport qs={qs} /></TabsContent>
        <TabsContent value="ledger"><LedgerReport qs={qs} /></TabsContent>
      </Tabs>
    </PageContainer>
  );
}

function PartyWiseReport({ qs, hasParty }: { qs: string; hasParty: boolean }) {
  const query = useQuery({
    queryKey: ["reports", "party", qs],
    queryFn: () => api.get<PartyReport>(`/api/reports/party?${qs}`),
    // Nothing to ask for until a party is chosen — this report is always
    // about one party, there is no "all parties" version of it.
    enabled: hasParty,
  });

  if (!hasParty) {
    return (
      <p className="pt-4 text-sm text-muted-foreground">
        Choose a party above to build its statement.
      </p>
    );
  }

  return (
    <div className="space-y-3 pt-4">
      {query.data && (
        <div className="rounded-2xl border p-3">
          <p className="font-semibold">{query.data.partyName}</p>
          <p className="text-xs text-muted-foreground">{query.data.periodLabel}</p>
        </div>
      )}
      <Totals label="Total billed" value={formatCurrency(query.data?.total ?? 0)} />
      <ExportButtons type="party" qs={qs} />
      <div className="space-y-2">
        {query.data?.rows.map((r) => (
          <Card key={r.serialNo}>
            <CardContent className="flex items-start justify-between gap-3 p-3 text-sm">
              <div className="min-w-0">
                <p className="font-medium">{r.serialNo}. {r.vehicleRegNo} · {formatDate(r.date)}</p>
                <p className="truncate text-muted-foreground">
                  {r.fromCity} → {r.toCity}
                  {r.weight != null ? ` · ${r.weight} MT` : ""}
                  {r.rate != null ? ` @ ${r.rate}` : ""}
                </p>
              </div>
              <p className="shrink-0 font-semibold">{formatCurrency(r.amount)}</p>
            </CardContent>
          </Card>
        ))}
        {query.data?.rows.length === 0 && (
          <p className="text-sm text-muted-foreground">No trips for this party in the selected period.</p>
        )}
      </div>
    </div>
  );
}

function VehicleSavingsReport({ qs }: { qs: string }) {
  const query = useQuery({
    queryKey: ["reports", "vehicle-savings", qs],
    queryFn: () => api.get<VehicleMonthlySaving[]>(`/api/reports/vehicle-savings?${qs}`),
  });

  const totalSaving = query.data?.reduce((s, r) => s + r.saving, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <Totals label="Total saved" value={formatCurrency(totalSaving)} />
      <ExportButtons type="vehicle-savings" qs={qs} />
      <div className="space-y-2">
        {query.data?.map((r) => (
          <Card key={`${r.vehicleRegNo}-${r.monthLabel}`}>
            <CardContent className="p-3 text-sm">
              <div className="flex items-center justify-between gap-3">
                <p className="font-medium">{r.vehicleRegNo} · {r.monthLabel}</p>
                <p className={r.saving >= 0 ? "font-semibold text-success" : "font-semibold text-destructive"}>
                  {formatCurrency(r.saving)}
                </p>
              </div>
              <p className="text-muted-foreground">
                {r.trips} trip{r.trips === 1 ? "" : "s"} · Revenue {formatCurrency(r.revenue)} · Expenses{" "}
                {formatCurrency(r.tripExpenses)} · Maintenance {formatCurrency(r.maintenanceCost)}
              </p>
              <p className="text-muted-foreground">Saving per trip: {formatCurrency(r.savingPerTrip)}</p>
            </CardContent>
          </Card>
        ))}
        {query.data?.length === 0 && (
          <p className="text-sm text-muted-foreground">No vehicle activity in the selected period.</p>
        )}
      </div>
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
  // The split of Received just above — always sums back to it exactly,
  // since every approved amount on a trip is one or the other.
  const advanceTotal = query.data?.reduce((s, t) => s + t.totalAdvanceReceived, 0) ?? 0;
  const paymentTotal = query.data?.reduce((s, t) => s + t.totalPaymentReceived, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <div className="grid grid-cols-2 gap-3">
        <Totals label="Received" value={formatCurrency(revenueTotal)} />
        <Totals label="Balance outstanding" value={formatCurrency(balanceTotal)} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Totals label="— of which Advance" value={formatCurrency(advanceTotal)} muted />
        <Totals label="— of which Payment" value={formatCurrency(paymentTotal)} muted />
      </div>
      <ExportButtons type="trips" qs={qs} />
      <div className="space-y-2">
        {query.data?.map((t) => (
          <Card key={t.id}>
            <CardContent className="p-3 text-sm">
              <p className="font-medium">{t.tripNo} · {t.vehicle?.regNo} · {formatDate(t.date)}</p>
              <p className="text-muted-foreground">{t.fromCity?.name} → {t.toCity?.name} · {t.party?.name}</p>
              <p className="mt-1">{formatCurrency(t.amount)} · Balance {formatCurrency(t.balanceReceivable)} · {t.status}</p>
              {(t.totalAdvanceReceived > 0 || t.totalPaymentReceived > 0) && (
                <p className="text-xs text-muted-foreground">
                  Advance {formatCurrency(t.totalAdvanceReceived)} · Payment {formatCurrency(t.totalPaymentReceived)}
                </p>
              )}
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
  // The split of Income just above — always sums back to it exactly, since
  // every income row is one or the other.
  const advance = query.data?.filter((r) => r.countsInCompanyAccounts && r.receiptType === "Advance")
    .reduce((s, r) => s + r.amount, 0) ?? 0;
  const payment = query.data?.filter((r) => r.countsInCompanyAccounts && r.receiptType === "Payment")
    .reduce((s, r) => s + r.amount, 0) ?? 0;

  return (
    <div className="space-y-3 pt-4">
      <div className="grid grid-cols-3 gap-3">
        <Totals label="Income" value={formatCurrency(income)} />
        <Totals label="Expense" value={formatCurrency(expense)} />
        <Totals label="Net" value={formatCurrency(income - expense)} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <Totals label="— of which Advance" value={formatCurrency(advance)} muted />
        <Totals label="— of which Payment" value={formatCurrency(payment)} muted />
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

function ExportButtons({
  type,
  qs,
}: {
  type: "trips" | "maintenance" | "ledger" | "party" | "vehicle-savings";
  qs: string;
}) {
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

function Totals({ label, value, muted }: { label: string; value: string; muted?: boolean }) {
  return (
    <Card>
      <CardContent className="p-3">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className={muted ? "text-base font-medium text-muted-foreground" : "text-lg font-semibold"}>{value}</p>
      </CardContent>
    </Card>
  );
}
