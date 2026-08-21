"use client";

import { useEffect, useState } from "react";
import { useParams, useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Trip, Vehicle, Driver, Party, City, State } from "@/lib/types";
import Link from "next/link";
import { ArrowLeft, Trash2, Lock, LockOpen, Share2, FileText, Receipt, Wallet, Plus, ChevronDown } from "lucide-react";
import { shareFile, shareText } from "@/lib/share";
import { iconForCategory } from "@/lib/expense-icons";
import { TripAuditTrail } from "@/components/audit-trail";
import { useAuth } from "@/contexts/auth-context";
import { cn } from "@/lib/utils";

const empty = "00000000-0000-0000-0000-000000000000";

export default function TripDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { user } = useAuth();
  const isNew = params.id === "new";

  // Cancelling a trip and removing an approved amount are both Owner-only,
  // enforced by the API as well — this only decides whether to draw them.
  const isOwner = user?.role === "Owner";

  const tripQuery = useQuery({
    queryKey: ["trips", params.id],
    queryFn: () => api.get<Trip>(`/api/trips/${params.id}`),
    enabled: !isNew,
  });

  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });
  const driversQuery = useQuery({ queryKey: ["drivers"], queryFn: () => api.get<Driver[]>("/api/drivers") });
  const partiesQuery = useQuery({ queryKey: ["parties"], queryFn: () => api.get<Party[]>("/api/masters/parties") });
  const citiesQuery = useQuery({ queryKey: ["cities"], queryFn: () => api.get<City[]>("/api/masters/cities") });
  const statesQuery = useQuery({ queryKey: ["states"], queryFn: () => api.get<State[]>("/api/masters/states") });

  // Quick-add from the trip form: create the record, then select it —
  // reuses the same masters endpoints/validation the Vehicles & Contacts screen uses.
  const [quickAddParty, setQuickAddParty] = useState<string | null>(null);
  const [quickAddCity, setQuickAddCity] = useState<{ text: string; target: "from" | "to" } | null>(null);

  const trip = tripQuery.data;

  // Form state
  const [date, setDate] = useState(new Date().toISOString().slice(0, 10));
  const [vehicleId, setVehicleId] = useState("");
  const [driverId, setDriverId] = useState("");
  const [partyId, setPartyId] = useState("");
  const [fromCityId, setFromCityId] = useState("");
  const [toCityId, setToCityId] = useState("");
  const [consignorName, setConsignorName] = useState("");
  const [consigneeName, setConsigneeName] = useState("");
  const [wayBillNo, setWayBillNo] = useState("");
  const [weight, setWeight] = useState("");
  const [rate, setRate] = useState("");
  const [amount, setAmount] = useState("0");
  const [startReading, setStartReading] = useState("0");
  const [commissionAmount, setCommissionAmount] = useState("");
  const [remarks, setRemarks] = useState("");
  const [error, setError] = useState("");
  const [hydrated, setHydrated] = useState(isNew);
  const [cancelConfirming, setCancelConfirming] = useState(false);
  const [detailsOpen, setDetailsOpen] = useState(isNew);
  // Collapsed by default: valuable when you need it, noise when you don't.
  const [historyOpen, setHistoryOpen] = useState(false);

  useEffect(() => {
    if (trip && !hydrated) {
      setDate(trip.date.slice(0, 10));
      setVehicleId(trip.vehicleId);
      setDriverId(trip.driverId);
      setPartyId(trip.partyId);
      setFromCityId(trip.fromCityId);
      setToCityId(trip.toCityId);
      setConsignorName(trip.consignorName);
      setConsigneeName(trip.consigneeName);
      setWayBillNo(trip.wayBillNo ?? "");
      setWeight(trip.weight?.toString() ?? "");
      setRate(trip.rate?.toString() ?? "");
      setAmount(trip.amount.toString());
      setStartReading(trip.startReading.toString());
      setCommissionAmount(trip.commissionAmount?.toString() ?? "");
      setRemarks(trip.remarks ?? "");
      setHydrated(true);
    }
  }, [trip, hydrated]);

  useEffect(() => {
    if (weight && rate) setAmount(String(Math.round(Number(weight) * Number(rate))));
  }, [weight, rate]);

  const selectedVehicle = vehiclesQuery.data?.find((v) => v.id === vehicleId);
  const isOtherOwner = selectedVehicle?.ownership === "Other";

  const saveMutation = useMutation({
    mutationFn: () =>
      api.post<Trip>("/api/trips", {
        id: trip?.id ?? empty,
        date,
        vehicleId,
        driverId,
        partyId,
        fromCityId,
        toCityId,
        consignorName,
        consigneeName,
        wayBillNo: wayBillNo || null,
        weight: weight ? Number(weight) : null,
        rate: rate ? Number(rate) : null,
        amount: Number(amount) || 0,
        startReading: Number(startReading) || 0,
        commissionAmount: isOtherOwner && commissionAmount ? Number(commissionAmount) : null,
        remarks: remarks || null,
      }),
    onSuccess: (saved) => {
      toast.success("Trip saved.");
      queryClient.invalidateQueries({ queryKey: ["trips"] });
      if (isNew) router.replace(`/trips/${saved.id}`);
      else queryClient.invalidateQueries({ queryKey: ["trips", params.id] });
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const closeMutation = useMutation({
    mutationFn: () => api.post<Trip>(`/api/trips/${trip!.id}/close`),
    onSuccess: () => {
      toast.success("Trip closed.");
      queryClient.invalidateQueries({ queryKey: ["trips", params.id] });
      queryClient.invalidateQueries({ queryKey: ["trips"] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Could not close the trip."),
  });

  const reopenMutation = useMutation({
    mutationFn: () => api.post<Trip>(`/api/trips/${trip!.id}/reopen`),
    onSuccess: () => {
      toast.success("Trip reopened.");
      queryClient.invalidateQueries({ queryKey: ["trips", params.id] });
      queryClient.invalidateQueries({ queryKey: ["trips"] });
    },
  });

  const cancelTripMutation = useMutation({
    mutationFn: () => api.delete(`/api/trips/${trip!.id}`),
    onSuccess: () => {
      toast.success("Trip cancelled.");
      queryClient.invalidateQueries({ queryKey: ["trips"] });
      router.push("/trips");
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Could not cancel the trip."),
  });

  return (
    <div className="mx-auto max-w-2xl space-y-6 p-4 sm:p-6">
      <div className="flex items-center gap-2">
        <Button variant="ghost" size="icon" onClick={() => router.push("/trips")}>
          <ArrowLeft className="h-4 w-4" />
        </Button>
        <h1 className="flex-1 text-xl font-semibold">
          {isNew ? "New trip" : trip ? `Trip ${trip.tripNo}` : "Loading…"}
        </h1>
        {trip && <Badge variant={trip.status === "Open" ? "default" : "success"}>{trip.status}</Badge>}
        {trip && (
          <Button
            variant="ghost"
            size="icon"
            aria-label="Share trip details"
            onClick={async () => {
              const outcome = await shareText(
                `Trip ${trip.tripNo}`,
                [
                  `Trip ${trip.tripNo} — ${trip.vehicle?.regNo}`,
                  `${trip.fromCity?.name} → ${trip.toCity?.name}`,
                  `Party: ${trip.party?.name}`,
                  `Amount: ${formatCurrency(trip.amount)}`,
                  `Balance receivable: ${formatCurrency(trip.balanceReceivable)}`,
                  `Status: ${trip.status}`,
                ].join("\n"),
              );
              if (outcome === "copied") toast.success("Copied — paste it into WhatsApp or wherever you need it.");
              if (outcome === "unavailable") toast.error("Couldn't share or copy on this device.");
            }}
          >
            <Share2 className="h-4 w-4" />
          </Button>
        )}
      </div>

      {/* A closed trip is settled: nothing can be added to it until someone
          deliberately reopens it. Saying so here — instead of leaving buttons
          that lead to a form which fails on submit — is the difference between
          a rule and a dead end. */}
      {trip?.status === "Closed" && (
        <div className="flex items-start gap-3 rounded-2xl border border-border bg-muted/50 p-4">
          <Lock className="mt-0.5 h-5 w-5 shrink-0 text-muted-foreground" />
          <div className="text-sm">
            <p className="font-medium">This trip is closed</p>
            <p className="text-muted-foreground">
              Expenses and amounts are final. Reopen the trip below if you need to change something.
            </p>
          </div>
        </div>
      )}

      {/* On a saved trip the two things people actually come here to do are
          record an expense and record money received — so those lead, above
          the trip's own fields. Each opens a dedicated full screen, which is
          much easier to fill in on a phone than a cramped popup. */}
      {trip && trip.status === "Open" && (
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Button
            size="lg"
            nativeButton={false}
            className="h-14 justify-start gap-3 text-base font-semibold"
            render={<Link href={`/trips/${trip.id}/expenses/new`} />}
          >
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-primary-foreground/20">
              <Receipt className="h-5 w-5" />
            </span>
            Add expense
          </Button>
          <Button
            size="lg"
            variant="outline"
            nativeButton={false}
            className="h-14 justify-start gap-3 text-base font-semibold"
            render={<Link href={`/trips/${trip.id}/amount/new`} />}
          >
            <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
              <Wallet className="h-5 w-5" />
            </span>
            Amount received
          </Button>
        </div>
      )}

      <Card>
        {/* Booking a trip needs these fields up front; revisiting a saved one
            almost never does, so it collapses out of the way rather than
            burying the expense and amount actions under a screenful of form. */}
        {!isNew && (
          <CardHeader className="flex-row items-center justify-between space-y-0">
            <CardTitle className="text-base">Trip details</CardTitle>
            <Button variant="ghost" size="sm" onClick={() => setDetailsOpen((o) => !o)}>
              {detailsOpen ? "Hide" : "Edit"}
              <ChevronDown className={cn("h-4 w-4 transition-transform", detailsOpen && "rotate-180")} />
            </Button>
          </CardHeader>
        )}
        <CardContent className={cn("space-y-4 pt-6", !isNew && !detailsOpen && "hidden")}>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Date</Label>
              <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Vehicle</Label>
              <SearchablePicker
                label="Vehicle"
                value={vehicleId}
                onSelect={setVehicleId}
                options={(vehiclesQuery.data ?? []).map((v) => ({ id: v.id, label: v.display }))}
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Driver</Label>
              <Select value={driverId} onValueChange={(v) => setDriverId(v ?? "")}>
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Choose">
                    {(v: string) => driversQuery.data?.find((x) => x.id === v)?.display ?? "Choose"}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent>
                  {driversQuery.data?.map((d) => <SelectItem key={d.id} value={d.id}>{d.display}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-2">
              <Label>Party</Label>
              <SearchablePicker
                label="Party"
                value={partyId}
                onSelect={setPartyId}
                options={(partiesQuery.data ?? []).map((p) => ({ id: p.id, label: p.name, sublabel: p.phone ?? undefined }))}
                onAddNew={(text) => setQuickAddParty(text)}
                addNewLabel="Add party"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>From</Label>
              <SearchablePicker
                label="From city"
                value={fromCityId}
                onSelect={setFromCityId}
                options={(citiesQuery.data ?? []).map((c) => ({ id: c.id, label: c.display }))}
                onAddNew={(text) => setQuickAddCity({ text, target: "from" })}
                addNewLabel="Add location"
              />
            </div>
            <div className="space-y-2">
              <Label>To</Label>
              <SearchablePicker
                label="To city"
                value={toCityId}
                onSelect={setToCityId}
                options={(citiesQuery.data ?? []).map((c) => ({ id: c.id, label: c.display }))}
                onAddNew={(text) => setQuickAddCity({ text, target: "to" })}
                addNewLabel="Add location"
              />
            </div>
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-2">
              <Label>Consignor (optional)</Label>
              <Input value={consignorName} onChange={(e) => setConsignorName(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Consignee (optional)</Label>
              <Input value={consigneeName} onChange={(e) => setConsigneeName(e.target.value)} />
            </div>
          </div>

          <div className="space-y-2">
            <Label>Way bill no. (optional)</Label>
            <Input
              value={wayBillNo}
              onChange={(e) => setWayBillNo(e.target.value)}
              placeholder="Printed on the LR when entered"
            />
          </div>

          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-2">
              <Label>Weight</Label>
              <Input type="number" value={weight} onChange={(e) => setWeight(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Rate</Label>
              <Input type="number" value={rate} onChange={(e) => setRate(e.target.value)} />
            </div>
            <div className="space-y-2">
              <Label>Amount</Label>
              <Input type="number" value={amount} onChange={(e) => setAmount(e.target.value)} />
            </div>
          </div>

          {isOtherOwner && (
            <div className="space-y-2">
              <Label>Commission amount</Label>
              <Input type="number" value={commissionAmount} onChange={(e) => setCommissionAmount(e.target.value)} />
            </div>
          )}

          <div className="space-y-2">
            <Label>Remarks</Label>
            <Textarea value={remarks} onChange={(e) => setRemarks(e.target.value)} />
          </div>

          {error && <p className="text-sm font-medium text-destructive">{error}</p>}

          <Button
            className="w-full"
            disabled={saveMutation.isPending || !vehicleId || !driverId || !partyId || !fromCityId || !toCityId}
            onClick={() => { setError(""); saveMutation.mutate(); }}
          >
            {saveMutation.isPending ? "Saving…" : "Save trip"}
          </Button>
        </CardContent>
      </Card>

      <QuickAddPartyDialog
        searchText={quickAddParty}
        onClose={() => setQuickAddParty(null)}
        onAdded={(id) => { setPartyId(id); queryClient.invalidateQueries({ queryKey: ["parties"] }); }}
      />
      <QuickAddCityDialog
        request={quickAddCity}
        states={statesQuery.data ?? []}
        onClose={() => setQuickAddCity(null)}
        onAdded={(id, target) => {
          if (target === "from") setFromCityId(id); else setToCityId(id);
          queryClient.invalidateQueries({ queryKey: ["cities"] });
        }}
      />

      {trip && (
        <>
          <Card>
            <CardHeader><CardTitle className="text-base">Summary</CardTitle></CardHeader>
            <CardContent className="grid grid-cols-2 gap-3 text-sm sm:grid-cols-3">
              <Summary label="Total expenses" value={formatCurrency(trip.totalExpenses)} />
              <Summary label="Approved received" value={formatCurrency(trip.totalApprovedReceived)} />
              <Summary label="Balance receivable" value={formatCurrency(trip.balanceReceivable)} />
              <Summary label="Net after expenses" value={formatCurrency(trip.netAfterExpenses)} />
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">Documents</CardTitle></CardHeader>
            <CardContent className="grid grid-cols-2 gap-3">
              <DocumentButton tripId={trip.id} kind="lr" icon={FileText} label="LR" />
              <DocumentButton tripId={trip.id} kind="bill" icon={Receipt} label="Bill" />
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">Expenses</CardTitle>
              {trip.status === "Open" && (
                <Button
                  size="sm"
                  variant="outline"
                  nativeButton={false}
                  render={<Link href={`/trips/${trip.id}/expenses/new`} />}
                >
                  <Plus className="h-4 w-4" /> Add
                </Button>
              )}
            </CardHeader>
            <CardContent className="space-y-2">
              {trip.expenses.map((e) => (
                <ExpenseRow key={e.id} tripId={trip.id} expense={e} canEdit={trip.status === "Open"} />
              ))}
              {trip.expenses.length === 0 && (
                <p className="text-sm text-muted-foreground">
                  No expenses yet — tap <span className="font-medium text-foreground">Add</span> to record one.
                </p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">Amounts received</CardTitle>
              {trip.status === "Open" && (
                <Button
                  size="sm"
                  variant="outline"
                  nativeButton={false}
                  render={<Link href={`/trips/${trip.id}/amount/new`} />}
                >
                  <Plus className="h-4 w-4" /> Add
                </Button>
              )}
            </CardHeader>
            <CardContent className="space-y-2">
              {trip.transactions.map((t) => (
                <AmountRow key={t.id} tripId={trip.id} transaction={t} isOwner={isOwner} />
              ))}
              {trip.transactions.length === 0 && (
                <p className="text-sm text-muted-foreground">
                  No amounts recorded yet — tap <span className="font-medium text-foreground">Add</span> to record one.
                </p>
              )}
            </CardContent>
          </Card>

          {/* The trip's own record of what happened to it — kept on the trip
              rather than only in a separate log, since that's where anyone
              asking "who changed this?" is already standing. */}
          <Card>
            <CardHeader className="flex-row items-center justify-between space-y-0">
              <CardTitle className="text-base">History</CardTitle>
              <Button variant="ghost" size="sm" onClick={() => setHistoryOpen((o) => !o)}>
                {historyOpen ? "Hide" : "Show"}
                <ChevronDown className={cn("h-4 w-4 transition-transform", historyOpen && "rotate-180")} />
              </Button>
            </CardHeader>
            {historyOpen && (
              <CardContent>
                <TripAuditTrail tripId={trip.id} />
              </CardContent>
            )}
          </Card>

          <Separator />

          {trip.status === "Open" ? (
            <Button
              variant="outline"
              className="w-full"
              disabled={closeMutation.isPending}
              onClick={() => closeMutation.mutate()}
            >
              <Lock className="h-4 w-4" /> Close trip
            </Button>
          ) : (
            <Button variant="outline" className="w-full" onClick={() => reopenMutation.mutate()}>
              <LockOpen className="h-4 w-4" /> Reopen trip
            </Button>
          )}

          {/* Owner-only, and confirmed before it acts: cancelling withdraws
              the whole trip along with its expenses and amounts. The API
              refuses this for anyone else regardless of what's drawn here. */}
          {isOwner && (
            <div className="rounded-2xl border border-destructive/30 p-3">
              {cancelConfirming ? (
                <div className="space-y-3">
                  <p className="text-sm">
                    Cancel trip {trip.tripNo}? Its expenses and amounts go with it. The record is kept for
                    the audit trail, but it leaves your trips list.
                  </p>
                  <div className="flex gap-2">
                    <Button
                      variant="outline"
                      className="flex-1"
                      onClick={() => setCancelConfirming(false)}
                    >
                      Keep trip
                    </Button>
                    <Button
                      variant="destructive"
                      className="flex-1"
                      disabled={cancelTripMutation.isPending}
                      onClick={() => cancelTripMutation.mutate()}
                    >
                      {cancelTripMutation.isPending ? "Cancelling…" : "Cancel trip"}
                    </Button>
                  </div>
                </div>
              ) : (
                <Button
                  variant="ghost"
                  className="w-full text-destructive"
                  onClick={() => setCancelConfirming(true)}
                >
                  <Trash2 className="h-4 w-4" /> Cancel trip
                </Button>
              )}
            </div>
          )}
        </>
      )}
    </div>
  );
}

/** Add a party without leaving the trip form. Same POST/validation the
 *  Vehicles & Contacts screen uses — just name is required, phone optional. */
function QuickAddPartyDialog({
  searchText,
  onClose,
  onAdded,
}: {
  searchText: string | null;
  onClose: () => void;
  onAdded: (id: string) => void;
}) {
  const [name, setName] = useState("");
  const [phone, setPhone] = useState("");
  const [error, setError] = useState("");

  const [openFor, setOpenFor] = useState(searchText);
  if (openFor !== searchText) {
    setOpenFor(searchText);
    setName(searchText ?? "");
    setPhone("");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post<string>("/api/masters/parties", {
        id: "00000000-0000-0000-0000-000000000000",
        name,
        phone: phone || null,
      }),
    onSuccess: (id) => {
      toast.success("Party added.");
      onAdded(id);
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={searchText !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>Add party</DialogTitle></DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
          <div className="space-y-2">
            <Label>Name</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} className="h-12 text-base" required autoFocus />
          </div>
          <div className="space-y-2">
            <Label>Phone (optional)</Label>
            <Input value={phone} onChange={(e) => setPhone(e.target.value)} className="h-12 text-base" />
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending} className="w-full">
              {mutation.isPending ? "Adding…" : "Add party"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

/** Add a from/to location without leaving the trip form. A city needs a
 *  state, so this is the one field the trip form can't skip. */
function QuickAddCityDialog({
  request,
  states,
  onClose,
  onAdded,
}: {
  request: { text: string; target: "from" | "to" } | null;
  states: State[];
  onClose: () => void;
  onAdded: (id: string, target: "from" | "to") => void;
}) {
  const [name, setName] = useState("");
  const [stateId, setStateId] = useState("");
  const [error, setError] = useState("");

  const [openFor, setOpenFor] = useState(request);
  if (openFor !== request) {
    setOpenFor(request);
    setName(request?.text ?? "");
    setStateId("");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post<string>("/api/masters/cities", { id: "00000000-0000-0000-0000-000000000000", name, stateId }),
    onSuccess: (id) => {
      toast.success("Location added.");
      onAdded(id, request!.target);
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={request !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader><DialogTitle>Add location</DialogTitle></DialogHeader>
        <form className="space-y-4" onSubmit={(e) => { e.preventDefault(); mutation.mutate(); }}>
          <div className="space-y-2">
            <Label>City</Label>
            <Input value={name} onChange={(e) => setName(e.target.value)} className="h-12 text-base" required autoFocus />
          </div>
          <div className="space-y-2">
            <Label>State</Label>
            <Select value={stateId} onValueChange={(v) => setStateId(v ?? "")}>
              <SelectTrigger className="h-12 w-full text-base">
                <SelectValue placeholder="Choose a state">
                  {(v: string) => states.find((s) => s.id === v)?.name ?? "Choose a state"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {states.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending || !stateId} className="w-full">
              {mutation.isPending ? "Adding…" : "Add location"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function Summary({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="font-semibold">{value}</p>
    </div>
  );
}

function DocumentButton({
  tripId,
  kind,
  icon: Icon,
  label,
}: {
  tripId: string;
  kind: "lr" | "bill";
  icon: typeof FileText;
  label: string;
}) {
  const [busy, setBusy] = useState(false);

  const openAndShare = async () => {
    setBusy(true);
    try {
      const file = await api.getFile(`/api/trips/${tripId}/${kind}`, `${label}.pdf`);
      const outcome = await shareFile(file, { title: `${label} — ${tripId}` });
      if (outcome === "downloaded") toast.success(`${label} downloaded — open it from your downloads.`);
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : `Couldn't generate the ${label}.`);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Button variant="outline" className="h-14" disabled={busy} onClick={openAndShare}>
      <Icon className="h-4 w-4" /> {busy ? "Preparing…" : `${label} — view / share`}
    </Button>
  );
}

/// An amount received. An approved one can never be edited, so deleting it is
/// the only correction available — and that authority is the Owner's alone,
/// which the API enforces too (DELETE /api/transactions/{id} is Owner-gated).
function AmountRow({ tripId, transaction, isOwner }: {
  tripId: string;
  transaction: Trip["transactions"][number];
  isOwner: boolean;
}) {
  const queryClient = useQueryClient();
  const [confirming, setConfirming] = useState(false);

  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/transactions/${transaction.id}`),
    onSuccess: () => {
      toast.success("Amount removed.");
      queryClient.invalidateQueries({ queryKey: ["trips", tripId] });
      queryClient.invalidateQueries({ queryKey: ["trips"] });
      queryClient.invalidateQueries({ queryKey: ["audit", "trip", tripId] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Couldn't remove that amount."),
  });

  return (
    <div className="rounded-lg border p-3 text-sm">
      <div className="flex items-center justify-between gap-2">
        <div>
          <p className="font-medium">{formatCurrency(transaction.amount)} · {transaction.paymentMode}</p>
          <p className="text-xs text-muted-foreground">{formatDate(transaction.date)}</p>
        </div>
        <div className="flex items-center gap-1">
          <Badge
            variant={
              transaction.approvalStatus === "Approved"
                ? "success"
                : transaction.approvalStatus === "Rejected"
                  ? "destructive"
                  : "warning"
            }
          >
            {transaction.approvalStatus}
          </Badge>
          {isOwner && (
            <Button
              variant="ghost"
              size="icon"
              onClick={() => setConfirming((c) => !c)}
              disabled={deleteMutation.isPending}
            >
              <Trash2 className="h-4 w-4 text-destructive" />
            </Button>
          )}
        </div>
      </div>

      {/* Deleting money off a trip changes its balance, so it asks first
          rather than acting on a single mis-tap. */}
      {confirming && (
        <div className="mt-3 flex items-center justify-between gap-2 rounded-lg bg-accent p-2">
          <p className="text-xs">Remove this amount from the trip?</p>
          <div className="flex gap-1">
            <Button size="sm" variant="ghost" onClick={() => setConfirming(false)}>
              Cancel
            </Button>
            <Button
              size="sm"
              variant="destructive"
              disabled={deleteMutation.isPending}
              onClick={() => deleteMutation.mutate()}
            >
              {deleteMutation.isPending ? "Removing…" : "Remove"}
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}

function ExpenseRow({ tripId, expense, canEdit }: {
  tripId: string;
  expense: Trip["expenses"][number];
  canEdit: boolean;
}) {
  const queryClient = useQueryClient();
  const deleteMutation = useMutation({
    mutationFn: () => api.delete(`/api/trips/expenses/${expense.id}`),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["trips", tripId] });
      // The trail gained an entry — refresh it so History stays in step.
      queryClient.invalidateQueries({ queryKey: ["audit", "trip", tripId] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Couldn't remove that expense."),
  });

  const Icon = iconForCategory(expense.expenseCategory?.name ?? "");

  return (
    <div className="flex items-center gap-3 rounded-2xl border p-3 text-sm">
      <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
        <Icon className="h-4 w-4" />
      </div>
      <div className="flex-1">
        <p className="font-medium">{expense.expenseCategory?.name} · {formatCurrency(expense.amount)}</p>
        <p className="text-xs text-muted-foreground">{formatDate(expense.date)}</p>
      </div>
      {canEdit && (
        <Button variant="ghost" size="icon" onClick={() => deleteMutation.mutate()} disabled={deleteMutation.isPending}>
          <Trash2 className="h-4 w-4 text-destructive" />
        </Button>
      )}
    </div>
  );
}
