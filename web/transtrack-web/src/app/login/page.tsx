"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardDescription } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/auth-context";
import type { LoginResponse } from "@/lib/types";
import { BrandLogo } from "@/components/brand-logo";
import { TruckDrive } from "@/components/truck-drive";
import { ThemeToggle } from "@/components/theme-toggle";

type Stage = "credentials" | "changePassword" | "register";

/// Pre-filled on the registration form and accepted if left as-is — the new
/// account is forced through a password change on its first sign-in either
/// way, so this is a starting point rather than a lasting password.
const DEFAULT_PASSWORD = "Welcome@123";

export default function LoginPage() {
  const [stage, setStage] = useState<Stage>("credentials");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [displayName, setDisplayName] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const [busy, setBusy] = useState(false);

  // Registration form
  const [regCompanyName, setRegCompanyName] = useState("");
  const [regPhone, setRegPhone] = useState("");
  const [regPassword, setRegPassword] = useState(DEFAULT_PASSWORD);

  const router = useRouter();
  const { refresh } = useAuth();

  async function submitCredentials(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (!username.trim() || !password) {
      setError("Enter your phone number and password.");
      return;
    }

    setBusy(true);
    try {
      const result = await api.post<LoginResponse>("/api/auth/login", { username, password });

      if (result.status === "MustChangePassword") {
        setDisplayName(result.displayName);
        setStage("changePassword");
      } else if (result.status === "Recovery") {
        router.push("/enterprise");
      } else {
        await refresh();
        router.push("/dashboard");
      }
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Try again.");
    } finally {
      setBusy(false);
    }
  }

  async function submitRegistration(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (!regCompanyName.trim()) {
      setError("Enter your company name.");
      return;
    }
    if (regPhone.replace(/\D/g, "").length !== 10) {
      setError("Enter a 10-digit mobile number.");
      return;
    }
    if (regPassword.length < 8) {
      setError("The password must be at least 8 characters.");
      return;
    }

    setBusy(true);
    try {
      const result = await api.post<{ username: string; companyName: string; message: string }>(
        "/api/auth/register",
        { companyName: regCompanyName, phone: regPhone, password: regPassword },
      );

      // Registration is not a sign-in: hand them back to the login form with
      // the number already filled in, since the account still has to go
      // through its forced password change.
      setUsername(result.username);
      setPassword("");
      setRegCompanyName("");
      setRegPhone("");
      setRegPassword(DEFAULT_PASSWORD);
      setNotice(result.message);
      setStage("credentials");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Try again.");
    } finally {
      setBusy(false);
    }
  }

  async function submitNewPassword(e: React.FormEvent) {
    e.preventDefault();
    setError("");

    if (newPassword.length < 8) {
      setError("The new password must be at least 8 characters.");
      return;
    }
    if (newPassword !== confirmPassword) {
      setError("The passwords do not match.");
      return;
    }

    setBusy(true);
    try {
      await api.post<LoginResponse>("/api/auth/change-password", { newPassword, confirmPassword });
      await refresh();
      router.push("/dashboard");
    } catch (err) {
      setError(err instanceof ApiError ? err.message : "Something went wrong. Try again.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="relative flex min-h-screen items-center justify-center overflow-hidden bg-gradient-to-b from-accent/60 via-background to-background p-4">
      <div className="absolute top-3 right-3">
        <ThemeToggle />
      </div>

      {/* Truck ambling along the foot of the sign-in screen — the first
          thing that says this app is about moving goods. */}
      <div className="absolute inset-x-0 bottom-0">
        <TruckDrive seed="login" speed="slow" size="md" />
      </div>

      <Card className="w-full max-w-sm border-none shadow-lg shadow-primary/5">
        <CardHeader className="items-center text-center">
          {/* The full logo already carries the name and the tagline, so no
              separate title line — repeating it would just say it twice. */}
          <BrandLogo className="h-auto w-56" />
          <CardDescription className="sr-only">LorryOwner — fleet &amp; trip management</CardDescription>
        </CardHeader>
        <CardContent>
          {stage === "credentials" && (
            <form onSubmit={submitCredentials} className="space-y-5">
              <div className="space-y-2">
                <Label htmlFor="username" className="text-base">Phone number</Label>
                <Input
                  id="username"
                  autoFocus
                  type="tel"
                  inputMode="numeric"
                  placeholder="9876543210"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  autoComplete="username"
                  className="h-12 text-base"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="password" className="text-base">Password</Label>
                <Input
                  id="password"
                  type="password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  className="h-12 text-base"
                />
              </div>
              {notice && <p className="text-sm font-medium text-success">{notice}</p>}
              {error && <p className="text-sm font-medium text-destructive">{error}</p>}
              <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={busy}>
                {busy ? "Signing in…" : "Sign in"}
              </Button>
              <div className="text-center text-sm text-muted-foreground">
                New to LorryOwner?{" "}
                <button
                  type="button"
                  className="font-medium text-primary underline-offset-4 hover:underline"
                  onClick={() => {
                    setError("");
                    setNotice("");
                    setStage("register");
                  }}
                >
                  Register your company
                </button>
              </div>
            </form>
          )}

          {stage === "register" && (
            <form onSubmit={submitRegistration} className="space-y-5">
              <p className="text-sm text-muted-foreground">
                Create your company. Your phone number becomes your login.
              </p>
              <div className="space-y-2">
                <Label htmlFor="regCompanyName" className="text-base">Company name</Label>
                <Input
                  id="regCompanyName"
                  autoFocus
                  value={regCompanyName}
                  onChange={(e) => setRegCompanyName(e.target.value)}
                  className="h-12 text-base"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="regPhone" className="text-base">Phone number</Label>
                <Input
                  id="regPhone"
                  type="tel"
                  inputMode="numeric"
                  placeholder="9876543210"
                  value={regPhone}
                  onChange={(e) => setRegPhone(e.target.value)}
                  className="h-12 text-base"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="regPassword" className="text-base">Password</Label>
                <Input
                  id="regPassword"
                  value={regPassword}
                  onChange={(e) => setRegPassword(e.target.value)}
                  autoComplete="new-password"
                  className="h-12 text-base"
                />
                <p className="text-xs text-muted-foreground">
                  You&apos;ll be asked to set your own password the first time you sign in.
                </p>
              </div>
              {error && <p className="text-sm font-medium text-destructive">{error}</p>}
              <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={busy}>
                {busy ? "Registering…" : "Register"}
              </Button>
              <div className="text-center text-sm text-muted-foreground">
                Already registered?{" "}
                <button
                  type="button"
                  className="font-medium text-primary underline-offset-4 hover:underline"
                  onClick={() => {
                    setError("");
                    setStage("credentials");
                  }}
                >
                  Sign in
                </button>
              </div>
            </form>
          )}

          {stage === "changePassword" && (
            <form onSubmit={submitNewPassword} className="space-y-5">
              <p className="text-sm text-muted-foreground">
                {displayName ? `Welcome, ${displayName}. ` : ""}
                Set a new password to continue.
              </p>
              <div className="space-y-2">
                <Label htmlFor="newPassword" className="text-base">New password</Label>
                <Input
                  id="newPassword"
                  type="password"
                  autoFocus
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  autoComplete="new-password"
                  className="h-12 text-base"
                />
              </div>
              <div className="space-y-2">
                <Label htmlFor="confirmPassword" className="text-base">Confirm new password</Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  autoComplete="new-password"
                  className="h-12 text-base"
                />
              </div>
              {error && <p className="text-sm font-medium text-destructive">{error}</p>}
              <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={busy}>
                {busy ? "Saving…" : "Set password and continue"}
              </Button>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
