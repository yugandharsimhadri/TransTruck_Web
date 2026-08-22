"use client";

import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { useState } from "react";

/**
 * Reference data: the lists a trip is assembled from. These are read on
 * almost every screen — the trip form, the report filters, the masters
 * tabs — and changed only when someone deliberately edits them, at which
 * point the mutation invalidates the key anyway.
 *
 * At the default 30 seconds they were refetched on essentially every
 * navigation, which on a phone means a fresh round trip each time someone
 * moves between screens. Ten minutes keeps them effectively instant without
 * ever showing stale data after an edit, because an edit invalidates rather
 * than waiting for expiry.
 */
const REFERENCE_KEYS = [["vehicles"], ["drivers"], ["parties"], ["cities"], ["states"]];

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const [client] = useState(() => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: 1,
          // Money and trip state stay on a short leash: a stale balance or a
          // missed approval is worse than an extra request.
          staleTime: 30_000,
          // Keep results in memory well past staleness so navigating back to a
          // screen paints from cache immediately and revalidates behind it,
          // rather than showing an empty state and a spinner.
          gcTime: 30 * 60_000,
          refetchOnWindowFocus: false,
        },
      },
    });

    for (const key of REFERENCE_KEYS) {
      queryClient.setQueryDefaults(key, { staleTime: 10 * 60_000, gcTime: 60 * 60_000 });
    }

    return queryClient;
  });

  return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
}
