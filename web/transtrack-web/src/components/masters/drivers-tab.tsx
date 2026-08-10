"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import type { Driver } from "@/lib/types";
import { Plus, Pencil } from "lucide-react";

export function DriversTab() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Driver | "new" | null>(null);

  const driversQuery = useQuery({
    queryKey: ["drivers"],
    queryFn: () => api.get<Driver[]>("/api/drivers"),
  });

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setEditing("new")}>
          <Plus className="h-4 w-4" /> Add driver
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Code</TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Phone</TableHead>
              <TableHead>Salary</TableHead>
              <TableHead>Status</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {driversQuery.data?.map((d) => (
              <TableRow key={d.id} className="cursor-pointer" onClick={() => setEditing(d)}>
                <TableCell className="font-medium">{d.employeeCode}</TableCell>
                <TableCell>{d.name}</TableCell>
                <TableCell>{d.phone}</TableCell>
                <TableCell>{d.salary}</TableCell>
                <TableCell>
                  <Badge variant={d.isActive ? "success" : "secondary"}>{d.isActive ? "Active" : "Inactive"}</Badge>
                </TableCell>
                <TableCell><Pencil className="h-4 w-4 text-muted-foreground" /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {driversQuery.data?.length === 0 && (
          <p className="p-4 text-sm text-muted-foreground">No drivers yet.</p>
        )}
      </div>

      <DriverDialog
        driver={editing}
        onClose={() => setEditing(null)}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["drivers"] })}
      />
    </div>
  );
}

function DriverDialog({
  driver,
  onClose,
  onSaved,
}: {
  driver: Driver | "new" | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = driver === "new";
  const existing = isNew ? null : driver;

  const [name, setName] = useState(existing?.name ?? "");
  const [phone, setPhone] = useState(existing?.phone ?? "");
  const [salary, setSalary] = useState(existing?.salary?.toString() ?? "");
  const [error, setError] = useState("");

  const [openFor, setOpenFor] = useState(driver);
  if (openFor !== driver) {
    setOpenFor(driver);
    setName(existing?.name ?? "");
    setPhone(existing?.phone ?? "");
    setSalary(existing?.salary?.toString() ?? "");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/drivers", {
        id: existing?.id ?? "00000000-0000-0000-0000-000000000000",
        name,
        phone,
        salary: Number(salary) || 0,
        joiningDate: existing?.joiningDate ?? new Date().toISOString(),
        isActive: existing?.isActive ?? true,
      }),
    onSuccess: () => {
      toast.success("Driver saved.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={driver !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? "Add driver" : `Edit ${existing?.name}`}</DialogTitle>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            mutation.mutate();
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input id="name" value={name} onChange={(e) => setName(e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="phone">Phone</Label>
            <Input id="phone" value={phone} onChange={(e) => setPhone(e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="salary">Salary</Label>
            <Input id="salary" type="number" value={salary} onChange={(e) => setSalary(e.target.value)} />
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending} className="w-full">
              {mutation.isPending ? "Saving…" : "Save"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
