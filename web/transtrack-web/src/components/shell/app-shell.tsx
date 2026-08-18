"use client";

import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { LogOut } from "lucide-react";
import { cn } from "@/lib/utils";
import { useAuth } from "@/contexts/auth-context";
import { visibleNavItems, visiblePrimaryNavItems, visibleMoreNavItems, moreTabItem } from "./nav-items";
import { BrandMark } from "@/components/brand-logo";
import { TruckAlive } from "@/components/truck-alive";
import { ThemeToggle } from "@/components/theme-toggle";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
  DropdownMenuSeparator,
  DropdownMenuLabel,
} from "@/components/ui/dropdown-menu";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";

export function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const { user, logout } = useAuth();
  const items = visibleNavItems(user?.role);
  const mobileTabItems = [...visiblePrimaryNavItems(user?.role), moreTabItem];
  const moreHrefs = visibleMoreNavItems(user?.role).map((i) => i.href);
  const moreNavItemsMatch = (path: string) => moreHrefs.some((href) => path.startsWith(href));

  return (
    <div className="flex min-h-screen flex-col bg-muted/40 md:flex-row">
      {/* Desktop sidebar — a soft blue-tinted rail like Apple Mail/Notes:
          light in light mode, dark in dark mode, always with the blue
          accent for the active item. */}
      <aside className="hidden w-64 shrink-0 flex-col border-r border-sidebar-border bg-sidebar text-sidebar-foreground md:flex">
        <div className="flex items-center gap-2.5 px-5 py-5">
          <BrandMark className="h-12 w-12" />
          <div className="min-w-0">
            <p className="font-semibold leading-none">LorryOwner</p>
            <p className="truncate text-xs text-sidebar-foreground/60">{user?.companyName}</p>
          </div>
        </div>
        <nav className="flex-1 space-y-1 px-3">
          {items.map((item) => {
            const active = pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                className={cn(
                  "flex items-center gap-3 rounded-2xl px-3.5 py-2.5 text-sm font-medium transition",
                  active
                    ? "bg-sidebar-primary text-sidebar-primary-foreground"
                    : "text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-sidebar-foreground",
                )}
              >
                <item.icon className="h-[18px] w-[18px]" />
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="space-y-1 border-t border-sidebar-border p-3">
          <div className="px-3.5 py-1">
            <ThemeToggle className="text-sidebar-foreground/75 hover:bg-sidebar-accent hover:text-sidebar-foreground" />
          </div>
          <button
            onClick={logout}
            className="flex w-full items-center gap-3 rounded-2xl px-3.5 py-2.5 text-sm font-medium text-sidebar-foreground/75 transition hover:bg-sidebar-accent hover:text-sidebar-foreground"
          >
            <LogOut className="h-[18px] w-[18px]" />
            Sign out
          </button>
        </div>
      </aside>

      {/* Mobile top bar */}
      <header className="flex items-center justify-between border-b bg-background px-4 py-3 md:hidden">
        <div className="flex items-center gap-2">
          <BrandMark className="h-11 w-11" />
          <p className="font-semibold leading-none">LorryOwner</p>
        </div>
        <div className="flex items-center gap-1">
          <ThemeToggle />
          <DropdownMenu>
            <DropdownMenuTrigger>
              <Avatar className="h-9 w-9">
                <AvatarFallback className="bg-accent text-accent-foreground">
                  {(user?.displayName ?? "?").slice(0, 1)}
                </AvatarFallback>
              </Avatar>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              <DropdownMenuLabel>
                <p className="font-medium">{user?.displayName}</p>
                <p className="text-xs font-normal text-muted-foreground">{user?.companyName}</p>
              </DropdownMenuLabel>
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={logout}>
                <LogOut className="h-4 w-4" /> Sign out
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </header>

      <main className="flex-1 pb-[calc(5rem+env(safe-area-inset-bottom))] md:pb-10">{children}</main>

      {/* The heartbeat truck — quiet by default, quicker while a request is
          in flight. Above the tab bar on mobile, along the floor on desktop. */}
      <TruckAlive />

      {/* Mobile bottom tab bar — big, unambiguous tap targets. pb-safe keeps
          the labels clear of the iPhone home indicator. */}
      <nav className="pb-safe fixed inset-x-0 bottom-0 z-40 flex border-t bg-background/95 backdrop-blur md:hidden">
        {mobileTabItems.map((item) => {
          const active =
            item.href === "/more"
              ? pathname === "/more" || moreNavItemsMatch(pathname)
              : pathname.startsWith(item.href);
          return (
            <Link
              key={item.href}
              href={item.href}
              className={cn(
                "flex flex-1 flex-col items-center gap-1 py-2.5 text-[11px] font-medium",
                active ? "text-primary" : "text-muted-foreground",
              )}
            >
              <span className={cn("flex h-8 w-12 items-center justify-center rounded-full", active && "bg-accent")}>
                <item.icon className="h-5 w-5" />
              </span>
              {item.label}
            </Link>
          );
        })}
      </nav>
    </div>
  );
}
