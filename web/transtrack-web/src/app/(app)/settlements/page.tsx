"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate, toDateInput, today } from "@/lib/format";
import { cn } from "@/lib/utils";
import type { Party, PaymentMode, SettleableTrip } from "@/lib/types";
import { Banknote, Landmark, Smartphone, FileText, Check } from "lucide-react";

const paymentModes: { value: PaymentMode; label: string; icon: typeof Banknote }[] = [
  { value: "Bank", label: "Bank", icon: Landmark },
  { value: "Cash", label: "Cash", icon: Banknote },
  { value: "Upi", label: "UPI", icon: Smartphone },
  { value: "Cheque", label: "Cheque", icon: FileText },
];

/**
 * Settling a month in one go.
 *
 * A party that ran twenty loads pays for them with one transfer, and recording
 * that used to mean opening twenty trips, entering twenty receipts, getting
 * twenty approvals and closing twenty trips. Here you pick the party, tick what
 * the payment covers, and confirm one total.
 *
 * Nothing is closed on this screen. The settlement goes to the Owner as a
 * single approval, and it is approving it that posts the money and closes the
 * trips — the same rule every other receipt follows, applied once instead of
 * twenty times.
 */
export default function SettlementsPage() {
  const queryClient = useQueryClient();

  const [partyId, setPartyId] = useState("");
  const [date, setDate] = useState(today());
  const [paymentMode, setPaymentMode] = useState<PaymentMode>("Bank");
  const [remarks, setRemarks] = useState("");
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [confirming, setConfirming] = useState(false);

  // Which trips to show. Opens on the calendar month just gone, which is what
  // a month-end payment covers; both ends can be cleared for everything owed.
  const [from, setFrom] = useState(firstOfLastMonth());
  const [to, setTo] = useState(today());

  const partiesQuery = useQuery({
    queryKey: ["parties"],
    queryFn: () => api.get<Party[]>("/api/masters/parties"),
  });

  // The period is part of the question, not a refinement of it: a settlement is
  // almost always "the month they have just paid for", and without a range the
  // list is a year of trips to hunt through.
  const tripsQuery = useQuery({
    queryKey: ["settleable", partyId, from, to],
    queryFn: () => {
      const params = new URLSearchParams({ partyId });
      if (from) params.set("from", from);
      if (to) params.set("to", to);
      return api.get<SettleableTrip[]>(`/api/settlements/settleable?${params}`);
    },
    enabled: Boolean(partyId),
  });

  const trips = tripsQuery.data ?? [];
  const chosen = trips.filter((t) => selected.has(t.tripId));
  const total = chosen.reduce((sum, t) => sum + t.balance, 0);
  const allSelected = trips.length > 0 && chosen.length === trips.length;

  const toggle = (tripId: string) =>
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(tripId)) next.delete(tripId);
      else next.add(tripId);
      return next;
    });

  // A tick on a trip the period no longer shows would be settled invisibly, so
  // moving either end clears the selection the same way changing party does.
  const setPeriod = (next: string, which: "from" | "to") => {
    if (which === "from") setFrom(next);
    else setTo(next);
    setSelected(new Set());
  };

  const pickParty = (id: string) => {
    setPartyId(id);
    // A tick against another party's trip would mean nothing, and carrying it
    // over silently is how the wrong trip gets settled.
    setSelected(new Set());
  };

  const mutation = useMutation({
    mutationFn: () =>
      api.post<string>("/api/settlements", {
        partyId,
        date,
        paymentMode,
        tripIds: chosen.map((t) => t.tripId),
        remarks: remarks || null,
      }),
    onSuccess: () => {
      toast.success(`Sent for approval — ${formatCurrency(total)} across ${chosen.length} trips.`);
      setConfirming(false);
      setSelected(new Set());
      setRemarks("");
      queryClient.invalidateQueries({ queryKey: ["settleable", partyId] });
      queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    },
    onError: (err) => {
      setConfirming(false);
      toast.error(err instanceof ApiError ? err.message : "Couldn't record the settlement.");
    },
  });

  const partyName = partiesQuery.data?.find((p) => p.id === partyId)?.name ?? "this party";

  return (
    <>
      <PageHeader title="Bulk Settlement" backTo="/dashboard" />

      <PageContainer className="space-y-4 pb-32">
        <Card>
          <CardContent className="space-y-3 p-4">
            <div className="space-y-1.5">
              <Label className="text-xs">Party</Label>
              <SearchablePicker
                label="Party"
                value={partyId}
                onSelect={pickParty}
                options={(partiesQuery.data ?? []).map((p) => ({
                  id: p.id,
                  label: p.name,
                  sublabel: p.phone ?? undefined,
                }))}
              />
            </div>

            {/* Which trips the payment covers, not when it arrived — the two
                are different dates and get their own fields. */}
            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label htmlFor="tripsFrom" className="text-xs">Trips from</Label>
                <Input id="tripsFrom" type="date" value={from} onChange={(e) => setPeriod(e.target.value, "from")} />
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="tripsTo" className="text-xs">Trips to</Label>
                <Input id="tripsTo" type="date" value={to} onChange={(e) => setPeriod(e.target.value, "to")} />
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label className="text-xs">Payment date</Label>
                <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs">How was it paid?</Label>
                <div className="grid grid-cols-4 gap-1">
                  {paymentModes.map((m) => (
                    <button
                      key={m.value}
                      type="button"
                      aria-label={m.label}
                      onClick={() => setPaymentMode(m.value)}
                      className={cn(
                        "flex h-10 items-center justify-center rounded-lg border-2 transition",
                        paymentMode === m.value
                          ? "border-primary bg-accent text-accent-foreground"
                          : "border-border bg-card hover:bg-accent/50",
                      )}
                    >
                      <m.icon className="h-4 w-4" />
                    </button>
                  ))}
                </div>
              </div>
            </div>
          </CardContent>
        </Card>

        {!partyId && (
          <p className="text-sm text-muted-foreground">
            Choose a party to see everything they still owe.
          </p>
        )}

        {partyId && trips.length === 0 && !tripsQuery.isLoading && (
          <p className="text-sm text-muted-foreground">
            {partyName} has no open trips with anything outstanding.
          </p>
        )}

        {trips.length > 0 && (
          <>
            <div className="flex items-center justify-between gap-3">
              <p className="text-sm text-muted-foreground">
                {trips.length} open trip{trips.length === 1 ? "" : "s"}
              </p>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setSelected(allSelected ? new Set() : new Set(trips.map((t) => t.tripId)))}
              >
                {allSelected ? "Clear all" : "Select all"}
              </Button>
            </div>

            <div className="space-y-2">
              {trips.map((t) => {
                const isSelected = selected.has(t.tripId);
                return (
                  <button
                    key={t.tripId}
                    type="button"
                    onClick={() => toggle(t.tripId)}
                    aria-pressed={isSelected}
                    className={cn(
                      "flex w-full items-center gap-3 rounded-2xl border-2 p-3 text-left transition active:scale-[0.99]",
                      isSelected ? "border-primary bg-accent/40" : "border-border bg-card hover:bg-accent/20",
                    )}
                  >
                    <span
                      aria-hidden="true"
                      className={cn(
                        "flex h-6 w-6 shrink-0 items-center justify-center rounded-md border-2",
                        isSelected ? "border-primary bg-primary text-primary-foreground" : "border-muted-foreground/40",
                      )}
                    >
                      {isSelected && <Check className="h-4 w-4" />}
                    </span>

                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {t.tripNo} · {t.vehicleRegNo}
                        {t.lrNo ? ` · LR ${t.lrNo}` : ""}
                      </p>
                      <p className="truncate text-xs text-muted-foreground">
                        {formatDate(t.date)} · billed {formatCurrency(t.grandTotal)}
                        {t.received > 0 ? ` · paid ${formatCurrency(t.received)}` : ""}
                      </p>
                    </div>

                    <p className="shrink-0 text-sm font-semibold tabular-nums">{formatCurrency(t.balance)}</p>
                  </button>
                );
              })}
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs">Remarks (optional)</Label>
              <Textarea
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
                placeholder="Cheque number, UTR, whatever you'll want to find this by"
              />
            </div>
          </>
        )}
      </PageContainer>

      {/* The running total, pinned above the tab bar. The figure being agreed
          has to be on screen while the ticking happens, not only in the
          confirmation that follows it. */}
      {chosen.length > 0 && (
        <div className="pb-safe fixed inset-x-0 bottom-[calc(4.5rem+env(safe-area-inset-bottom))] z-40 border-t bg-background/95 px-4 py-3 backdrop-blur md:bottom-0">
          <div className="mx-auto flex max-w-5xl items-center gap-3">
            <div className="min-w-0 flex-1">
              <p className="text-xs text-muted-foreground">
                {chosen.length} trip{chosen.length === 1 ? "" : "s"} selected
              </p>
              <p className="text-xl font-semibold tabular-nums">{formatCurrency(total)}</p>
            </div>
            <Button className="h-12 shrink-0" onClick={() => setConfirming(true)}>
              Settle &amp; close
            </Button>
          </div>
        </div>
      )}

      {/* Asked before anything is sent, and stating the figure in full: this
          is the number the owner will be approving, and the last point at
          which a mis-tick is cheap to fix. */}
      <Dialog open={confirming} onOpenChange={(open) => !open && setConfirming(false)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Settle {chosen.length} trip{chosen.length === 1 ? "" : "s"}?</DialogTitle>
          </DialogHeader>

          <div className="space-y-3">
            <div className="rounded-2xl bg-muted/50 p-4 text-center">
              <p className="text-xs text-muted-foreground">Total received from {partyName}</p>
              <p className="text-3xl font-semibold tabular-nums">{formatCurrency(total)}</p>
            </div>

            <p className="text-sm text-muted-foreground">
              This records the payment against each trip and sends the whole settlement to the
              owner as one approval. The trips close when it is approved — nothing changes until
              then.
            </p>

            <div className="max-h-40 space-y-1 overflow-y-auto rounded-lg border p-2">
              {chosen.map((t) => (
                <div key={t.tripId} className="flex justify-between gap-3 text-xs">
                  <span className="truncate text-muted-foreground">{t.tripNo} · {t.vehicleRegNo}</span>
                  <span className="shrink-0 tabular-nums">{formatCurrency(t.balance)}</span>
                </div>
              ))}
            </div>
          </div>

          <DialogFooter>
            <Button
              className="h-12 w-full"
              disabled={mutation.isPending}
              onClick={() => mutation.mutate()}
            >
              {mutation.isPending ? "Sending…" : `Confirm ${formatCurrency(total)}`}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

/** The first of last month — a month-end payment almost always covers the
 *  month that has just finished, so that is the range the screen opens on. */
function firstOfLastMonth() {
  const now = new Date();
  return toDateInput(new Date(now.getFullYear(), now.getMonth() - 1, 1));
}
