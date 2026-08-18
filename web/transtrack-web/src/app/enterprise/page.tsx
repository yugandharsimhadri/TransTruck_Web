"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogDescription,
  DialogFooter,
} from "@/components/ui/dialog";
import { api, ApiError } from "@/lib/api";
import type { CompanySummary, CompanyUserSummary, OnboardResult } from "@/lib/types";
import { Plus, LogOut, RotateCw, KeyRound, Phone } from "lucide-react";
import { BrandMark } from "@/components/brand-logo";
import { ThemeToggle } from "@/components/theme-toggle";

export default function EnterprisePage() {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [onboardOpen, setOnboardOpen] = useState(false);
  const [manageCompany, setManageCompany] = useState<CompanySummary | null>(null);
  const [newCredentials, setNewCredentials] = useState<OnboardResult | null>(null);

  const companiesQuery = useQuery({
    queryKey: ["enterprise", "companies"],
    queryFn: () => api.get<CompanySummary[]>("/api/enterprise/companies"),
    retry: false,
  });

  useEffect(() => {
    const err = companiesQuery.error;
    if (err instanceof ApiError && (err.status === 401 || err.status === 403)) {
      router.replace("/login");
    }
  }, [companiesQuery.error, router]);

  async function logout() {
    try {
      await api.post("/api/auth/logout");
    } finally {
      router.push("/login");
    }
  }

  return (
    <div className="min-h-dvh bg-muted/40">
      <header className="flex items-center justify-between border-b bg-background px-4 py-3 sm:px-6">
        <div className="flex items-center gap-2">
          <BrandMark className="h-11 w-11" />
          <div>
            <p className="font-semibold leading-none">LorryOwner</p>
            <p className="text-xs text-muted-foreground">EnterpriseAdmin</p>
          </div>
        </div>
        <div className="flex items-center gap-1">
          <ThemeToggle />
          <Button variant="ghost" size="sm" onClick={logout}>
            <LogOut className="h-4 w-4" /> Sign out
          </Button>
        </div>
      </header>

      <main className="mx-auto max-w-4xl space-y-4 p-4 sm:p-6">
        <div className="flex items-center justify-between">
          <h1 className="text-lg font-semibold">Companies</h1>
          <Button onClick={() => setOnboardOpen(true)}>
            <Plus className="h-4 w-4" /> Onboard company
          </Button>
        </div>

        {companiesQuery.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}

        <div className="grid gap-3 sm:grid-cols-2">
          {companiesQuery.data?.map((c) => (
            <Card key={c.id} className="cursor-pointer transition hover:shadow-md" onClick={() => setManageCompany(c)}>
              <CardHeader className="pb-2">
                <div className="flex items-start justify-between gap-2">
                  <CardTitle className="text-base">{c.companyName}</CardTitle>
                  <Badge variant={c.isLicenseValid ? "success" : "destructive"}>
                    {c.isLicenseValid ? "Active" : "Expired"}
                  </Badge>
                </div>
              </CardHeader>
              <CardContent className="space-y-1 text-sm text-muted-foreground">
                <p>{c.ownerName} · {c.ownerPhone}</p>
                <p>License until {new Date(c.licenseExpiresOn).toLocaleDateString()}</p>
              </CardContent>
            </Card>
          ))}
        </div>

        {companiesQuery.data?.length === 0 && (
          <p className="text-sm text-muted-foreground">No companies onboarded yet.</p>
        )}
      </main>

      <OnboardDialog
        open={onboardOpen}
        onOpenChange={setOnboardOpen}
        onOnboarded={(result) => {
          setOnboardOpen(false);
          setNewCredentials(result);
          queryClient.invalidateQueries({ queryKey: ["enterprise", "companies"] });
        }}
      />

      <CredentialsDialog result={newCredentials} onClose={() => setNewCredentials(null)} />

      <ManageCompanyDialog
        company={manageCompany}
        onClose={() => setManageCompany(null)}
        onLicenseRenewed={() => queryClient.invalidateQueries({ queryKey: ["enterprise", "companies"] })}
      />
    </div>
  );
}

function OnboardDialog({
  open,
  onOpenChange,
  onOnboarded,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onOnboarded: (result: OnboardResult) => void;
}) {
  const [companyName, setCompanyName] = useState("");
  const [ownerName, setOwnerName] = useState("");
  const [ownerPhone, setOwnerPhone] = useState("");
  const [licenseMonths, setLicenseMonths] = useState("12");
  const [error, setError] = useState("");

  const mutation = useMutation({
    mutationFn: () =>
      api.post<OnboardResult>("/api/enterprise/companies", {
        companyName,
        ownerName,
        ownerPhone,
        licenseMonths: Number(licenseMonths) || 12,
      }),
    onSuccess: (result) => {
      setCompanyName("");
      setOwnerName("");
      setOwnerPhone("");
      setLicenseMonths("12");
      setError("");
      onOnboarded(result);
    },
    onError: (err) => setError(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Onboard a company</DialogTitle>
          <DialogDescription>
            Creates the company and its Owner login. The owner signs in with their own phone number —
            password is Welcome@123 the first time, and they'll be asked to change it right away.
          </DialogDescription>
        </DialogHeader>
        <form
          className="space-y-4"
          onSubmit={(e) => {
            e.preventDefault();
            mutation.mutate();
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="companyName">Company name</Label>
            <Input id="companyName" value={companyName} onChange={(e) => setCompanyName(e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="ownerName">Owner name</Label>
            <Input id="ownerName" value={ownerName} onChange={(e) => setOwnerName(e.target.value)} required />
          </div>
          <div className="space-y-2">
            <Label htmlFor="ownerPhone">Owner phone (this becomes their username)</Label>
            <Input
              id="ownerPhone"
              type="tel"
              inputMode="numeric"
              placeholder="9876543210"
              value={ownerPhone}
              onChange={(e) => setOwnerPhone(e.target.value)}
              required
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="licenseMonths">License length (months)</Label>
            <Input
              id="licenseMonths"
              type="number"
              min={1}
              value={licenseMonths}
              onChange={(e) => setLicenseMonths(e.target.value)}
            />
          </div>
          {error && <p className="text-sm font-medium text-destructive">{error}</p>}
          <DialogFooter>
            <Button type="submit" disabled={mutation.isPending} className="w-full">
              {mutation.isPending ? "Onboarding…" : "Onboard company"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}

function CredentialsDialog({ result, onClose }: { result: OnboardResult | null; onClose: () => void }) {
  return (
    <Dialog open={result !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{result?.companyName} onboarded</DialogTitle>
          <DialogDescription>
            Relay these to the owner now — this is the only time the password is shown.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-2 rounded-lg border bg-muted/50 p-4 font-mono text-sm">
          <p>Username: <span className="font-semibold">{result?.ownerUsername}</span></p>
          <p>Temporary password: <span className="font-semibold">{result?.temporaryPassword}</span></p>
        </div>
        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => {
              if (result) {
                navigator.clipboard.writeText(
                  `Username: ${result.ownerUsername}\nTemporary password: ${result.temporaryPassword}`,
                );
                toast.success("Copied to clipboard.");
              }
            }}
          >
            Copy
          </Button>
          <Button onClick={onClose}>Done</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function ManageCompanyDialog({
  company,
  onClose,
  onLicenseRenewed,
}: {
  company: CompanySummary | null;
  onClose: () => void;
  onLicenseRenewed: () => void;
}) {
  const usersQuery = useQuery({
    queryKey: ["enterprise", "companies", company?.id, "users"],
    queryFn: () => api.get<CompanyUserSummary[]>(`/api/enterprise/companies/${company!.id}/users`),
    enabled: company !== null,
  });

  const [renewMonths, setRenewMonths] = useState("12");
  const [resetFor, setResetFor] = useState<CompanyUserSummary | null>(null);
  const [tempPassword, setTempPassword] = useState("Welcome@123");
  const [resetMessage, setResetMessage] = useState("");
  const [renameFor, setRenameFor] = useState<CompanyUserSummary | null>(null);
  const [newPhone, setNewPhone] = useState("");
  const [renameMessage, setRenameMessage] = useState("");
  const queryClient = useQueryClient();

  const renewMutation = useMutation({
    mutationFn: () =>
      api.post<{ licenseExpiresOn: string }>(`/api/enterprise/companies/${company!.id}/renew-license`, {
        months: Number(renewMonths) || 12,
      }),
    onSuccess: () => {
      toast.success("License renewed.");
      onLicenseRenewed();
    },
    onError: (err) => toast.error(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const resetMutation = useMutation({
    mutationFn: () =>
      api.post<{ message: string }>(
        `/api/enterprise/companies/${company!.id}/users/${resetFor!.id}/reset-password`,
        { temporaryPassword: tempPassword },
      ),
    onSuccess: (res) => setResetMessage(res.message),
    onError: (err) => setResetMessage(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  const renameMutation = useMutation({
    mutationFn: () =>
      api.post<{ username: string }>(
        `/api/enterprise/companies/${company!.id}/users/${renameFor!.id}/change-username`,
        { newPhone },
      ),
    onSuccess: (res) => {
      setRenameMessage(`Username is now '${res.username}'.`);
      queryClient.invalidateQueries({ queryKey: ["enterprise", "companies", company?.id, "users"] });
    },
    onError: (err) => setRenameMessage(err instanceof ApiError ? err.message : "Something went wrong."),
  });

  return (
    <Dialog open={company !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>{company?.companyName}</DialogTitle>
          <DialogDescription>{company?.ownerName} · {company?.ownerPhone}</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div className="flex items-end gap-2">
            <div className="flex-1 space-y-2">
              <Label htmlFor="renewMonths">Extend license by (months)</Label>
              <Input
                id="renewMonths"
                type="number"
                min={1}
                value={renewMonths}
                onChange={(e) => setRenewMonths(e.target.value)}
              />
            </div>
            <Button onClick={() => renewMutation.mutate()} disabled={renewMutation.isPending}>
              <RotateCw className="h-4 w-4" /> Renew
            </Button>
          </div>

          <div>
            <p className="mb-2 text-sm font-medium">Users</p>
            <div className="space-y-2">
              {usersQuery.data?.map((u) => (
                <div key={u.id} className="flex items-center justify-between gap-2 rounded-lg border p-2 text-sm">
                  <div className="min-w-0">
                    <p className="font-medium">{u.displayName}</p>
                    <p className="truncate text-xs text-muted-foreground">{u.username} · {u.role}</p>
                  </div>
                  <div className="flex shrink-0 gap-1">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => { setRenameFor(u); setNewPhone(""); setRenameMessage(""); }}
                    >
                      <Phone className="h-4 w-4" />
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => { setResetFor(u); setTempPassword("Welcome@123"); setResetMessage(""); }}
                    >
                      <KeyRound className="h-4 w-4" /> Reset
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      </DialogContent>

      <Dialog open={resetFor !== null} onOpenChange={(open) => !open && setResetFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reset password for {resetFor?.displayName}</DialogTitle>
            <DialogDescription>They must change it again on their next sign-in.</DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={(e) => {
              e.preventDefault();
              resetMutation.mutate();
            }}
          >
            <div className="space-y-2">
              <Label htmlFor="tempPassword">Temporary password (min 8 characters)</Label>
              <Input
                id="tempPassword"
                value={tempPassword}
                onChange={(e) => setTempPassword(e.target.value)}
                minLength={8}
                required
              />
            </div>
            {resetMessage && <p className="text-sm text-muted-foreground">{resetMessage}</p>}
            <DialogFooter>
              <Button type="submit" disabled={resetMutation.isPending} className="w-full">
                {resetMutation.isPending ? "Resetting…" : "Reset password"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      <Dialog open={renameFor !== null} onOpenChange={(open) => !open && setRenameFor(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Update phone number for {renameFor?.displayName}</DialogTitle>
            <DialogDescription>
              Their username is always their phone number — enter the new one to sign in with.
            </DialogDescription>
          </DialogHeader>
          <form
            className="space-y-4"
            onSubmit={(e) => {
              e.preventDefault();
              renameMutation.mutate();
            }}
          >
            <div className="space-y-2">
              <Label htmlFor="newPhone">New phone number</Label>
              <Input
                id="newPhone"
                type="tel"
                inputMode="numeric"
                placeholder="9876543210"
                value={newPhone}
                onChange={(e) => setNewPhone(e.target.value)}
                required
              />
            </div>
            {renameMessage && <p className="text-sm text-muted-foreground">{renameMessage}</p>}
            <DialogFooter>
              <Button type="submit" disabled={renameMutation.isPending} className="w-full">
                {renameMutation.isPending ? "Updating…" : "Update username"}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </Dialog>
  );
}
