export function formatCurrency(amount: number): string {
  return new Intl.NumberFormat("en-IN", {
    style: "currency",
    currency: "INR",
    maximumFractionDigits: 0,
  }).format(amount);
}

export function formatDate(value: string | null | undefined): string {
  if (!value) return "—";
  return new Date(value).toLocaleDateString("en-IN", { day: "2-digit", month: "short", year: "numeric" });
}

/**
 * A Date as the yyyy-MM-dd an `<input type="date">` expects, read in the
 * viewer's own timezone.
 *
 * `toISOString().slice(0, 10)` is the obvious way to do this and is wrong
 * everywhere east of UTC: it converts to UTC first, so midnight on the 1st in
 * India is 18:30 on the 31st in ISO, and a date defaulted that way is a day
 * early. On a bill period that silently pulls in the previous month's trips.
 */
export function toDateInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Today, in the viewer's timezone. */
export const today = () => toDateInput(new Date());

/** Date plus time — for the audit trail, where two changes on the same day
 *  need to be told apart and put in order. */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) return "—";
  return new Date(value).toLocaleString("en-IN", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
