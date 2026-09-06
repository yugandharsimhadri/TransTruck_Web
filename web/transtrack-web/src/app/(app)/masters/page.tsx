"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { DriversTab } from "@/components/masters/drivers-tab";
import { PartiesTab } from "@/components/masters/parties-tab";
import { CitiesStatesTab } from "@/components/masters/cities-states-tab";
import { PageContainer } from "@/components/shell/page-container";
import { PageHeader } from "@/components/shell/page-header";

/**
 * The people and places a trip is booked against. Vehicles used to be a tab
 * here too and now has its own screen (/vehicles) — a lorry carries a loan, an
 * EMI and its own running costs, which is a place you go to work rather than a
 * list you set up once and forget.
 *
 * Places stays here rather than following Vehicles: a city is reference data
 * for the route, which is the same kind of thing as the party at the end of it.
 */
export default function MastersPage() {
  return (
    <>
      <PageHeader title="Drivers & Parties" backTo="/dashboard" />
      <PageContainer className="space-y-4">
        <Tabs defaultValue="drivers">
          <TabsList>
            <TabsTrigger value="drivers">Drivers</TabsTrigger>
            <TabsTrigger value="parties">Parties</TabsTrigger>
            {/* "Places" rather than "Cities & States": the longer label pushed
                this tab off the edge of a 320px phone. The list scrolls, so it
                was reachable, but a tab you have to discover by swiping is a
                tab most people never find. */}
            <TabsTrigger value="places">Places</TabsTrigger>
          </TabsList>
          <TabsContent value="drivers"><DriversTab /></TabsContent>
          <TabsContent value="parties"><PartiesTab /></TabsContent>
          <TabsContent value="places"><CitiesStatesTab /></TabsContent>
        </Tabs>
      </PageContainer>
    </>
  );
}
