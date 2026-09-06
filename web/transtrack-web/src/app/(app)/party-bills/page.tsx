"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api, ApiError } from "@/lib/api";
import { shareFile } from "@/lib/share";
import { formatCurrency, formatDate, today, toDateInput } from "@/lib/format";
import type { Party, PartyReport } from "@/lib/types";
import { FileDown } from "lucide-react";

/**
 * The month-end invoice for one party: every trip run for them over a period,
 * on the same letterhead as the per-trip bill.
 *
 * Its own screen rather than a tab under Reports because it is a document you
 * send someone, not a figure you look up — and the thing you do at the end of
 * it is print it, which is a different intent from reading a report.
 */
export default function PartyBillsPage() {
  const [partyId, setPartyId] = useState("");
  const [from, setFrom] = useState(firstOfThisMonth());
  const [to, setTo] = useState(today());
  const [busy, setBusy] = useState(false);

  const partiesQuery = useQuery({
    queryKey: ["parties"],
    queryFn: () => api.get<Party[]>("/api/masters/parties"),
  });

  const params = new URLSearchParams();
  if (partyId) params.set("partyId", partyId);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const qs = params.toString();

  // Previewed on screen before it is printed — nobody should have to open a
  // PDF to find out the period was wrong.
  const previewQuery = useQuery({
    queryKey: ["party-bill", qs],
    queryFn: () => api.get<PartyReport>(`/api/reports/party?${qs}`),
    enabled: Boolean(partyId),
  });

  const report = previewQuery.data;

  const download = async () => {
    setBusy(true);
    try {
      const file = await api.getFile(`/api/reports/party/bill.pdf?${qs}`, "party-bill.pdf");
      const outcome = await shareFile(file, { title: "Bill" });
      if (outcome === "downloaded") toast.success("Downloaded — open it from your downloads.");
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Couldn't generate the bill.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Party Bills" backTo="/dashboard" />
      <PageContainer className="space-y-4">
        <Card>
          <CardContent className="space-y-3 p-4">
            <div className="space-y-1.5">
              <Label className="text-xs">Party</Label>
              <SearchablePicker
                label="Party"
                value={partyId}
                onSelect={setPartyId}
                options={(partiesQuery.data ?? []).map((p) => ({
                  id: p.id,
                  label: p.name,
                  sublabel: p.isGstEnabled ? `GST ${p.gstPercentage}%` : (p.phone ?? undefined),
                }))}
              />
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label className="text-xs">From</Label>
                <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">To</Label>
                <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} />
              </div>
            </div>
          </CardContent>
        </Card>

        {!partyId && (
          <p className="text-sm text-muted-foreground">Choose a party to see their bill for the period.</p>
        )}

        {report && (
          <>
            <div className="grid grid-cols-2 gap-3">
              <Totals label="Trips" value={report.rows.length.toString()} />
              <Totals label="Bill total" value={formatCurrency(report.grandTotal)} />
            </div>

            {/* The same freight → extras → tax → total the printed bill shows,
                so what is on screen reconciles with what gets sent. */}
            <Card>
              <CardContent className="space-y-1 p-3 text-sm">
                <Line label="Freight" value={report.total} />
                {report.hasWayment && <Line label="Wayment" value={report.totalWayment} />}
                {report.hasLoading && <Line label="Loading" value={report.totalLoading} />}
                {report.hasUnloading && <Line label="Unloading" value={report.totalUnloading} />}
                <Line label="Total before tax" value={report.totalBeforeTax} bold={!report.hasGst} />
                {/* Tax lands on the bill's total, not on each trip, so it sits
                    here rather than in the rows above. */}
                {report.hasGst && (
                  <>
                    <Line label={report.gstLabel} value={report.totalGst} />
                    <Line label="Grand total" value={report.grandTotal} bold />
                  </>
                )}
              </CardContent>
            </Card>

            <Button className="h-12 w-full" disabled={busy || report.rows.length === 0} onClick={download}>
              <FileDown className="h-4 w-4" /> {busy ? "Preparing…" : "Bill — view / share"}
            </Button>

            <div className="space-y-2">
              {report.rows.map((r) => (
                <Card key={r.serialNo}>
                  <CardContent className="flex items-center justify-between gap-3 p-3 text-sm">
                    <div className="min-w-0">
                      <p className="truncate font-medium">
                        {formatDate(r.date)} · {r.vehicleRegNo}
                        {r.lrNo ? ` · LR ${r.lrNo}` : ""}
                      </p>
                      <p className="truncate text-xs text-muted-foreground">
                        {r.fromCity} → {r.toCity}
                        {r.waymentCharge > 0 ? ` · wayment ${formatCurrency(r.waymentCharge)}` : ""}
                        {r.loadingCharge > 0 ? ` · loading ${formatCurrency(r.loadingCharge)}` : ""}
                        {r.unloadingCharge > 0 ? ` · unloading ${formatCurrency(r.unloadingCharge)}` : ""}
                      </p>
                    </div>
                    <p className="shrink-0 font-semibold tabular-nums">{formatCurrency(r.totalBeforeTax)}</p>
                  </CardContent>
                </Card>
              ))}
              {report.rows.length === 0 && (
                <p className="text-sm text-muted-foreground">No trips for this party in that period.</p>
              )}
            </div>
          </>
        )}
      </PageContainer>
    </>
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

function Line({ label, value, bold }: { label: string; value: number; bold?: boolean }) {
  return (
    <div className={`flex items-baseline justify-between gap-3 ${bold ? "font-semibold" : ""}`}>
      <span className={bold ? undefined : "text-muted-foreground"}>{label}</span>
      <span className="tabular-nums">{formatCurrency(value)}</span>
    </div>
  );
}

/** Bills are almost always cut for a calendar month, so that is the range the
 *  screen opens on rather than making someone set both ends every time. */
function firstOfThisMonth() {
  const now = new Date();
  return toDateInput(new Date(now.getFullYear(), now.getMonth(), 1));
}
