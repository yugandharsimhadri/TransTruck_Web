"use client";

import { useState } from "react";
import { useParams, useRouter } from "next/navigation";
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
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";
import { DocumentPanel } from "@/components/masters/document-panel";
import { VehicleExpensesPanel } from "@/components/masters/vehicle-expenses-panel";
import { api, ApiError } from "@/lib/api";
import { formatCurrency } from "@/lib/format";
import { VEHICLE_DOCUMENT_TYPES, type Vehicle, type VehicleOwnership } from "@/lib/types";

const empty = "00000000-0000-0000-0000-000000000000";

/**
 * One lorry, on a screen of its own.
 *
 * This was a single dialog holding identity, ownership, five expiry dates, the
 * loan terms, the recurring paperwork costs and the document uploads, all in
 * one scroll. Everything was technically reachable and nothing was findable —
 * on a phone the loan fields sat below three folds of form.
 *
 * Split into the three questions actually asked about a vehicle, which are
 * asked at different times by different people:
 *   Details  — what this lorry is. Set once.
 *   Papers   — what expires when, the scans, and what the paperwork costs.
 *   Loan     — what is still owed on it.
 *
 * The three tabs edit one record and share one Save, so moving between them
 * never loses a half-typed field. The documents and the cost schedules are the
 * exception: they have their own endpoints and save themselves on the spot,
 * which is why they sit apart from the form's Save.
 */
export default function VehicleDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;
  const isNew = id === "new";
  const router = useRouter();
  const queryClient = useQueryClient();

  // The list is the only endpoint that returns vehicles, and it is almost
  // always already cached from the screen that linked here.
  const vehiclesQuery = useQuery({
    queryKey: ["vehicles"],
    queryFn: () => api.get<Vehicle[]>("/api/vehicles"),
  });

  const existing = isNew ? null : vehiclesQuery.data?.find((v) => v.id === id) ?? null;
  const loading = !isNew && vehiclesQuery.isLoading;

  if (loading) {
    return (
      <>
        <PageHeader title="Loading…" backTo="/vehicles" width="form" />
        <PageContainer width="form">{null}</PageContainer>
      </>
    );
  }

  if (!isNew && !existing) {
    return (
      <>
        <PageHeader title="Vehicle not found" backTo="/vehicles" width="form" />
        <PageContainer width="form">
          <p className="text-sm text-muted-foreground">
            That vehicle no longer exists. It may have been removed.
          </p>
        </PageContainer>
      </>
    );
  }

  return (
    <VehicleEditor
      key={existing?.id ?? "new"}
      vehicle={existing}
      onSavedNew={async (savedId) => {
        // Land on the real vehicle so its papers, loan and uploads become
        // reachable, and replace rather than push so Back skips the empty form.
        await queryClient.invalidateQueries({ queryKey: ["vehicles"] });
        router.replace(`/vehicles/${savedId}`);
      }}
      onSaved={() => queryClient.invalidateQueries({ queryKey: ["vehicles"] })}
    />
  );
}

function VehicleEditor({
  vehicle,
  onSaved,
  onSavedNew,
}: {
  vehicle: Vehicle | null;
  onSaved: () => void;
  onSavedNew: (id: string) => void;
}) {
  const isNew = vehicle === null;

  const [regNo, setRegNo] = useState(vehicle?.regNo ?? "");
  const [vehicleType, setVehicleType] = useState(vehicle?.vehicleType ?? "");
  const [capacity, setCapacity] = useState(vehicle?.capacity?.toString() ?? "");
  const [ownership, setOwnership] = useState<VehicleOwnership>(vehicle?.ownership ?? "Own");
  const [ownerName, setOwnerName] = useState(vehicle?.owner?.name ?? "");
  const [ownerPhone, setOwnerPhone] = useState(vehicle?.owner?.phone ?? "");
  const [isActive, setIsActive] = useState(vehicle?.isActive ?? true);

  const [permitUpto, setPermitUpto] = useState(vehicle?.permitUpto?.slice(0, 10) ?? "");
  const [nationalPermitUpto, setNationalPermitUpto] = useState(vehicle?.nationalPermitUpto?.slice(0, 10) ?? "");
  const [insuranceUpto, setInsuranceUpto] = useState(vehicle?.insuranceUpto?.slice(0, 10) ?? "");
  const [fitnessUpto, setFitnessUpto] = useState(vehicle?.fitnessUpto?.slice(0, 10) ?? "");
  const [pollutionUpto, setPollutionUpto] = useState(vehicle?.pollutionUpto?.slice(0, 10) ?? "");

  const [loanAmount, setLoanAmount] = useState(vehicle?.loanAmount?.toString() ?? "");
  const [loanStartDate, setLoanStartDate] = useState(vehicle?.loanStartDate?.slice(0, 10) ?? "");
  const [loanEndDate, setLoanEndDate] = useState(vehicle?.loanEndDate?.slice(0, 10) ?? "");
  const [emiAmount, setEmiAmount] = useState(vehicle?.emiAmount?.toString() ?? "");
  const [emiDay, setEmiDay] = useState(vehicle?.emiDayOfMonth?.toString() ?? "");

  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: async () => {
      let ownerId: string | undefined = vehicle?.ownerId ?? undefined;
      if (ownership === "Other") {
        ownerId = await api.post<string>("/api/masters/owners/basic", {
          existingOwnerId: ownerId ?? null,
          name: ownerName,
          phone: ownerPhone,
        });
      }

      return await api.post<string>("/api/vehicles", {
        id: vehicle?.id ?? empty,
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
        loanAmount: loanAmount ? Number(loanAmount) : null,
        loanStartDate: loanStartDate || null,
        loanEndDate: loanEndDate || null,
        emiAmount: emiAmount ? Number(emiAmount) : null,
        emiDayOfMonth: emiDay ? Number(emiDay) : null,
        isActive,
      });
    },
    onSuccess: (savedId) => {
      onSaved();
      if (isNew && savedId) {
        toast.success("Vehicle saved — its papers and loan are on the tabs above.");
        onSavedNew(savedId);
        return;
      }
      toast.success("Saved.");
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const save = (e: React.FormEvent) => {
    e.preventDefault();
    setError("");
    mutation.mutate();
  };

  return (
    <>
      <PageHeader
        width="form"
        title={isNew ? "Add vehicle" : vehicle.regNo}
        subtitle={
          isNew
            ? undefined
            : [vehicle.vehicleType, vehicle.ownership === "Own" ? "Own fleet" : vehicle.owner?.name ?? "Other owner"]
                .filter(Boolean)
                .join(" · ")
        }
        backTo="/vehicles"
        actions={
          !isNew && (
            <Badge variant={vehicle.isActive ? "success" : "secondary"}>
              {vehicle.isActive ? "Active" : "Inactive"}
            </Badge>
          )
        }
      />

      <PageContainer width="form" className="space-y-4">
        <form onSubmit={save} className="space-y-4">
          <Tabs defaultValue="details">
            <TabsList>
              <TabsTrigger value="details">Details</TabsTrigger>
              <TabsTrigger value="papers">Papers</TabsTrigger>
              <TabsTrigger value="loan">Loan</TabsTrigger>
            </TabsList>

            <TabsContent value="details" className="space-y-4">
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

              {/* Retiring a lorry rather than deleting it: its trips, expenses
                  and maintenance history all still have to add up. */}
              <label className="flex items-center gap-2.5 rounded-lg border p-3">
                <input
                  type="checkbox"
                  className="h-4 w-4"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                />
                <span className="text-sm font-medium">In service</span>
                <span className="text-xs text-muted-foreground">
                  Turn off to retire it without losing its history
                </span>
              </label>
            </TabsContent>

            <TabsContent value="papers" className="space-y-4">
              <div className="space-y-3 rounded-lg border p-3">
                <Label className="text-xs text-muted-foreground">Valid until</Label>
                <div className="grid grid-cols-2 gap-3">
                  <div className="space-y-1">
                    <Label htmlFor="insuranceUpto" className="text-xs">Insurance</Label>
                    <Input id="insuranceUpto" type="date" value={insuranceUpto}
                      onChange={(e) => setInsuranceUpto(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="fitnessUpto" className="text-xs">Fitness</Label>
                    <Input id="fitnessUpto" type="date" value={fitnessUpto}
                      onChange={(e) => setFitnessUpto(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="permitUpto" className="text-xs">Permit</Label>
                    <Input id="permitUpto" type="date" value={permitUpto}
                      onChange={(e) => setPermitUpto(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="nationalPermitUpto" className="text-xs">National permit</Label>
                    <Input id="nationalPermitUpto" type="date" value={nationalPermitUpto}
                      onChange={(e) => setNationalPermitUpto(e.target.value)} />
                  </div>
                  <div className="space-y-1">
                    <Label htmlFor="pollutionUpto" className="text-xs">Pollution</Label>
                    <Input id="pollutionUpto" type="date" value={pollutionUpto}
                      onChange={(e) => setPollutionUpto(e.target.value)} />
                  </div>
                </div>
              </div>

              {/* Saves itself — hence sitting outside the form's own Save. */}
              <VehicleExpensesPanel vehicleId={vehicle?.id ?? null} />

              <DocumentPanel
                ownerPath="vehicles"
                ownerId={vehicle?.id ?? null}
                types={VEHICLE_DOCUMENT_TYPES}
                emptyText="No documents uploaded for this vehicle yet."
              />
            </TabsContent>

            <TabsContent value="loan" className="space-y-4">
              {/* Most lorries are owned outright, so this opens on a plain
                  statement of that rather than five empty boxes implying
                  something is missing. */}
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label htmlFor="loanAmount" className="text-xs">Loan amount</Label>
                  <Input id="loanAmount" type="number" inputMode="decimal" value={loanAmount}
                    onChange={(e) => setLoanAmount(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="emiAmount" className="text-xs">EMI amount</Label>
                  <Input id="emiAmount" type="number" inputMode="decimal" value={emiAmount}
                    onChange={(e) => setEmiAmount(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="loanStartDate" className="text-xs">Loan start</Label>
                  <Input id="loanStartDate" type="date" value={loanStartDate}
                    onChange={(e) => setLoanStartDate(e.target.value)} />
                </div>
                <div className="space-y-1">
                  <Label htmlFor="loanEndDate" className="text-xs">Loan end</Label>
                  <Input id="loanEndDate" type="date" value={loanEndDate}
                    onChange={(e) => setLoanEndDate(e.target.value)} />
                </div>
                <div className="space-y-1">
                  {/* A day rather than a date: the instalment repeats monthly,
                      and only the day is stable across those months. */}
                  <Label htmlFor="emiDay" className="text-xs">EMI day of month</Label>
                  <Input id="emiDay" type="number" inputMode="numeric" min={1} max={31} placeholder="5"
                    value={emiDay} onChange={(e) => setEmiDay(e.target.value)} />
                </div>
              </div>

              {loanAmount ? (
                <p className="rounded-lg bg-muted/50 p-3 text-xs text-muted-foreground">
                  {formatCurrency(Number(loanAmount) || 0)} borrowed
                  {emiAmount ? `, repaid at ${formatCurrency(Number(emiAmount) || 0)} a month` : ""}
                  {emiDay ? ` on the ${emiDay}${ordinal(Number(emiDay))}` : ""}.
                </p>
              ) : (
                <p className="rounded-lg bg-muted/50 p-3 text-xs text-muted-foreground">
                  Nothing owed on this lorry. Fill these in if it is on finance.
                </p>
              )}
            </TabsContent>
          </Tabs>

          {error && <p className="text-sm font-medium text-destructive">{error}</p>}

          {/* One Save for all three tabs — they are one record, and a save per
              tab would lose whatever was typed on the others. */}
          <Button type="submit" disabled={mutation.isPending} className="h-12 w-full">
            {mutation.isPending ? "Saving…" : isNew ? "Save vehicle" : "Save changes"}
          </Button>
        </form>
      </PageContainer>
    </>
  );
}

function ordinal(day: number) {
  if (day >= 11 && day <= 13) return "th";
  return { 1: "st", 2: "nd", 3: "rd" }[day % 10] ?? "th";
}
