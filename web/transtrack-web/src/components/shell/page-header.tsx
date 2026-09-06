"use client";

import Link from "next/link";
import { ArrowLeft, X } from "lucide-react";
import { cn } from "@/lib/utils";

/**
 * The bar every screen wears: a way out on the left, the screen's name beside
 * it, and whatever that screen's main action is on the right.
 *
 * Sticky, deliberately. It used to scroll away with the content, so on a long
 * reports page or a half-filled trip form there was nothing on screen that led
 * anywhere — on a phone the only way back was the browser gesture or the tab
 * bar at the far bottom. A way out that you have to scroll to find is not a way
 * out.
 *
 * Back vs close is a real distinction, not decoration:
 *  - `backTo` is where this screen sits in the hierarchy — a trip belongs to
 *    the trips list, so the arrow points there whatever route you arrived by.
 *  - `closeTo` marks a screen you are *in the middle of*, an entry form, where
 *    leaving means abandoning what you were typing. The cross says that.
 *
 * Screens with neither — only the dashboard — are home; there is nothing above
 * them to return to, and a control leading nowhere is worse than none.
 */
export function PageHeader({
  title,
  subtitle,
  backTo,
  closeTo,
  actions,
  width = "default",
}: {
  title: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Parent screen. Renders a back arrow. */
  backTo?: string;
  /** Where dismissing this screen lands. Renders a close cross instead. */
  closeTo?: string;
  actions?: React.ReactNode;
  width?: "default" | "form";
}) {
  const target = closeTo ?? backTo;
  const Icon = closeTo ? X : ArrowLeft;
  const label = closeTo ? "Close" : "Back";

  return (
    <header className="sticky top-0 z-30 border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/80">
      <div
        className={cn(
          "mx-auto flex w-full items-center gap-2 px-4 py-3 sm:px-6",
          width === "form" ? "max-w-2xl" : "max-w-5xl",
        )}
      >
        {target && (
          <Link
            href={target}
            aria-label={label}
            // 40px square: this is the control someone reaches for one-handed
            // on a phone, so it gets a real tap target rather than a bare icon.
            className="-ml-2 flex h-10 w-10 shrink-0 items-center justify-center rounded-full text-muted-foreground transition hover:bg-accent hover:text-accent-foreground active:scale-95"
          >
            <Icon className="h-5 w-5" />
          </Link>
        )}

        <div className="min-w-0 flex-1">
          <h1 className="truncate text-lg font-semibold leading-tight sm:text-xl">{title}</h1>
          {subtitle && <p className="truncate text-xs text-muted-foreground">{subtitle}</p>}
        </div>

        {actions && <div className="flex shrink-0 items-center gap-2">{actions}</div>}
      </div>
    </header>
  );
}
