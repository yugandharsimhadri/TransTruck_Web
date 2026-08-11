import { cn } from "@/lib/utils";

/**
 * The standard page wrapper: consistent padding, and a max width so content
 * stops stretching on a large monitor.
 *
 * Without it a screen with no constraint let a single card span the whole
 * 1184px column, leaving a money figure alone at the far left of an otherwise
 * empty band — and, worse, the app looked like two different products
 * depending on which screen you were on, since some pages were constrained
 * and some weren't.
 *
 * Two widths, because two kinds of screen exist here:
 *  - "default" (5xl) for lists and dashboards, which use the room.
 *  - "form" (2xl) for anything primarily read or filled in top to bottom,
 *    where a long line length is actively harder to use.
 */
export function PageContainer({
  width = "default",
  className,
  children,
}: {
  width?: "default" | "form";
  className?: string;
  children: React.ReactNode;
}) {
  return (
    <div
      className={cn(
        "mx-auto w-full p-4 sm:p-6",
        width === "form" ? "max-w-2xl" : "max-w-5xl",
        className,
      )}
    >
      {children}
    </div>
  );
}
