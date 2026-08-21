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
import { useAuth } from "@/contexts/auth-context";

export default function SettingsPage() {
  // Accountants are allowed in so they can manage their own peers, but only
  // the Users tab — the company's letterhead and bank details stay with the
  // Owner and Co-owner.
  return (
    <RequireRole roles={["Owner", "CoOwner", "Accountant"]}>
      <SettingsScreen />
    </RequireRole>
  );
}

function SettingsScreen() {
  const { user } = useAuth();
  const canManageCompany = user?.role === "Owner" || user?.role === "CoOwner";

  return (
    <PageContainer className="space-y-4">
      <h1 className="text-xl font-semibold">Settings</h1>
      <Tabs defaultValue={canManageCompany ? "company" : "users"}>
        <TabsList>
          {canManageCompany && <TabsTrigger value="company">Company</TabsTrigger>}
          <TabsTrigger value="users">Users</TabsTrigger>
        </TabsList>
        {canManageCompany && <TabsContent value="company"><CompanyTab /></TabsContent>}
        <TabsContent value="users"><UsersTab /></TabsContent>
      </Tabs>
    </PageContainer>
  );
}

function CompanyTab() {
  const queryClient = useQueryClient();
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
  const [bankAccountNo, setBankAccountNo] = useState("");
  const [ifsc, setIfsc] = useState("");
  const [showBankDetailsOnBill, setShowBankDetailsOnBill] = useState(false);
  const [logoBase64, setLogoBase64] = useState<string | null>(null);
  const [logoFileName, setLogoFileName] = useState<string | null>(null);
  const [error, setError] = useState("");

  // Keyed on the row actually fetched rather than a one-shot "have I
  // hydrated yet" flag. That flag meant a remount rehydrated the form from
  // whatever was in the query cache — including a stale copy from before the
  // last save — and then never corrected itself when the real data arrived,
  // which is what made saved details look like they vanished and came back.
  const [hydratedFrom, setHydratedFrom] = useState<string | null>(null);

  useEffect(() => {
    const c = companyQuery.data;
    if (!c) return;

    const stamp = `${c.id}:${c.updatedAt ?? c.createdAt ?? ""}`;
    if (stamp === hydratedFrom) return;

    setCompanyName(c.companyName);
    setTagline(c.tagline ?? "");
    setAddressLine(c.addressLine ?? "");
    setPhone(c.phone ?? "");
    setCell(c.cell ?? "");
    setPan(c.pan ?? "");
    setGstin(c.gstin ?? "");
    setJurisdictionNote(c.jurisdictionNote ?? "");
    setBankAccountNo(c.bankAccountNo ?? "");
    setIfsc(c.ifsc ?? "");
    setShowBankDetailsOnBill(c.showBankDetailsOnBill ?? false);
    setLogoBase64(c.logoBase64 ?? null);
    setLogoFileName(c.logoFileName ?? null);
    setHydratedFrom(stamp);
  }, [companyQuery.data, hydratedFrom]);

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
        bankAccountNo: bankAccountNo || null,
        ifsc: ifsc || null,
        showBankDetailsOnBill,
        logoBase64,
        logoFileName,
      }),
    onSuccess: () => {
      toast.success("Company details saved.");
      // Without this the cache keeps serving the pre-save copy to anything
      // that reads company settings next — including this form on remount.
      queryClient.invalidateQueries({ queryKey: ["masters", "company-settings"] });
    },
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

      <div className="space-y-3 rounded-2xl border p-4">
        <div className="flex items-center justify-between gap-3">
          <div>
            <Label htmlFor="showBank" className="text-base">Print bank details on the Bill</Label>
            <p className="mt-0.5 text-xs text-muted-foreground">
              Off by default. Nothing is printed unless this is on and the fields below are filled in.
            </p>
          </div>
          <button
            id="showBank"
            type="button"
            role="switch"
            aria-checked={showBankDetailsOnBill}
            onClick={() => setShowBankDetailsOnBill((v) => !v)}
            className={`relative h-8 w-14 shrink-0 rounded-full transition-colors ${showBankDetailsOnBill ? "bg-success" : "bg-muted"}`}
          >
            <span className={`absolute top-1 left-1 h-6 w-6 rounded-full bg-white shadow transition-transform ${showBankDetailsOnBill ? "translate-x-6" : "translate-x-0"}`} />
          </button>
        </div>
        <div className="space-y-2">
          <Label className="text-base">Bank account number</Label>
          <Input
            value={bankAccountNo}
            onChange={(e) => setBankAccountNo(e.target.value)}
            className="h-12 text-base"
            inputMode="numeric"
          />
        </div>
        <div className="space-y-2">
          <Label className="text-base">IFSC code</Label>
          <Input
            value={ifsc}
            onChange={(e) => setIfsc(e.target.value.toUpperCase())}
            className="h-12 text-base"
          />
        </div>
      </div>

      {error && <p className="text-sm font-medium text-destructive">{error}</p>}

      <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={mutation.isPending}>
        {mutation.isPending ? "Saving…" : "Save company details"}
      </Button>
    </form>
  );
}

/// Roles are ranked by authority, and nobody may create or edit above their
/// own level: an Owner manages everyone, a Co-owner manages Co-owners and
/// Accountants, an Accountant only other Accountants. The API enforces this
/// too (AuthService.SaveUserAsync) — this just avoids offering a choice that
/// would be refused.
const ROLE_RANK: Record<UserRole, number> = { Owner: 1, CoOwner: 2, Accountant: 3 };

function canManage(actor: UserRole | undefined, target: UserRole): boolean {
  return actor !== undefined && ROLE_RANK[actor] <= ROLE_RANK[target];
}

function UsersTab() {
  const queryClient = useQueryClient();
  const { user: signedIn } = useAuth();
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<UserSummary | null>(null);

  const myRole = signedIn?.role as UserRole | undefined;
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
        {usersQuery.data?.map((u) => {
          // Someone above your level is shown but not editable — hiding them
          // would make the team list look wrong, and opening a form that can
          // only fail is worse than a row that plainly doesn't respond.
          const editable = canManage(myRole, u.role);
          return (
            <Card
              key={u.id}
              className={editable ? "cursor-pointer transition hover:shadow-sm" : "opacity-60"}
              onClick={editable ? () => setEditing(u) : undefined}
            >
              <CardContent className="flex items-center justify-between p-4">
                <div>
                  <p className="font-medium">{u.displayName}</p>
                  <p className="text-sm text-muted-foreground">
                    {u.username} · {u.role}
                    {!editable && " · view only"}
                  </p>
                </div>
                <Badge variant={u.isActive ? "success" : "secondary"}>{u.isActive ? "Active" : "Inactive"}</Badge>
              </CardContent>
            </Card>
          );
        })}
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
  const { user: signedIn } = useAuth();
  const myRole = signedIn?.role as UserRole | undefined;

  // Only the roles this person is allowed to hand out. A Co-owner never sees
  // "Owner" in the list, so the refusal never has to happen.
  const assignableRoles = (["Owner", "CoOwner", "Accountant"] as UserRole[]).filter((r) =>
    canManage(myRole, r),
  );

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
            {assignableRoles.map((r) => (
              <SelectItem key={r} value={r}>
                {r === "CoOwner" ? "Co-owner" : r}
              </SelectItem>
            ))}
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
