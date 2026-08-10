"use client";

import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import type { Party } from "@/lib/types";
import { Plus, Pencil } from "lucide-react";

export function PartiesTab() {
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState<Party | "new" | null>(null);

  const partiesQuery = useQuery({
    queryKey: ["parties"],
    queryFn: () => api.get<Party[]>("/api/masters/parties"),
  });

  return (
    <div className="space-y-3">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setEditing("new")}>
          <Plus className="h-4 w-4" /> Add party
        </Button>
      </div>

      <div className="overflow-x-auto rounded-lg border">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Name</TableHead>
              <TableHead>Phone</TableHead>
              <TableHead>GSTIN</TableHead>
              <TableHead className="w-10" />
            </TableRow>
          </TableHeader>
          <TableBody>
            {partiesQuery.data?.map((p) => (
              <TableRow key={p.id} className="cursor-pointer" onClick={() => setEditing(p)}>
                <TableCell className="font-medium">{p.name}</TableCell>
                <TableCell>{p.phone ?? "—"}</TableCell>
                <TableCell>{p.gstin ?? "—"}</TableCell>
                <TableCell><Pencil className="h-4 w-4 text-muted-foreground" /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        {partiesQuery.data?.length === 0 && (
          <p className="p-4 text-sm text-muted-foreground">No parties yet.</p>
        )}
      </div>

      <PartyDialog
        party={editing}
        onClose={() => setEditing(null)}
        onSaved={() => queryClient.invalidateQueries({ queryKey: ["parties"] })}
      />
    </div>
  );
}

function PartyDialog({
  party,
  onClose,
  onSaved,
}: {
  party: Party | "new" | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const isNew = party === "new";
  const existing = isNew ? null : party;

  const [name, setName] = useState(existing?.name ?? "");
  const [phone, setPhone] = useState(existing?.phone ?? "");
  const [gstin, setGstin] = useState(existing?.gstin ?? "");
  const [error, setError] = useState("");

  const [openFor, setOpenFor] = useState(party);
  if (openFor !== party) {
    setOpenFor(party);
    setName(existing?.name ?? "");
    setPhone(existing?.phone ?? "");
    setGstin(existing?.gstin ?? "");
    setError("");
  }

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/masters/parties", {
        id: existing?.id ?? "00000000-0000-0000-0000-000000000000",
        name,
        phone: phone || null,
        gstin: gstin || null,
      }),
    onSuccess: () => {
      toast.success("Party saved.");
      onSaved();
      onClose();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={party !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isNew ? "Add party" : `Edit ${existing?.name}`}</DialogTitle>
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
            <Input id="phone" value={phone} onChange={(e) => setPhone(e.target.value)} />
          </div>
          <div className="space-y-2">
            <Label htmlFor="gstin">GSTIN</Label>
            <Input id="gstin" value={gstin} onChange={(e) => setGstin(e.target.value)} />
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
