"use client";

import { useState } from "react";
import Link from "next/link";
import { useInfiniteQuery, useQuery } from "@tanstack/react-query";
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
import { Label } from "@/components/ui/label";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { TripListPage, TripListSort, Vehicle } from "@/lib/types";
import { Plus } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";

type SortKey = "date-desc" | "date-asc" | "balance-desc" | "amount-desc";

const sortLabels: Record<SortKey, string> = {
  "date-desc": "Latest Trips",
  "date-asc": "Oldest Trips",
  "balance-desc": "Highest Balance",
  "amount-desc": "Highest Trip Amount",
};

// The API sorts and filters now, so these controls have to speak its language.
const sortParam: Record<SortKey, TripListSort> = {
  "date-desc": "DateDesc",
  "date-asc": "DateAsc",
  "balance-desc": "BalanceDesc",
  "amount-desc": "AmountDesc",
};

// A page has to be worth the round trip without being a wall of cards on a
// phone. Twenty-five fills a tall screen roughly twice over.
const PAGE_SIZE = 25;

export default function TripsPage() {
  const [filter, setFilter] = useState<"open" | "closed" | "all">("open");
  const [regNo, setRegNo] = useState("");
  const [sort, setSort] = useState<SortKey>("date-desc");

  // Filtering and sorting are the API's job now. They have to be: a filter
  // applied here would only ever see the pages already fetched, so "Closed"
  // over a run of open trips would show nothing and quietly mean it.
  // Every control below is therefore part of the query key — change one and
  // this starts again from the first page, which is also what the user
  // expects to see.
  const tripsQuery = useInfiniteQuery({
    queryKey: ["trips", filter, regNo, sort],
    initialPageParam: 0,
    queryFn: ({ pageParam }) => {
      const params = new URLSearchParams({
        sort: sortParam[sort],
        skip: String(pageParam),
        take: String(PAGE_SIZE),
      });
      if (filter !== "all") params.set("status", filter === "open" ? "Open" : "Closed");
      if (regNo) params.set("regNo", regNo);
      return api.get<TripListPage>(`/api/trips?${params}`);
    },
    getNextPageParam: (last, pages) => {
      const loaded = pages.reduce((n, p) => n + p.items.length, 0);
      return loaded < last.total ? loaded : undefined;
    },
  });

  const trips = tripsQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const total = tripsQuery.data?.pages[0]?.total ?? 0;

  const vehiclesQuery = useQuery({ queryKey: ["vehicles"], queryFn: () => api.get<Vehicle[]>("/api/vehicles") });

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

        {/* A full-width button rather than a numbered pager: on a phone this
            sits under the thumb, needs no aiming, and never competes with the
            bottom tab bar for the same corner of the screen. The count above
            it answers the question a pager would have — how much is left. */}
        {trips.length > 0 && (
          <div className="flex flex-col items-center gap-2 pt-2">
            <p className="text-xs text-muted-foreground" aria-live="polite">
              Showing {trips.length} of {total} {total === 1 ? "trip" : "trips"}
            </p>
            {tripsQuery.hasNextPage && (
              <Button
                variant="outline"
                className="h-11 w-full sm:w-auto sm:min-w-56"
                onClick={() => tripsQuery.fetchNextPage()}
                disabled={tripsQuery.isFetchingNextPage}
              >
                {tripsQuery.isFetchingNextPage ? "Loading…" : "Load more trips"}
              </Button>
            )}
          </div>
        )}
      </div>
    </PageContainer>
  );
}
