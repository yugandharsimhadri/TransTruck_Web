import {
  Fuel,
  Ticket,
  PackagePlus,
  PackageMinus,
  Wrench,
  ShieldCheck,
  CircleEllipsis,
  Receipt,
  type LucideIcon,
} from "lucide-react";

/**
 * Matches a category's own name (whatever the company typed when they
 * created it — these are free-text masters, not a fixed enum) to a
 * recognizable icon by keyword. Falls back to a generic receipt for
 * anything that doesn't match one of the common trucking-expense terms,
 * so a custom category never renders unlabeled.
 */
export function iconForCategory(name: string): LucideIcon {
  const n = name.toLowerCase();
  if (n.includes("fuel") || n.includes("diesel") || n.includes("petrol")) return Fuel;
  if (n.includes("toll")) return Ticket;
  if (n.includes("unload")) return PackageMinus;
  if (n.includes("load")) return PackagePlus;
  if (n.includes("repair") || n.includes("service") || n.includes("spare")) return Wrench;
  if (n.includes("insurance")) return ShieldCheck;
  if (n.includes("other") || n.includes("misc")) return CircleEllipsis;
  return Receipt;
}
