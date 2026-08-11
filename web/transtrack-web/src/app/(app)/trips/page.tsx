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
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { TripListItem } from "@/lib/types";
import { Plus } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";

export default function TripsPage() {
  const [filter, setFilter] = useState<"open" | "closed" | "all">("open");

  const tripsQuery = useQuery({
    queryKey: ["trips"],
    queryFn: () => api.get<TripListItem[]>("/api/trips"),
  });

  const trips = (tripsQuery.data ?? []).filter((t) =>
    filter === "all" ? true : filter === "open" ? t.status === "Open" : t.status === "Closed",
  );

  return (
    <div className="space-y-4 p-4 sm:p-6">
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

      <Select value={filter} onValueChange={(v) => v && setFilter(v as typeof filter)}>
        <SelectTrigger className="w-40">
          <SelectValue>{(v: typeof filter) => (v === "open" ? "Open" : v === "closed" ? "Closed" : "All")}</SelectValue>
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="open">Open</SelectItem>
          <SelectItem value="closed">Closed</SelectItem>
          <SelectItem value="all">All</SelectItem>
        </SelectContent>
      </Select>

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
    </div>
  );
}
