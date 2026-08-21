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
import { DocumentPanel } from "@/components/masters/document-panel";
import { DRIVER_DOCUMENT_TYPES, type Driver } from "@/lib/types";
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

      {/* Cards on a phone; the six-column table only above md. */}
      <div className="space-y-2 md:hidden">
        {driversQuery.data?.map((d) => (
          <button
            key={d.id}
            type="button"
            onClick={() => setEditing(d)}
            className="flex w-full items-center gap-3 rounded-2xl border p-3 text-left transition active:scale-[0.99]"
          >
            <div className="min-w-0 flex-1">
              <p className="truncate font-medium">{d.name}</p>
              <p className="truncate text-xs text-muted-foreground">
                {d.employeeCode} · {d.phone}
              </p>
            </div>
            <Badge variant={d.isActive ? "success" : "secondary"} className="shrink-0">
              {d.isActive ? "Active" : "Inactive"}
            </Badge>
            <Pencil className="h-4 w-4 shrink-0 text-muted-foreground" />
          </button>
        ))}
        {driversQuery.data?.length === 0 && (
          <p className="py-4 text-sm text-muted-foreground">No drivers yet.</p>
        )}
      </div>

      <div className="hidden overflow-x-auto rounded-lg border md:block">
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
        onSavedNew={async (id) => {
          // Refetch, then point the dialog at the real saved row so its
          // document upload is reachable without closing and reopening.
          const list = await queryClient.fetchQuery({
            queryKey: ["drivers"],
            queryFn: () => api.get<Driver[]>("/api/drivers"),
          });
          const saved = list.find((d) => d.id === id);
          if (saved) setEditing(saved);
        }}
      />
    </div>
  );
}

function DriverDialog({
  driver,
  onClose,
  onSaved,
  onSavedNew,
}: {
  driver: Driver | "new" | null;
  onClose: () => void;
  onSaved: () => void;
  /** Hands the parent the id of a just-created driver so the dialog can stay
   *  open on it, which is what makes the document upload reachable. */
  onSavedNew: (id: string) => void;
}) {
  const isNew = driver === "new";
  const existing = isNew ? null : driver;

  const [name, setName] = useState(existing?.name ?? "");
  const [phone, setPhone] = useState(existing?.phone ?? "");
  const [salary, setSalary] = useState(existing?.salary?.toString() ?? "");
  const [joiningDate, setJoiningDate] = useState(existing?.joiningDate?.slice(0, 10) ?? "");
  const [error, setError] = useState("");

  const [openFor, setOpenFor] = useState(driver);
  if (openFor !== driver) {
    setOpenFor(driver);
    setName(existing?.name ?? "");
    setPhone(existing?.phone ?? "");
    setSalary(existing?.salary?.toString() ?? "");
    setJoiningDate(existing?.joiningDate?.slice(0, 10) ?? "");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post<string>("/api/drivers", {
        id: existing?.id ?? "00000000-0000-0000-0000-000000000000",
        name,
        phone,
        salary: Number(salary) || 0,
        joiningDate: joiningDate || null,
        isActive: existing?.isActive ?? true,
      }),
    onSuccess: (savedId) => {
      onSaved();

      if (isNew && savedId) {
        toast.success("Driver saved — you can attach documents now.");
        onSavedNew(savedId);
        return;
      }

      toast.success("Driver saved.");
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={driver !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          {/* The null case is not dead: closing sets the value to null while
              the content is still mounted for the close animation, so without a
              fallback the title renders "Edit undefined" on the way out. */}
          <DialogTitle>{isNew ? "Add driver" : existing ? `Edit ${existing.name}` : "Edit driver"}</DialogTitle>
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
          <div className="space-y-2">
            <Label htmlFor="joiningDate">Joining date (optional)</Label>
            <Input id="joiningDate" type="date" value={joiningDate} onChange={(e) => setJoiningDate(e.target.value)} />
          </div>
          {/* Documents hang off a saved driver, same as vehicles — shown
              while adding too, so the capability is visible up front. */}
          <DocumentPanel
            ownerPath="drivers"
            ownerId={existing?.id ?? null}
            types={DRIVER_DOCUMENT_TYPES}
            emptyText="No documents uploaded for this driver yet."
          />

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
