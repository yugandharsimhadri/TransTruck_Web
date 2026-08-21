"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent, SheetHeader, SheetTitle } from "@/components/ui/sheet";
import { usePwaInstall } from "@/lib/use-pwa-install";
import { Smartphone, Share, Plus, Check } from "lucide-react";

/**
 * The "put this on your phone" offer, shown under the sign-in card.
 *
 * It renders nothing at all unless there is something real to offer: already
 * installed, or a browser that can't install, and the card is simply absent.
 * That's the whole design constraint — a button that does nothing when tapped
 * is worse than no button, particularly for the audience here, who will
 * reasonably assume the app is broken rather than that their browser is
 * unsupported.
 *
 * Deliberately not a popup: it sits inline on the sign-in screen, so it's
 * there when someone wants it and silent when they don't.
 */
export function InstallAppCard() {
  const install = usePwaInstall();
  const [showIosSteps, setShowIosSteps] = useState(false);

  // Nothing to say — while still working it out, once installed, or on a
  // browser that can't do this at all.
  if (install.kind === "checking" || install.kind === "installed" || install.kind === "unsupported") {
    return null;
  }

  const isIos = install.kind === "ios-safari" || install.kind === "ios-other";

  return (
    <>
      <section
        aria-labelledby="install-heading"
        className="mt-4 w-full max-w-sm rounded-3xl border bg-card/80 p-5 shadow-sm backdrop-blur"
      >
        <div className="flex items-start gap-3">
          <span
            aria-hidden="true"
            className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-primary/10 text-primary"
          >
            <Smartphone className="h-5 w-5" />
          </span>
          <div className="min-w-0 flex-1">
            <h2 id="install-heading" className="text-base font-semibold tracking-tight">
              {isIos ? "Install LorryOwner on iPhone" : "Install the LorryOwner app"}
            </h2>
            <p className="mt-0.5 text-sm text-muted-foreground">
              {isIos
                ? "Add it to your Home Screen for quick access."
                : "Manage your transport business faster from your phone."}
            </p>
          </div>
        </div>

        {install.kind === "prompt" && (
          <Button
            size="lg"
            className="mt-4 h-12 w-full text-base"
            disabled={install.busy}
            onClick={() => void install.promptToInstall()}
          >
            <Smartphone className="h-5 w-5" />
            {install.busy ? "Opening…" : "Install app"}
          </Button>
        )}

        {install.kind === "ios-safari" && (
          <Button
            size="lg"
            variant="outline"
            className="mt-4 h-12 w-full text-base"
            onClick={() => setShowIosSteps(true)}
          >
            How to install
          </Button>
        )}

        {/* On iOS outside Safari, Add to Home Screen is often missing entirely.
            Saying so is more use than a button that leads nowhere — and an
            automatic hand-off to Safari isn't possible from a web page, so
            it isn't pretended at. */}
        {install.kind === "ios-other" && (
          <p className="mt-3 rounded-2xl bg-muted/60 p-3 text-sm text-muted-foreground">
            For the best installation experience on iPhone, open{" "}
            <span className="font-medium text-foreground">lorryowner.com</span> in Safari, then use
            Share → Add to Home Screen.
          </p>
        )}

        <p className="mt-3 text-xs text-muted-foreground">
          {isIos ? "Works offline for the basics once added." : "Quick access from your home screen."}
        </p>
      </section>

      <Sheet open={showIosSteps} onOpenChange={setShowIosSteps}>
        <SheetContent
          side="bottom"
          className="rounded-t-3xl pb-[calc(2rem+env(safe-area-inset-bottom))]"
        >
          <SheetHeader>
            <SheetTitle>Add LorryOwner to your Home Screen</SheetTitle>
          </SheetHeader>

          <ol className="space-y-3 px-4 pb-2">
            <IosStep number={1} icon={<Share className="h-4 w-4" />}>
              Tap the <span className="font-medium text-foreground">Share</span> button at the bottom
              of Safari.
            </IosStep>
            <IosStep number={2} icon={<Plus className="h-4 w-4" />}>
              Scroll down and tap{" "}
              <span className="font-medium text-foreground">Add to Home Screen</span>.
            </IosStep>
            <IosStep number={3} icon={<Check className="h-4 w-4" />}>
              Tap <span className="font-medium text-foreground">Add</span>. LorryOwner appears on
              your Home Screen like any other app.
            </IosStep>
          </ol>
        </SheetContent>
      </Sheet>
    </>
  );
}

function IosStep({
  number,
  icon,
  children,
}: {
  number: number;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <li className="flex items-start gap-3">
      <span
        aria-hidden="true"
        className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary"
      >
        {icon}
      </span>
      <p className="pt-1 text-sm text-muted-foreground">
        <span className="sr-only">Step {number}. </span>
        {children}
      </p>
    </li>
  );
}
