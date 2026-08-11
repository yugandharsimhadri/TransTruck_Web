"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { api } from "@/lib/api";
import type { AuditEntry } from "@/lib/types";
import { AuditTrail } from "@/components/audit-trail";
import { RequireRole } from "@/components/require-role";
import { TruckLoading } from "@/components/truck-drive";

/** The entity names the API stores, paired with wording a user recognises. */
const recordTypes = [
  { value: "TripExpense", label: "Trip expenses" },
  { value: "TripTransaction", label: "Amounts received" },
  { value: "Trip", label: "Trips" },
  { value: "VehicleMaintenance", label: "Maintenance" },
  { value: "DriverLedgerEntry", label: "Driver ledger" },
];

export default function ActivityPage() {
  return (
    <RequireRole roles={["Owner", "CoOwner"]}>
      <ActivityScreen />
    </RequireRole>
  );
}

function ActivityScreen() {
  const [entityType, setEntityType] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const params = new URLSearchParams();
  if (entityType) params.set("entityType", entityType);
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const qs = params.toString();

  const query = useQuery({
    queryKey: ["audit", "recent", qs],
    queryFn: () => api.get<AuditEntry[]>(`/api/audit?${qs}`),
  });

  return (
    <div className="space-y-4 p-4 sm:p-6">
      <div>
        <h1 className="text-xl font-semibold">Activity</h1>
        <p className="text-sm text-muted-foreground">
          Every addition, change and deletion — who did it and when.
        </p>
      </div>

      <Card>
        <CardContent className="grid grid-cols-2 gap-3 p-4">
          <div className="col-span-2 space-y-1.5">
            <Label className="text-xs">Record type</Label>
            <Select value={entityType} onValueChange={(v) => setEntityType(v ?? "")}>
              <SelectTrigger className="w-full">
                <SelectValue placeholder="Everything">
                  {(v: string) => recordTypes.find((r) => r.value === v)?.label ?? "Everything"}
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {recordTypes.map((r) => (
                  <SelectItem key={r.value} value={r.value}>{r.label}</SelectItem>
                ))}
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
        </CardContent>
      </Card>

      {query.isLoading ? (
        <TruckLoading message="Loading activity…" seed="activity" />
      ) : (
        <Card>
          <CardContent className="p-4">
            <AuditTrail
              entries={query.data ?? []}
              emptyText="No activity in this period."
            />
          </CardContent>
        </Card>
      )}
    </div>
  );
}
