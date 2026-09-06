import {
  HandCoins,
  LayoutDashboard,
  Truck,
  Route,
  Users,
  CheckCircle2,
  Wrench,
  BookUser,
  BarChart3,
  ReceiptText,
  History,
  Settings,
  MoreHorizontal,
  type LucideIcon,
} from "lucide-react";
import type { UserRole } from "@/lib/types";

export interface NavItem {
  href: string;
  label: string;
  icon: LucideIcon;
  /** Omit to show for every role. */
  roles?: UserRole[];
}

/**
 * Navigation order, most-used first:
 *   Dashboard, Trips, Approvals, Maintenance, Driver Ledger, Reports,
 *   Party Bills, Vehicles, Drivers & Parties, Activity, Settings.
 *
 * The desktop sidebar shows all of it in that order; the mobile tab bar takes
 * the top four and puts the rest behind "More", keeping the bar at five slots
 * so it never crowds. Vehicles and Drivers & Parties sit behind "More" with the
 * other reference screens rather than in the tab bar — they are set up once and
 * rarely revisited, unlike recording a day's maintenance.
 *
 * Vehicles is its own destination rather than a tab inside one "masters"
 * screen: a lorry now carries a loan, an EMI and its own running costs, so
 * it is somewhere you go to work, not just a list you set up once.
 */
export const primaryNavItems: NavItem[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/trips", label: "Trips", icon: Route },
  { href: "/approvals", label: "Approvals", icon: CheckCircle2, roles: ["Owner"] },
  { href: "/maintenance", label: "Maintenance", icon: Wrench },
];

/** Everything else — reached via "More" on mobile, shown directly in the
 * desktop sidebar (which has the room for it). */
export const moreNavItems: NavItem[] = [
  { href: "/driver-ledger", label: "Driver Ledger", icon: BookUser },
  { href: "/reports", label: "Reports", icon: BarChart3 },
  { href: "/party-bills", label: "Party Bills", icon: ReceiptText },
  { href: "/settlements", label: "Bulk Settlement", icon: HandCoins },
  { href: "/vehicles", label: "Vehicles", icon: Truck },
  { href: "/masters", label: "Drivers & Parties", icon: Users },
  { href: "/activity", label: "Activity", icon: History, roles: ["Owner", "CoOwner"] },
  { href: "/settings", label: "Settings", icon: Settings },
];

export const moreTabItem: NavItem = { href: "/more", label: "More", icon: MoreHorizontal };

function byRole(items: NavItem[], role: UserRole | null | undefined): NavItem[] {
  return items.filter((item) => !item.roles || (role && item.roles.includes(role)));
}

export function visiblePrimaryNavItems(role: UserRole | null | undefined): NavItem[] {
  return byRole(primaryNavItems, role);
}

export function visibleMoreNavItems(role: UserRole | null | undefined): NavItem[] {
  return byRole(moreNavItems, role);
}

/** Back-compat for the desktop sidebar's flat list. */
export function visibleNavItems(role: UserRole | null | undefined): NavItem[] {
  return [...visiblePrimaryNavItems(role), ...visibleMoreNavItems(role)];
}
