import type { Metadata, Viewport } from "next";
import "./globals.css";
import { QueryProvider } from "@/lib/query-provider";
import { AuthProvider } from "@/contexts/auth-context";
import { Toaster } from "@/components/ui/sonner";
import { ServiceWorkerRegister } from "@/components/sw-register";
import { ThemeProvider } from "@/components/theme-provider";

export const metadata: Metadata = {
  title: "LorryOwner",
  description: "Fleet & trip management",
  manifest: "/manifest.json",
  // apple must be a PNG: iOS ignores SVG for the home-screen icon, and would
  // otherwise fall back to a screenshot of the page.
  icons: { icon: "/icon.svg", apple: "/apple-touch-icon.png" },
};

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  // Deliberately no maximumScale/userScalable lock: pinch-zoom stays
  // available. The usual reason to lock it is stopping iOS from zooming in
  // when a text field is focused, but that only happens under 16px and every
  // input here is 16px on phones (text-base, dropping to text-sm only at md
  // and up), so the lock would cost accessibility and buy nothing — and this
  // app is explicitly for people who may want to zoom in to read.
  //
  // viewportFit: cover lets the layout paint under the notch/home indicator;
  // the safe-area insets in globals.css keep content clear of both.
  viewportFit: "cover",
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#3B82F6" },
    { media: "(prefers-color-scheme: dark)", color: "#16233f" },
  ],
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" suppressHydrationWarning className="h-full antialiased">
      <body className="min-h-full flex flex-col bg-background text-foreground">
        <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
          <QueryProvider>
            <AuthProvider>
              {children}
              <Toaster richColors position="top-center" />
              <ServiceWorkerRegister />
            </AuthProvider>
          </QueryProvider>
        </ThemeProvider>
      </body>
    </html>
  );
}
