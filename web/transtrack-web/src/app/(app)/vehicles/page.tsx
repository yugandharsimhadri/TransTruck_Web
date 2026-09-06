"use client";

import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { VehiclesTab } from "@/components/masters/vehicles-tab";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";

/**
 * The fleet, on its own screen rather than as a tab beside drivers and
 * parties. A lorry now carries a loan, an EMI and its own recurring paperwork
 * costs, so it is somewhere the owner actually works rather than reference
 * data set up once.
 */
export default function VehiclesPage() {
  return (
    <>
      <PageHeader
        title="Vehicles"
        backTo="/dashboard"
        actions={
          <Button
            size="sm"
            nativeButton={false}
            render={
              <Link href="/vehicles/new">
                <Plus className="h-4 w-4" /> Add
              </Link>
            }
          />
        }
      />
      <PageContainer className="space-y-4">
        <VehiclesTab />
      </PageContainer>
    </>
  );
}
