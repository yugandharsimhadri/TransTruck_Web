"use client";

import { VehiclesTab } from "@/components/masters/vehicles-tab";
import { PageContainer } from "@/components/shell/page-container";

/**
 * The fleet, on its own screen rather than as a tab beside drivers and
 * parties. A lorry now carries a loan, an EMI and its own recurring paperwork
 * costs, so it is somewhere the owner actually works rather than reference
 * data set up once.
 */
export default function VehiclesPage() {
  return (
    <PageContainer className="space-y-4">
      <h1 className="text-xl font-semibold">Vehicles</h1>
      <VehiclesTab />
    </PageContainer>
  );
}
