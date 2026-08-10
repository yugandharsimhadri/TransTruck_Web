"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { Card, CardContent } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { TripTransaction } from "@/lib/types";
import { Check, X } from "lucide-react";
import { RequireRole } from "@/components/require-role";
import { TruckEmpty } from "@/components/truck-drive";

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

  const [remarksById, setRemarksById] = useState<Record<string, string>>({});

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
    <div className="space-y-4 p-4 sm:p-6">
      <h1 className="text-xl font-semibold">Approvals</h1>

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
        {pendingQuery.data?.length === 0 && (
          <TruckEmpty
            variant="box"
            title="All caught up"
            hint="Nothing is waiting for your approval right now."
          />
        )}
      </div>
    </div>
  );
}
