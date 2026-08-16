"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PageContainer } from "@/components/shell/page-container";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { TripListItem, Vehicle } from "@/lib/types";
import { Plus } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";

type SortKey = "date-desc" | "date-asc" | "balance-desc" | "amount-desc";

const sortLabels: Record<SortKey, string> = {
  "date-desc": "Latest Trips",
  "date-asc": "Oldest Trips",
  "balance-desc": "Highest Balance",
  "amount-desc": "Highest Trip Amount",
};

export default function TripsPage() {
  const [filter, setFilter] = useState<"open" | "closed" | "all">("open");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [regNo, setRegNo] = useState("");
  const [sort, setSort] = useState<SortKey>("date-desc");

  const tripsQuery = useQuery({
    queryKey: ["trips"],
    queryFn: () => api.get<TripListItem[]>("/api/trips"),
  });
  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });

  // Filtered and sorted on the client: the list is already fully loaded, and
  // t.date is the trip's own date (not when the record was entered).
  const trips = (tripsQuery.data ?? [])
    .filter((t) => (filter === "all" ? true : filter === "open" ? t.status === "Open" : t.status === "Closed"))
    .filter((t) => !regNo || t.vehicleRegNo === regNo)
    .filter((t) => !from || t.date.slice(0, 10) >= from)
    .filter((t) => !to || t.date.slice(0, 10) <= to)
    .sort((a, b) => {
      switch (sort) {
        case "date-asc": return a.date.localeCompare(b.date);
        case "balance-desc": return b.balanceReceivable - a.balanceReceivable;
        case "amount-desc": return b.amount - a.amount;
        default: return b.date.localeCompare(a.date);
      }
    });

  return (
    <PageContainer className="space-y-4">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Trips</h1>
        <Button
          size="sm"
          nativeButton={false}
          render={
            <Link href="/trips/new">
              <Plus className="h-4 w-4" /> New trip
            </Link>
          }
        />
      </div>

      <div className="space-y-3 rounded-2xl border p-3">
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1">
            <Label className="text-xs">Status</Label>
            <Select value={filter} onValueChange={(v) => v && setFilter(v as typeof filter)}>
              <SelectTrigger className="h-11 w-full">
                <SelectValue>{(v: typeof filter) => (v === "open" ? "Open" : v === "closed" ? "Closed" : "All")}</SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="open">Open</SelectItem>
                <SelectItem value="closed">Closed</SelectItem>
                <SelectItem value="all">All</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Vehicle</Label>
            <SearchablePicker
              label="Vehicle"
              placeholder="All vehicles"
              value={regNo}
              onSelect={setRegNo}
              options={[
                { id: "", label: "All vehicles" },
                ...(vehiclesQuery.data ?? []).map((v) => ({ id: v.regNo, label: v.regNo })),
              ]}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Trip date from</Label>
            <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-11" />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Trip date to</Label>
            <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-11" />
          </div>
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Sort by</Label>
          <Select value={sort} onValueChange={(v) => v && setSort(v as SortKey)}>
            <SelectTrigger className="h-11 w-full">
              <SelectValue>{(v: SortKey) => sortLabels[v]}</SelectValue>
            </SelectTrigger>
            <SelectContent>
              {(Object.keys(sortLabels) as SortKey[]).map((k) => (
                <SelectItem key={k} value={k}>{sortLabels[k]}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>

      <div className="space-y-2">
        {trips.map((t) => (
          <Link key={t.id} href={`/trips/${t.id}`}>
            <Card className="transition hover:shadow-md">
              <CardContent className="flex items-center justify-between gap-3 p-4">
                <div className="min-w-0">
                  <p className="truncate font-medium">
                    {t.tripNo} · {t.vehicleRegNo} · {formatDate(t.date)}
                  </p>
                  <p className="truncate text-sm text-muted-foreground">
                    {t.fromCity} → {t.toCity} · {t.partyName}
                  </p>
                </div>
                <div className="shrink-0 text-right">
                  <p className="font-semibold">{formatCurrency(t.amount)}</p>
                  <Badge variant={t.status === "Open" ? "default" : "success"}>{t.status}</Badge>
                </div>
              </CardContent>
            </Card>
          </Link>
        ))}
        {trips.length === 0 && !tripsQuery.isLoading && (
          <TruckEmpty
            variant="container"
            title="No trips here yet"
            hint="Book your first trip and it'll show up in this list."
          />
        )}
      </div>
    </PageContainer>
  );
}
