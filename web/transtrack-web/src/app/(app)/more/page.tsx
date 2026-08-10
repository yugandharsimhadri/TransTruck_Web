"use client";

import Link from "next/link";
import { ChevronRight, LogOut } from "lucide-react";
import { useAuth } from "@/contexts/auth-context";
import { visibleMoreNavItems } from "@/components/shell/nav-items";

export default function MorePage() {
  const { user, logout } = useAuth();
  const items = visibleMoreNavItems(user?.role);

  return (
    <div className="space-y-6 p-4 sm:p-6">
      <h1 className="text-xl font-semibold">More</h1>

      <div className="overflow-hidden rounded-2xl border">
        {items.map((item, i) => (
          <Link
            key={item.href}
            href={item.href}
            className={`flex items-center gap-3 px-4 py-3.5 transition hover:bg-accent ${i > 0 ? "border-t" : ""}`}
          >
            <div className="flex h-9 w-9 items-center justify-center rounded-full bg-accent text-accent-foreground">
              <item.icon className="h-[18px] w-[18px]" />
            </div>
            <span className="flex-1 font-medium">{item.label}</span>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </Link>
        ))}
      </div>

      <button
        onClick={logout}
        className="flex w-full items-center gap-3 rounded-2xl border px-4 py-3.5 font-medium text-destructive transition hover:bg-destructive/10"
      >
        <LogOut className="h-[18px] w-[18px]" />
        Sign out
      </button>
    </div>
  );
}
