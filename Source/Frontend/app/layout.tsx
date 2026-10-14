import type React from "react";
import type { Metadata } from "next";
import { APP_NAME } from "@/lib/application-branding";
import { GeistSans } from "geist/font/sans";
import { GeistMono } from "geist/font/mono";
import { SafeAnalytics } from "@/components/safe-analytics";
import { Toaster } from "@/components/ui/sonner";
import { ThemeProvider } from "@/components/theme-provider";
import { SidebarProvider } from "@/components/sidebar-context";
import "./globals.css";

export const metadata: Metadata = {
  title: `${APP_NAME} | Quản lý Hợp đồng`,
  description:
    "Nền tảng số hóa, lưu trữ và quản lý hợp đồng doanh nghiệp (Mua, Bán, Phụ lục, Bàn giao).",
  generator: "Next.js",
  icons: {
    icon: [
      { url: "/dtc-32x32.png?v=2", type: "image/png", sizes: "32x32" },
      { url: "/dtc-192x192.png?v=2", type: "image/png", sizes: "192x192" },
      {
        url: "/dtc-light-32x32.png?v=2",
        type: "image/png",
        sizes: "32x32",
        media: "(prefers-color-scheme: light)",
      },
      {
        url: "/dtc-dark-32x32.png?v=2",
        type: "image/png",
        sizes: "32x32",
        media: "(prefers-color-scheme: dark)",
      },
    ],
    shortcut: "/dtc-32x32.ico?v=2",
    apple: [
      { url: "/dtc-apple-touch-icon.png?v=2", type: "image/png", sizes: "180x180" },
    ],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="vi" suppressHydrationWarning>
      <body
        className={`${GeistSans.variable} ${GeistMono.variable} font-sans antialiased bg-background text-foreground min-h-screen`}
      >
        <ThemeProvider
          attribute="class"
          defaultTheme="system"
          enableSystem
          disableTransitionOnChange
        >
          <SidebarProvider>{children}</SidebarProvider>

          <Toaster richColors position="top-right" />
        </ThemeProvider>
        <SafeAnalytics />
      </body>
    </html>
  );
}
