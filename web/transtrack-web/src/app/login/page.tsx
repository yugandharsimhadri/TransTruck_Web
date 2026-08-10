"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { api, ApiError } from "@/lib/api";
import { useAuth } from "@/contexts/auth-context";
import type { LoginResponse } from "@/lib/types";
import { TruckMark } from "@/components/truck-mark";
import { TruckDrive } from "@/components/truck-drive";
import { ThemeToggle } from "@/components/theme-toggle";

type Stage = "credentials" | "changePassword";

export default function LoginPage() {
  const [stage, setStage] = useState<Stage>("credentials");
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [displayName, setDisplayName] = useState<string | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);

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
          <div className="mb-1 flex h-16 w-16 items-center justify-center rounded-3xl bg-primary text-primary-foreground shadow-md shadow-primary/30">
            <TruckMark className="h-9 w-9" />
          </div>
          <CardTitle className="text-2xl">TransTruck</CardTitle>
          <CardDescription>Fleet &amp; trip management</CardDescription>
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
              {error && <p className="text-sm font-medium text-destructive">{error}</p>}
              <Button type="submit" size="lg" className="h-12 w-full text-base" disabled={busy}>
                {busy ? "Signing in…" : "Sign in"}
              </Button>
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
