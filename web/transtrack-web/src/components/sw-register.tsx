"use client";

import { useEffect } from "react";

export function ServiceWorkerRegister() {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production") return;
    if ("serviceWorker" in navigator) {
      navigator.serviceWorker.register("/sw.js").catch(() => {
        // Offline shell is a nice-to-have — a failed registration
        // (unsupported browser, blocked, etc.) shouldn't be fatal.
      });
    }
  }, []);

  return null;
}
