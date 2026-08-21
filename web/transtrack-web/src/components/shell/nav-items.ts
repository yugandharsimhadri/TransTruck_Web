import {
  LayoutDashboard,
  Truck,
  CheckCircle2,
  Database,
  Wrench,
  BookUser,
  BarChart3,
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
 *   Vehicles & Contacts, Activity, Settings.
 *
 * The desktop sidebar shows all of it in that order; the mobile tab bar takes
 * the top four and puts the rest behind "More", keeping the bar at five slots
 * so it never crowds. Vehicles & Contacts sits with the other reference screens
 * than in the tab bar — it's set up once and rarely revisited, unlike
 * recording a day's maintenance.
 */
export const primaryNavItems: NavItem[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/trips", label: "Trips", icon: Truck },
  { href: "/approvals", label: "Approvals", icon: CheckCircle2, roles: ["Owner"] },
  { href: "/maintenance", label: "Maintenance", icon: Wrench },
];

/** Everything else — reached via "More" on mobile, shown directly in the
 * desktop sidebar (which has the room for it). */
export const moreNavItems: NavItem[] = [
  { href: "/driver-ledger", label: "Driver Ledger", icon: BookUser },
  { href: "/reports", label: "Reports", icon: BarChart3 },
  { href: "/masters", label: "Vehicles & Contacts", icon: Database },
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
