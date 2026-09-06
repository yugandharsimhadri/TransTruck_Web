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
import { PageHeader } from "@/components/shell/page-header";
import { Label } from "@/components/ui/label";
import { Input } from "@/components/ui/input";
import { SearchablePicker } from "@/components/ui/searchable-picker";
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Party, TripListPage, TripListSort, Vehicle } from "@/lib/types";
import { Plus } from "lucide-react";
import { TruckEmpty } from "@/components/truck-drive";

// A page has to be worth the round trip without being a wall of cards on a
// phone. Twenty-five fills a tall screen roughly twice over.
const PAGE_SIZE = 25;

export default function TripsPage() {
  const [filter, setFilter] = useState<"open" | "closed" | "all">("open");
  const [regNo, setRegNo] = useState("");
  const [partyId, setPartyId] = useState("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  // Filtering is the API's job. It has to be: a filter applied here would only
  // ever see the pages already fetched, so "Closed" over a run of open trips
  // would show nothing and quietly mean it. Every control below is therefore
  // part of the query key — change one and this starts again from the first
  // page, which is also what the user expects to see.
  //
  // Always oldest-first. The list reads as a run of work in the order it
  // happened, which is how a trip sheet is checked against a ledger; the sort
  // control that used to sit here offered four orders nobody asked for and
  // made the default ambiguous.
  const tripsQuery = useInfiniteQuery({
    queryKey: ["trips", filter, regNo, partyId, from, to],
    initialPageParam: 0,
    queryFn: ({ pageParam }) => {
      const params = new URLSearchParams({
        sort: "DateAsc" satisfies TripListSort,
        skip: String(pageParam),
        take: String(PAGE_SIZE),
      });
      if (filter !== "all") params.set("status", filter === "open" ? "Open" : "Closed");
      if (regNo) params.set("regNo", regNo);
      if (partyId) params.set("partyId", partyId);
      if (from) params.set("from", from);
      if (to) params.set("to", to);
      return api.get<TripListPage>(`/api/trips?${params}`);
    },
    getNextPageParam: (last, pages) => {
      const loaded = pages.reduce((n, p) => n + p.items.length, 0);
      return loaded < last.total ? loaded : undefined;
    },
  });

  const trips = tripsQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const total = tripsQuery.data?.pages[0]?.total ?? 0;

  // Filters look backwards, so they list retired lorries and parties too — you
  // still need to find last year's trips on a lorry you have since sold.
  const vehiclesQuery = useQuery({
    queryKey: ["vehicles", "all"],
    queryFn: () => api.get<Vehicle[]>("/api/vehicles?includeInactive=true"),
  });
  const partiesQuery = useQuery({ queryKey: ["parties"], queryFn: () => api.get<Party[]>("/api/masters/parties") });

  return (
    <>
      <PageHeader
        title="Trips"
        backTo="/dashboard"
        actions={
          <Button
            size="sm"
            nativeButton={false}
            render={
              <Link href="/trips/new">
                <Plus className="h-4 w-4" /> New trip
              </Link>
            }
          />
        }
      />
      <PageContainer className="space-y-4">
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
            <Label className="text-xs">Party</Label>
            <SearchablePicker
              label="Party"
              placeholder="All parties"
              value={partyId}
              onSelect={setPartyId}
              options={[
                { id: "", label: "All parties" },
                ...(partiesQuery.data ?? []).map((p) => ({ id: p.id, label: p.name })),
              ]}
            />
          </div>

          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1">
              <Label className="text-xs">From</Label>
              <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="h-11" />
            </div>
            <div className="space-y-1">
              <Label className="text-xs">To</Label>
              <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="h-11" />
            </div>
          </div>

          {/* Only offered once something is set, so the row is a way out of a
              narrow filter rather than permanent furniture. */}
          {(partyId || regNo || from || to || filter !== "open") && (
            <Button
              variant="ghost"
              size="sm"
              className="w-full"
              onClick={() => {
                setFilter("open");
                setRegNo("");
                setPartyId("");
                setFrom("");
                setTo("");
              }}
            >
              Clear filters
            </Button>
          )}
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
                    {/* The whole invoice, not the freight — a trip carrying
                        extras is worth more than weight × rate, and showing the
                        smaller figure beside a larger balance reads as an error. */}
                    <p className="font-semibold">{formatCurrency(t.grandTotal)}</p>
                    {t.totalExtras > 0 && (
                      <p className="text-xs text-muted-foreground">
                        incl. {formatCurrency(t.totalExtras)} extras
                        {t.gstAmount > 0 ? ` + ${formatCurrency(t.gstAmount)} GST` : ""}
                      </p>
                    )}
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
    </>
  );
}
