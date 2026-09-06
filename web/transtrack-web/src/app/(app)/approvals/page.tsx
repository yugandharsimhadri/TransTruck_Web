"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { Settlement, TripTransaction } from "@/lib/types";
import { Check, X } from "lucide-react";
import { RequireRole } from "@/components/require-role";
import { TruckEmpty } from "@/components/truck-drive";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";

export default function ApprovalsPage() {
  return (
    <RequireRole roles={["Owner"]}>
      <ApprovalsScreen />
    </RequireRole>
  );
}

function ApprovalsScreen() {
  const queryClient = useQueryClient();
  const pendingQuery = useQuery({
    queryKey: ["approvals", "pending"],
    queryFn: () => api.get<TripTransaction[]>("/api/approvals/pending"),
  });

  const settlementsQuery = useQuery({
    queryKey: ["approvals", "settlements"],
    queryFn: () => api.get<Settlement[]>("/api/settlements/pending"),
  });

  const [remarksById, setRemarksById] = useState<Record<string, string>>({});

  const refreshAll = () => {
    queryClient.invalidateQueries({ queryKey: ["approvals"] });
    // A settled trip is a closed trip with money on it, so the lists and
    // figures that count either are now wrong.
    queryClient.invalidateQueries({ queryKey: ["trips"] });
    queryClient.invalidateQueries({ queryKey: ["dashboard"] });
    queryClient.invalidateQueries({ queryKey: ["settleable"] });
  };

  const approveSettlement = useMutation({
    mutationFn: (id: string) =>
      api.post(`/api/settlements/${id}/approve`, { remarks: remarksById[id] ?? null }),
    onSuccess: () => {
      toast.success("Settled — the trips it covered are now closed.");
      refreshAll();
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const rejectSettlement = useMutation({
    mutationFn: (id: string) =>
      api.post(`/api/settlements/${id}/reject`, { remarks: remarksById[id] ?? null }),
    onSuccess: () => {
      toast.success("Rejected — those trips are untouched.");
      refreshAll();
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const approveMutation = useMutation({
    mutationFn: (id: string) => api.post(`/api/approvals/${id}/approve`, { remarks: remarksById[id] ?? null }),
    onSuccess: () => {
      toast.success("Approved.");
      queryClient.invalidateQueries({ queryKey: ["approvals", "pending"] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const rejectMutation = useMutation({
    mutationFn: (id: string) => api.post(`/api/approvals/${id}/reject`, { remarks: remarksById[id] ?? null }),
    onSuccess: () => {
      toast.success("Rejected.");
      queryClient.invalidateQueries({ queryKey: ["approvals", "pending"] });
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <>
      <PageHeader title="Approvals" backTo="/dashboard" />
      <PageContainer className="space-y-4">
        {/* Settlements first: one of these can be worth twenty of the rows
            below it, and it is a single decision rather than a queue to work
            through. */}
        {settlementsQuery.data && settlementsQuery.data.length > 0 && (
          <div className="space-y-3">
            <p className="text-xs font-semibold uppercase text-muted-foreground">Bulk settlements</p>
            {settlementsQuery.data.map((s) => (
              <Card key={s.id} className="border-primary/40">
                <CardContent className="space-y-3 p-4">
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate font-medium">{s.party?.name}</p>
                      <p className="text-sm text-muted-foreground">
                        {s.tripCount} trip{s.tripCount === 1 ? "" : "s"} · {formatDate(s.date)} · {s.paymentMode}
                      </p>
                    </div>
                    <p className="shrink-0 text-lg font-semibold tabular-nums">
                      {formatCurrency(s.totalAmount)}
                    </p>
                  </div>

                  {s.remarks && <p className="text-sm text-muted-foreground">{s.remarks}</p>}

                  {/* Which trips, and for how much — approving this closes
                      every one of them, so the list is not optional detail. */}
                  <details className="text-sm">
                    <summary className="cursor-pointer text-xs text-muted-foreground">
                      Show the {s.tripCount} trip{s.tripCount === 1 ? "" : "s"} this closes
                    </summary>
                    <div className="max-h-48 space-y-1 overflow-y-auto pt-2">
                      {s.transactions.map((t) => (
                        <div key={t.id} className="flex justify-between gap-3 text-xs">
                          <span className="truncate text-muted-foreground">
                            {t.trip?.tripNo ?? "Trip"}
                          </span>
                          <span className="shrink-0 tabular-nums">{formatCurrency(t.amount)}</span>
                        </div>
                      ))}
                    </div>
                  </details>

                  <Textarea
                    placeholder="Remarks (optional)"
                    value={remarksById[s.id] ?? ""}
                    onChange={(e) => setRemarksById((prev) => ({ ...prev, [s.id]: e.target.value }))}
                  />
                  <div className="flex gap-2">
                    <Button
                      className="flex-1"
                      disabled={approveSettlement.isPending}
                      onClick={() => approveSettlement.mutate(s.id)}
                    >
                      <Check className="h-4 w-4" /> Approve &amp; close
                    </Button>
                    <Button
                      variant="outline"
                      className="flex-1"
                      disabled={rejectSettlement.isPending}
                      onClick={() => rejectSettlement.mutate(s.id)}
                    >
                      <X className="h-4 w-4" /> Reject
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))}
          </div>
        )}

        <div className="space-y-3">
          {pendingQuery.data?.map((t) => (
            <Card key={t.id}>
              <CardContent className="space-y-3 p-4">
                <div className="flex items-start justify-between gap-2">
                  <div>
                    <p className="font-medium">
                      {t.trip?.tripNo} · {t.trip?.vehicle?.regNo}
                    </p>
                    <p className="text-sm text-muted-foreground">
                      {t.trip?.party?.name} · {formatDate(t.date)}
                    </p>
                  </div>
                  <p className="text-lg font-semibold">{formatCurrency(t.amount)}</p>
                </div>
                <p className="text-sm text-muted-foreground">Payment mode: {t.paymentMode}</p>
                <Textarea
                  placeholder="Remarks (optional)"
                  value={remarksById[t.id] ?? ""}
                  onChange={(e) => setRemarksById((prev) => ({ ...prev, [t.id]: e.target.value }))}
                />
                <div className="flex gap-2">
                  <Button
                    className="flex-1"
                    disabled={approveMutation.isPending}
                    onClick={() => approveMutation.mutate(t.id)}
                  >
                    <Check className="h-4 w-4" /> Approve
                  </Button>
                  <Button
                    variant="outline"
                    className="flex-1"
                    disabled={rejectMutation.isPending}
                    onClick={() => rejectMutation.mutate(t.id)}
                  >
                    <X className="h-4 w-4" /> Reject
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
          {pendingQuery.data?.length === 0 && settlementsQuery.data?.length === 0 && (
            <TruckEmpty
              variant="box"
              title="All caught up"
              hint="Nothing is waiting for your approval right now."
            />
          )}
        </div>
      </PageContainer>
    </>
  );
}
