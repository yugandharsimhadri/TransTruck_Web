"use client";

import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { PageContainer } from "@/components/shell/page-container";
import { Sheet, SheetTrigger, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { api, ApiError } from "@/lib/api";
import type { Company, UserSummary, UserRole } from "@/lib/types";
import { Plus, Building2, ImagePlus } from "lucide-react";
import { RequireRole } from "@/components/require-role";

export default function SettingsPage() {
  return (
    <RequireRole roles={["Owner", "CoOwner"]}>
      <SettingsScreen />
    </RequireRole>
  );
}

function SettingsScreen() {
  return (
    <PageContainer className="space-y-4">
      <h1 className="text-xl font-semibold">Settings</h1>
      <Tabs defaultValue="company">
        <TabsList>
          <TabsTrigger value="company">Company</TabsTrigger>
          <TabsTrigger value="users">Users</TabsTrigger>
        </TabsList>
        <TabsContent value="company"><CompanyTab /></TabsContent>
        <TabsContent value="users"><UsersTab /></TabsContent>
      </Tabs>
    </PageContainer>
  );
}

function CompanyTab() {
  const companyQuery = useQuery({
    queryKey: ["masters", "company-settings"],
    queryFn: () => api.get<Company>("/api/masters/company-settings"),
  });

  const [companyName, setCompanyName] = useState("");
  const [tagline, setTagline] = useState("");
  const [addressLine, setAddressLine] = useState("");
  const [phone, setPhone] = useState("");
  const [cell, setCell] = useState("");
  const [pan, setPan] = useState("");
  const [gstin, setGstin] = useState("");
  const [jurisdictionNote, setJurisdictionNote] = useState("");
  const [logoBase64, setLogoBase64] = useState<string | null>(null);
  const [logoFileName, setLogoFileName] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    if (companyQuery.data && !hydrated) {
      const c = companyQuery.data;
      setCompanyName(c.companyName);
      setTagline(c.tagline ?? "");
      setAddressLine(c.addressLine ?? "");
      setPhone(c.phone ?? "");
      setCell(c.cell ?? "");
      setPan(c.pan ?? "");
      setGstin(c.gstin ?? "");
      setJurisdictionNote(c.jurisdictionNote ?? "");
      setLogoBase64(c.logoBase64 ?? null);
      setLogoFileName(c.logoFileName ?? null);
      setHydrated(true);
    }
  }, [companyQuery.data, hydrated]);

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/masters/company-settings", {
        id: companyQuery.data?.id,
        companyName,
        tagline: tagline || null,
        addressLine: addressLine || null,
        phone: phone || null,
        cell: cell || null,
        pan: pan || null,
        gstin: gstin || null,
        jurisdictionNote: jurisdictionNote || null,
        logoBase64,
        logoFileName,
      }),
    onSuccess: () => toast.success("Company details saved."),
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  function onLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      // Strip the data: URL prefix — the backend stores just the base64 payload.
      setLogoBase64(result.split(",")[1] ?? result);
      setLogoFileName(file.name);
    };
    reader.readAsDataURL(file);
  }

  return (
    <form
      className="max-w-lg space-y-5 pt-4"
      onSubmit={(e) => {
        e.preventDefault();
        setError("");
        mutation.mutate();
      }}
    >
      <div className="flex items-center gap-4">
        <div className="flex h-20 w-20 shrink-0 items-center justify-center overflow-hidden rounded-2xl border-2 border-dashed bg-accent text-accent-foreground">
          {logoBase64 ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={`data:image/png;base64,${logoBase64}`} alt="Company logo" className="h-full w-full object-contain" />
          ) : (
            <Building2 className="h-8 w-8" />
          )}
        </div>
        <div>
          <Label htmlFor="logo" className="flex h-11 w-fit cursor-pointer items-center gap-2 rounded-full border px-4 text-sm font-medium hover:bg-accent desktop:h-10">
            <ImagePlus className="h-4 w-4" /> {logoBase64 ? "Change logo" : "Upload logo"}
          </Label>
          <input id="logo" type="file" accept="image/*" className="hidden" onChange={onLogoChange} />
          <p className="mt-1 text-xs text-muted-foreground">Printed on the Bill, LR, and report PDFs.</p>
        </div>
      </div>

      <div className="space-y-2">
        <Label className="text-base">Company name</Label>
        <Input value={companyName} onChange={(e) => setCompanyName(e.target.value)} className="h-12 text-base" required />
      </div>
      <div className="space-y-2">
        <Label className="text-base">Tagline</Label>
        <Input value={tagline} onChange={(e) => setTagline(e.target.value)} className="h-12 text-base" />
      </div>
      <div className="space-y-2">
        <Label className="text-base">Address</Label>
        <Textarea value={addressLine} onChange={(e) => setAddressLine(e.target.value)} />
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label className="text-base">Phone</Label>
          <Input type="tel" value={phone} onChange={(e) => setPhone(e.target.value)} className="h-12 text-base" />
        </div>
        <div className="space-y-2">
          <Label className="text-base">Mobile</Label>
          <Input type="tel" value={cell} onChange={(e) => setCell(e.target.value)} className="h-12 text-base" />
        </div>
      </div>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-2">
          <Label className="text-base">PAN</Label>
          <Input value={pan} onChange={(e) => setPan(e.target.value)} className="h-12 text-base" />
        </div>
        <div className="space-y-2">
          <Label className="text-base">GSTIN</Label>
          <Input value={gstin} onChange={(e) => setGstin(e.target.value)} className="h-12 text-base" />
        </div>
      </div>
      <div className="space-y-2">
        <Label className="text-base">Jurisdiction note (printed on documents)</Label>
        <Textarea value={jurisdictionNote} onChange={(e) => setJurisdictionNote(e.target.value)} />
      </div>

      {error && <p className="text-sm font-medium text-destructive">{error}</p>}

      <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending}>
        {mutation.isPending ? "Saving…" : "Save company details"}
      </Button>
    </form>
  );
}

function UsersTab() {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<UserSummary | null>(null);

  const usersQuery = useQuery({ queryKey: ["users"], queryFn: () => api.get<UserSummary[]>("/api/users") });

  return (
    <div className="space-y-3 pt-4">
      <div className="flex justify-end">
        <Sheet open={open} onOpenChange={setOpen}>
          <SheetTrigger className="flex h-9 items-center gap-1.5 rounded-full bg-primary px-4 text-sm font-medium text-primary-foreground">
            <Plus className="h-4 w-4" /> Add user
          </SheetTrigger>
          <SheetContent side="bottom" className="rounded-t-3xl pb-[calc(2rem+env(safe-area-inset-bottom))]">
            <SheetHeader><SheetTitle>Add a user</SheetTitle></SheetHeader>
            <UserForm
              user={null}
              onSaved={() => {
                setOpen(false);
                queryClient.invalidateQueries({ queryKey: ["users"] });
              }}
            />
          </SheetContent>
        </Sheet>
      </div>

      <div className="space-y-2">
        {usersQuery.data?.map((u) => (
          <Card key={u.id} className="cursor-pointer transition hover:shadow-sm" onClick={() => setEditing(u)}>
            <CardContent className="flex items-center justify-between p-4">
              <div>
                <p className="font-medium">{u.displayName}</p>
                <p className="text-sm text-muted-foreground">{u.username} · {u.role}</p>
              </div>
              <Badge variant={u.isActive ? "success" : "secondary"}>{u.isActive ? "Active" : "Inactive"}</Badge>
            </CardContent>
          </Card>
        ))}
      </div>

      <Sheet open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <SheetContent side="bottom" className="rounded-t-3xl pb-[calc(2rem+env(safe-area-inset-bottom))]">
          <SheetHeader><SheetTitle>Edit {editing?.displayName}</SheetTitle></SheetHeader>
          <UserForm
            user={editing}
            onSaved={() => {
              setEditing(null);
              queryClient.invalidateQueries({ queryKey: ["users"] });
            }}
          />
        </SheetContent>
      </Sheet>
    </div>
  );
}

function UserForm({ user, onSaved }: { user: UserSummary | null; onSaved: () => void }) {
  const isNew = user === null;
  const [username, setUsername] = useState(user?.username ?? "");
  const [displayName, setDisplayName] = useState(user?.displayName ?? "");
  const [role, setRole] = useState<UserRole>(user?.role ?? "Accountant");
  const [isActive, setIsActive] = useState(user?.isActive ?? true);
  const [newPassword, setNewPassword] = useState("");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post("/api/users", {
        id: user?.id ?? "00000000-0000-0000-0000-000000000000",
        username,
        displayName,
        role,
        isActive,
        newPassword: newPassword || null,
      }),
    onSuccess: () => {
      toast.success(isNew ? "User added." : "User updated.");
      onSaved();
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <form
      className="space-y-4 px-4"
      onSubmit={(e) => {
        e.preventDefault();
        setError("");
        mutation.mutate();
      }}
    >
      <div className="space-y-2">
        <Label>Phone number (used as username)</Label>
        <Input
          type="tel"
          inputMode="numeric"
          placeholder="9876543210"
          value={username}
          onChange={(e) => setUsername(e.target.value)}
          className="h-12 text-base"
          required
        />
      </div>
      <div className="space-y-2">
        <Label>Name</Label>
        <Input value={displayName} onChange={(e) => setDisplayName(e.target.value)} className="h-12 text-base" required />
      </div>
      <div className="space-y-2">
        <Label>Role</Label>
        <Select value={role} onValueChange={(v) => v && setRole(v as UserRole)}>
          <SelectTrigger className="h-12 w-full text-base">
            <SelectValue>{(v: UserRole) => v}</SelectValue>
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Owner">Owner</SelectItem>
            <SelectItem value="CoOwner">Co-owner</SelectItem>
            <SelectItem value="Accountant">Accountant</SelectItem>
          </SelectContent>
        </Select>
      </div>
      {!isNew && (
        <div className="flex items-center justify-between rounded-2xl border p-3">
          <Label htmlFor="isActive" className="text-base">Active</Label>
          <input
            id="isActive"
            type="checkbox"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            className="h-5 w-5"
          />
        </div>
      )}
      <div className="space-y-2">
        <Label>{isNew ? "Temporary password" : "Reset password (optional)"}</Label>
        <Input
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          placeholder={isNew ? "Welcome@123" : "Leave blank to keep current password"}
          className="h-12 text-base"
          required={isNew}
        />
      </div>
      {error && <p className="text-sm font-medium text-destructive">{error}</p>}
      <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending}>
        {mutation.isPending ? "Saving…" : isNew ? "Add user" : "Save changes"}
      </Button>
    </form>
  );
}
