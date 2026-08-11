"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { api } from "@/lib/api";
import { formatCurrency, formatDate } from "@/lib/format";
import type { DashboardSummary, ComplianceAlert } from "@/lib/types";
import { useAuth } from "@/contexts/auth-context";
import { cn } from "@/lib/utils";
import {
  Truck,
  Receipt,
  ClipboardList,
  AlertTriangle,
  ChevronRight,
  Plus,
  TrendingUp,
} from "lucide-react";

/**
 * The dashboard's job is to answer two questions in the first screenful:
 * "is anything waiting for me?" and "where does the money stand?" — in that
 * order. Everything else is reference.
 *
 * The previous version laid six equal cards in a grid, which made
 * "3 approvals waiting" (work you must do) look exactly like "4 trips this
 * month" (trivia), gave the eye nowhere to land, and was a dead end because
 * nothing was tappable. This version leads with what needs attention, shows
 * it only when it exists, and makes every figure a doorway to the screen
 * that acts on it.
 */
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
  const alerts = alertsQuery.data ?? [];
  const expired = alerts.filter((a) => a.isExpired);

  const thisMonth = new Date().toLocaleDateString("en-IN", { month: "long", year: "numeric" });

  return (
    <div className="space-y-5 p-4 sm:p-6">
      {/* One quiet line of context. The old greeting spent the most valuable
          pixels on a phone telling users their own name. */}
      <div className="flex items-baseline justify-between gap-3">
        <h1 className="truncate text-lg font-semibold">{user?.companyName}</h1>
        <p className="shrink-0 text-xs text-muted-foreground">{thisMonth}</p>
      </div>

      {/* Anything that needs a decision, first — and absent entirely when
          there's nothing to decide, so its presence always means something. */}
      <div className="space-y-2">
        {expired.length > 0 && (
          <AttentionRow
            tone="danger"
            icon={AlertTriangle}
            title={`${expired.length} vehicle ${expired.length === 1 ? "document has" : "documents have"} expired`}
            detail={`${expired[0].regNo} · ${expired[0].document}${expired.length > 1 ? ` and ${expired.length - 1} more` : ""}`}
            href="/masters"
          />
        )}

        {(s?.pendingApprovals ?? 0) > 0 && (
          <AttentionRow
            tone="warning"
            icon={ClipboardList}
            title={`${s!.pendingApprovals} ${s!.pendingApprovals === 1 ? "amount is" : "amounts are"} waiting for approval`}
            detail="Approve or reject to update the trip balances"
            href="/approvals"
          />
        )}
      </div>

      {/* The hero figure: money still owed to the company. It's the number a
          fleet owner opens the app to check.

          Three states, because the balance genuinely can go either way: a
          party can pay an advance larger than the freight, which leaves it
          negative. Rendering that as "Still to collect -₹5,000" reads as a
          bug, so each case gets its own wording. */}
      <Link href="/trips" className="block">
        <Card className="transition active:scale-[0.99]">
          <CardContent className="p-5">
            <div className="flex items-start justify-between gap-3">
              <div className="min-w-0">
                {s ? (
                  <>
                    <p className="text-sm text-muted-foreground">{balanceLabel(s.outstandingBalance)}</p>
                    <p
                      className={cn(
                        "mt-1 text-4xl font-semibold tracking-tight tabular-nums",
                        s.outstandingBalance === 0 && "text-success",
                      )}
                    >
                      {s.outstandingBalance === 0
                        ? "All settled"
                        : formatCurrency(Math.abs(s.outstandingBalance))}
                    </p>
                    <p className="mt-1 text-xs text-muted-foreground">{balanceHint(s.outstandingBalance)}</p>
                  </>
                ) : (
                  <>
                    <p className="text-sm text-muted-foreground">Still to collect</p>
                    <Skeleton className="mt-2 h-10 w-40" />
                  </>
                )}
              </div>
              <ChevronRight className="mt-1 h-5 w-5 shrink-0 text-muted-foreground" />
            </div>
          </CardContent>
        </Card>
      </Link>

      {/* This month, side by side so earned and spent can be compared at a
          glance rather than hunted for in a grid of six. */}
      <div className="grid grid-cols-2 gap-3">
        <MoneyTile
          label="Earned"
          value={s ? formatCurrency(s.revenueThisMonth) : null}
          icon={TrendingUp}
          tone="positive"
          href="/reports"
        />
        <MoneyTile
          label="Spent"
          value={s ? formatCurrency(s.expensesThisMonth) : null}
          icon={Receipt}
          tone="neutral"
          href="/reports"
        />
      </div>

      {/* Reference figures, deliberately quieter than everything above. */}
      <Link href="/trips" className="block">
        <Card className="transition active:scale-[0.99]">
          <CardContent className="flex items-center gap-3 p-4">
            <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent text-accent-foreground">
              <Truck className="h-5 w-5" />
            </span>
            <div className="min-w-0 flex-1">
              <p className="text-sm font-medium">
                {s ? `${s.tripsThisMonth} ${s.tripsThisMonth === 1 ? "trip" : "trips"} this month` : "Trips this month"}
              </p>
              <p className="text-xs text-muted-foreground">Tap to see them all</p>
            </div>
            <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
          </CardContent>
        </Card>
      </Link>

      {/* Expiring-but-not-yet-expired documents: worth knowing, not an
          emergency, so they sit below the money rather than above it. */}
      {alerts.length > expired.length && (
        <Card>
          <CardContent className="space-y-2 p-4">
            <p className="text-sm font-medium">Expiring in the next 30 days</p>
            {alerts
              .filter((a) => !a.isExpired)
              .map((a, i) => (
                <div key={i} className="flex items-center justify-between gap-3 text-sm">
                  <span className="min-w-0 truncate">
                    {a.regNo} · <span className="text-muted-foreground">{a.document}</span>
                  </span>
                  <span className="shrink-0 text-xs text-muted-foreground">{formatDate(a.upto)}</span>
                </div>
              ))}
          </CardContent>
        </Card>
      )}

      {/* The one thing people come here to start. Low on the screen, where a
          thumb naturally rests on a phone. */}
      <Button
        size="lg"
        nativeButton={false}
        className="h-14 w-full text-base font-semibold"
        render={<Link href="/trips/new" />}
      >
        <Plus className="h-5 w-5" /> Book a trip
      </Button>
    </div>
  );
}

/** The outstanding balance is a signed figure, and all three signs happen in
 *  practice — so each gets wording that reads as a fact rather than a fault. */
function balanceLabel(balance: number): string {
  if (balance > 0) return "Still to collect";
  if (balance < 0) return "Collected in advance";
  return "Nothing outstanding";
}

function balanceHint(balance: number): string {
  if (balance > 0) return "Approved payments only — pending ones don't count yet";
  if (balance < 0) return "Parties have paid more than the freight billed so far";
  return "Every trip is paid up";
}

/** A single call to action. Coloured by urgency, and always a link — if it's
 *  worth interrupting someone for, it's worth taking them there. */
function AttentionRow({
  tone,
  icon: Icon,
  title,
  detail,
  href,
}: {
  tone: "danger" | "warning";
  icon: typeof AlertTriangle;
  title: string;
  detail: string;
  href: string;
}) {
  const tones = {
    danger: "border-destructive/30 bg-destructive/10 text-destructive",
    warning: "border-warning/30 bg-warning/10 text-warning",
  };

  return (
    <Link href={href} className="block">
      <div className={cn("flex items-center gap-3 rounded-2xl border p-4 transition active:scale-[0.99]", tones[tone])}>
        <Icon className="h-5 w-5 shrink-0" />
        <div className="min-w-0 flex-1">
          <p className="text-sm font-semibold text-foreground">{title}</p>
          <p className="truncate text-xs text-muted-foreground">{detail}</p>
        </div>
        <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground" />
      </div>
    </Link>
  );
}

function MoneyTile({
  label,
  value,
  icon: Icon,
  tone,
  href,
}: {
  label: string;
  value: string | null;
  icon: typeof Receipt;
  tone: "positive" | "neutral";
  href: string;
}) {
  return (
    <Link href={href} className="block">
      <Card className="h-full transition active:scale-[0.99]">
        <CardContent className="p-4">
          <div className="flex items-center gap-1.5">
            <Icon className={cn("h-4 w-4", tone === "positive" ? "text-success" : "text-muted-foreground")} />
            <p className="text-xs text-muted-foreground">{label}</p>
          </div>
          {value === null ? (
            <Skeleton className="mt-2 h-7 w-20" />
          ) : (
            <p className="mt-1.5 text-xl font-semibold tracking-tight tabular-nums">{value}</p>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
