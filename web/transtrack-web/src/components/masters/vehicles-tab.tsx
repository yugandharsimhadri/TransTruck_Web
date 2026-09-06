"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api } from "@/lib/api";
import { formatCurrency } from "@/lib/format";
import type { Vehicle } from "@/lib/types";
import { ChevronRight } from "lucide-react";

/**
 * The fleet, as a list you pick from.
 *
 * Selecting a lorry used to open a dialog holding everything about it at once;
 * it now opens that lorry's own screen, where the details, the papers and the
 * loan are separate tabs. The list's job is only to help you find the right
 * one, so each row carries what tells lorries apart — and a loan marker, since
 * "which ones am I still paying for" is a question asked of the list rather
 * than of any single vehicle.
 */
export function VehiclesTab() {
  const vehiclesQuery = useQuery({
    queryKey: ["vehicles"],
    queryFn: () => api.get<Vehicle[]>("/api/vehicles"),
  });

  const vehicles = vehiclesQuery.data ?? [];

  return (
    <div className="space-y-3">
      {/* Cards on a phone, table on a wide screen. A five-column table is
          419px at its narrowest, so on a 320px screen a quarter of it sat off
          the edge behind a sideways scroll — while every other list in the app
          is a stacked card. The table earns its place above md, where the
          density genuinely helps scanning a long fleet. */}
      <div className="space-y-2 md:hidden">
        {vehicles.map((v) => (
          <Link
            key={v.id}
            href={`/vehicles/${v.id}`}
            className="flex w-full items-center gap-3 rounded-2xl border p-3 text-left transition active:scale-[0.99]"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">{v.regNo}</p>
              <p className="truncate text-xs text-muted-foreground">
                {v.vehicleType ?? "No type"} · {v.ownership === "Own" ? "Own" : v.owner?.name ?? "Other owner"}
                {v.hasLoan ? ` · EMI ${formatCurrency(v.emiAmount ?? 0)}` : ""}
              </p>
            </div>
            <Badge variant={v.isActive ? "success" : "secondary"} className="shrink-0">
              {v.isActive ? "Active" : "Inactive"}
            </Badge>
            <ChevronRight aria-hidden="true" className="h-5 w-5 shrink-0 text-muted-foreground" />
          </Link>
        ))}
        {vehicles.length === 0 && !vehiclesQuery.isLoading && (
          <p className="py-4 text-sm text-muted-foreground">No vehicles yet.</p>
        )}
      </div>

      <div className="hidden overflow-x-auto rounded-lg border md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Reg No</TableHead>
              <TableHead>Type</TableHead>
              <TableHead>Ownership</TableHead>
              <TableHead>Loan</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {vehicles.map((v) => (
              <TableRow key={v.id}>
                <TableCell className="font-medium">
                  <Link href={`/vehicles/${v.id}`} className="block hover:underline">
                    {v.regNo}
                  </Link>
                </TableCell>
                <TableCell>{v.vehicleType ?? "—"}</TableCell>
                <TableCell>{v.ownership === "Own" ? "Own" : v.owner?.name ?? "Other"}</TableCell>
                <TableCell className="text-muted-foreground">
                  {v.hasLoan ? `EMI ${formatCurrency(v.emiAmount ?? 0)}` : "—"}
                </TableCell>
                <TableCell>
                  <Badge variant={v.isActive ? "success" : "secondary"}>{v.isActive ? "Active" : "Inactive"}</Badge>
                </TableCell>
                <TableCell>
                  <Link href={`/vehicles/${v.id}`} aria-label={`Open ${v.regNo}`}>
                    <ChevronRight className="h-4 w-4 text-muted-foreground" />
                  </Link>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {vehicles.length === 0 && !vehiclesQuery.isLoading && (
          <p className="p-4 text-sm text-muted-foreground">No vehicles yet.</p>
        )}
      </div>
    </div>
  );
}
