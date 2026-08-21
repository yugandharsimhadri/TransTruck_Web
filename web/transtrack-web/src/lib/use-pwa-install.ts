"use client";

import { useEffect, useState } from "react";

/**
 * Everything the UI needs to offer "add this to your phone", and nothing more.
 *
 * The awkward part of PWA installation is that the three platforms disagree
 * completely: Chrome hands you an event you can trigger later, iOS has no API
 * at all and expects the user to use the Share menu, and plenty of browsers
 * simply can't install. So this resolves to one of a few plain states and lets
 * the component render the right thing — including rendering *nothing*, which
 * is the correct answer more often than not.
 */
export type PwaInstallState =
  /** Still working it out (or server-rendering). Render nothing yet — this
   *  avoids a button that flashes in and then disappears a tick later. */
  | { kind: "checking" }
  /** Already running as an installed app. Nothing to offer. */
  | { kind: "installed" }
  /** Chrome/Edge/Android gave us a real prompt we can fire on a tap. */
  | { kind: "prompt"; promptToInstall: () => Promise<void>; busy: boolean }
  /** iOS Safari: no API exists, so the user is walked through Share → Add. */
  | { kind: "ios-safari" }
  /** iOS in a non-Safari browser, where Add to Home Screen may be missing
   *  entirely. We say so plainly rather than pretending. */
  | { kind: "ios-other" }
  /** Nothing sensible to offer. Render nothing — never a dead button. */
  | { kind: "unsupported" };

/** The event Chromium fires when the app qualifies for installation. Not in
 *  lib.dom, so it's typed here rather than cast to `any` at each use. */
interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{ outcome: "accepted" | "dismissed" }>;
}

// Chromium fires beforeinstallprompt as soon as the page qualifies, which is
// routinely *before* React has mounted anything. A listener added inside a
// component would simply miss it and the button would never appear. So the
// event is captured at module scope — on import, during bundle evaluation —
// and components subscribe to whatever was caught.
let deferredPrompt: BeforeInstallPromptEvent | null = null;
let installed = false;
const subscribers = new Set<() => void>();

const notify = () => subscribers.forEach((fn) => fn());

if (typeof window !== "undefined") {
  window.addEventListener("beforeinstallprompt", (event) => {
    // Suppress Chrome's own mini-infobar so installation happens through the
    // app's button, at a moment the user chose.
    event.preventDefault();
    deferredPrompt = event as BeforeInstallPromptEvent;
    notify();
  });

  window.addEventListener("appinstalled", () => {
    installed = true;
    deferredPrompt = null;
    notify();
  });
}

/** Running as an installed app rather than in a browser tab. The iOS check is
 *  separate because Safari never implemented the display-mode media query for
 *  home-screen apps. */
function isStandalone(): boolean {
  if (typeof window === "undefined") return false;

  const displayMode = window.matchMedia?.("(display-mode: standalone)").matches ?? false;
  const iosStandalone = (window.navigator as Navigator & { standalone?: boolean }).standalone === true;

  return displayMode || iosStandalone;
}

function isIos(): boolean {
  if (typeof navigator === "undefined") return false;

  // iPadOS 13+ reports itself as a Mac, and the only reliable tell is that
  // Macs don't have a touchscreen.
  const iPadOsMasqueradingAsMac = navigator.platform === "MacIntel" && navigator.maxTouchPoints > 1;

  return /iPad|iPhone|iPod/.test(navigator.userAgent) || iPadOsMasqueradingAsMac;
}

/** Add to Home Screen lives in Safari's share sheet; the other iOS browsers
 *  either lack it or hide it somewhere else, so they get different wording. */
function isIosSafari(): boolean {
  if (typeof navigator === "undefined") return false;
  return isIos() && !/CriOS|FxiOS|EdgiOS|OPiOS|mercury/i.test(navigator.userAgent);
}

export function usePwaInstall(): PwaInstallState {
  // Deliberately starts as "checking" on both server and client so the first
  // client render matches the server's, then resolves in the effect below.
  const [state, setState] = useState<PwaInstallState>({ kind: "checking" });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    const resolve = () => {
      if (installed || isStandalone()) {
        setState({ kind: "installed" });
        return;
      }

      if (deferredPrompt) {
        setState({
          kind: "prompt",
          busy: false,
          promptToInstall: async () => {
            const prompt = deferredPrompt;
            if (!prompt) return;

            setBusy(true);
            try {
              await prompt.prompt();
              const { outcome } = await prompt.userChoice;

              // A prompt can only be used once. On acceptance the appinstalled
              // event takes over; on dismissal the offer is simply gone until
              // Chrome decides to fire the event again, so the button goes
              // rather than sitting there doing nothing when tapped.
              deferredPrompt = null;
              if (outcome === "accepted") installed = true;
              notify();
            } catch {
              // A prompt that refuses to open (already consumed, or blocked)
              // shouldn't throw into the UI — just stop offering it.
              deferredPrompt = null;
              notify();
            } finally {
              setBusy(false);
            }
          },
        });
        return;
      }

      if (isIos()) {
        setState({ kind: isIosSafari() ? "ios-safari" : "ios-other" });
        return;
      }

      setState({ kind: "unsupported" });
    };

    resolve();
    subscribers.add(resolve);

    // Catches the case where the app is installed while open, or launched
    // into standalone in the same session.
    const media = window.matchMedia?.("(display-mode: standalone)");
    media?.addEventListener?.("change", resolve);

    return () => {
      subscribers.delete(resolve);
      media?.removeEventListener?.("change", resolve);
    };
  }, []);

  return state.kind === "prompt" ? { ...state, busy } : state;
}
