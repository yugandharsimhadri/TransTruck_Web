"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { VehiclesTab } from "@/components/masters/vehicles-tab";
import { DriversTab } from "@/components/masters/drivers-tab";
import { PartiesTab } from "@/components/masters/parties-tab";
import { CitiesStatesTab } from "@/components/masters/cities-states-tab";

export default function MastersPage() {
  return (
    <div className="space-y-4 p-4 sm:p-6">
      <h1 className="text-xl font-semibold">Masters</h1>

      <Tabs defaultValue="vehicles">
        <TabsList>
          <TabsTrigger value="vehicles">Vehicles</TabsTrigger>
          <TabsTrigger value="drivers">Drivers</TabsTrigger>
          <TabsTrigger value="parties">Parties</TabsTrigger>
          <TabsTrigger value="places">Cities & States</TabsTrigger>
        </TabsList>
        <TabsContent value="vehicles"><VehiclesTab /></TabsContent>
        <TabsContent value="drivers"><DriversTab /></TabsContent>
        <TabsContent value="parties"><PartiesTab /></TabsContent>
        <TabsContent value="places"><CitiesStatesTab /></TabsContent>
      </Tabs>
    </div>
  );
}
