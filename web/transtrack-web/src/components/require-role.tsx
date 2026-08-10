"use client";

import { useAuth } from "@/contexts/auth-context";
import type { UserRole } from "@/lib/types";
import { ShieldAlert } from "lucide-react";

/**
 * Page-level role gate. Hiding a nav link keeps a screen out of sight, but
 * anyone can still type the URL or open an old bookmark — the API rejects
 * those calls (every restricted endpoint carries its own [Authorize] policy),
 * so this is about not showing a broken page full of buttons that can only
 * fail, rather than about stopping the request itself.
 *
 * Kept deliberately simple: no redirect, just an explanation. Bouncing a
 * user somewhere else silently is more confusing than telling them plainly
 * that this screen isn't theirs.
 */
export function RequireRole({
  roles,
  children,
}: {
  roles: UserRole[];
  children: React.ReactNode;
}) {
  const { user } = useAuth();

  if (user && !roles.includes(user.role)) {
    return (
      <div className="flex flex-col items-center justify-center gap-3 p-10 text-center">
        <div className="flex h-14 w-14 items-center justify-center rounded-full bg-muted text-muted-foreground">
          <ShieldAlert className="h-7 w-7" />
        </div>
        <p className="text-base font-semibold">This screen is for the owner</p>
        <p className="max-w-xs text-sm text-muted-foreground">
          Your account doesn&apos;t have access to this section. Ask the owner if you need it.
        </p>
      </div>
    );
  }

  return <>{children}</>;
}
