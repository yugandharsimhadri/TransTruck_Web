"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { useAuth } from "@/contexts/auth-context";
import { AppShell } from "@/components/shell/app-shell";
import { TruckLoading } from "@/components/truck-drive";

export default function AppGroupLayout({ children }: { children: React.ReactNode }) {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) router.replace("/login");
  }, [loading, user, router]);

  if (loading || !user) {
    return <TruckLoading message="Getting your fleet ready…" seed="app-shell" />;
  }

  return <AppShell>{children}</AppShell>;
}
