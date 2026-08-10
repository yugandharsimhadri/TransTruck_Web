"use client";

import { useQuery } from "@tanstack/react-query";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { DashboardSummary, ComplianceAlert } from "@/lib/types";
import { useAuth } from "@/contexts/auth-context";
import {
  Truck,
  IndianRupee,
  Receipt,
  ClipboardList,
  Wallet,
  AlertTriangle,
} from "lucide-react";

export default function DashboardPage() {
  const { user } = useAuth();

  const summaryQuery = useQuery({
    queryKey: ["dashboard", "summary"],
    queryFn: () => api.get<DashboardSummary>("/api/dashboard/summary"),
  });

  const alertsQuery = useQuery({
    queryKey: ["dashboard", "compliance-alerts"],
    queryFn: () => api.get<ComplianceAlert[]>("/api/dashboard/compliance-alerts"),
  });

  const s = summaryQuery.data;

  const cards = [
    { label: "Trips this month", value: s ? String(s.tripsThisMonth) : null, icon: Truck },
    { label: "Revenue this month", value: s ? formatCurrency(s.revenueThisMonth) : null, icon: IndianRupee },
    { label: "Expenses this month", value: s ? formatCurrency(s.expensesThisMonth) : null, icon: Receipt },
    { label: "Pending approvals", value: s ? String(s.pendingApprovals) : null, icon: ClipboardList },
    { label: "Outstanding balance", value: s ? formatCurrency(s.outstandingBalance) : null, icon: Wallet },
    { label: "Vehicles expiring soon", value: s ? String(s.vehiclesExpiringSoon) : null, icon: AlertTriangle },
  ];

  return (
    <div className="space-y-6 p-4 sm:p-6">
      <div>
        <h1 className="text-xl font-semibold">Welcome back, {user?.displayName}</h1>
        <p className="text-sm text-muted-foreground">{user?.companyName}</p>
      </div>

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        {cards.map((c) => (
          <Card key={c.label}>
            <CardContent className="flex items-start justify-between gap-2 p-4">
              <div>
                <p className="text-xs text-muted-foreground">{c.label}</p>
                {c.value === null ? (
                  <Skeleton className="mt-1 h-6 w-16" />
                ) : (
                  <p className="mt-1 text-lg font-semibold">{c.value}</p>
                )}
              </div>
              <c.icon className="h-5 w-5 shrink-0 text-muted-foreground" />
            </CardContent>
          </Card>
        ))}
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Compliance alerts — next 30 days</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2">
          {alertsQuery.isLoading && <Skeleton className="h-10 w-full" />}
          {alertsQuery.data?.length === 0 && (
            <p className="text-sm text-muted-foreground">Nothing expiring soon.</p>
          )}
          {alertsQuery.data?.map((a, i) => (
            <div key={i} className="flex items-center justify-between rounded-lg border p-3 text-sm">
              <div>
                <p className="font-medium">{a.regNo} · {a.document}</p>
                <p className="text-xs text-muted-foreground">{formatDate(a.upto)}</p>
              </div>
              <Badge variant={a.isExpired ? "destructive" : "warning"}>
                {a.isExpired ? "Expired" : "Expiring soon"}
              </Badge>
            </div>
          ))}
        </CardContent>
      </Card>
    </div>
  );
}
