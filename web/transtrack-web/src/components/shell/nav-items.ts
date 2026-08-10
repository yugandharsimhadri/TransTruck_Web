import {
  LayoutDashboard,
  Truck,
  CheckCircle2,
  Database,
  Wrench,
  BookUser,
  BarChart3,
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

/** The bottom tab bar on mobile — kept to five so it never crowds. */
export const primaryNavItems: NavItem[] = [
  { href: "/dashboard", label: "Dashboard", icon: LayoutDashboard },
  { href: "/trips", label: "Trips", icon: Truck },
  { href: "/approvals", label: "Approvals", icon: CheckCircle2, roles: ["Owner"] },
  { href: "/masters", label: "Masters", icon: Database },
];

/** Everything else — reached via "More" on mobile, shown directly in the
 * desktop sidebar (which has the room for it). */
export const moreNavItems: NavItem[] = [
  { href: "/maintenance", label: "Maintenance", icon: Wrench },
  { href: "/driver-ledger", label: "Driver Ledger", icon: BookUser },
  { href: "/reports", label: "Reports", icon: BarChart3 },
  { href: "/settings", label: "Settings", icon: Settings, roles: ["Owner", "CoOwner"] },
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
