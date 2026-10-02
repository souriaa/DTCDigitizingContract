import type React from "react";
import type { Metadata } from "next";
import { GeistSans } from "geist/font/sans";
import { GeistMono } from "geist/font/mono";
import { SafeAnalytics } from "@/components/safe-analytics";
import { Toaster } from "@/components/ui/sonner";
import { ThemeProvider } from "@/components/theme-provider";
import { SidebarProvider } from "@/components/sidebar-context";
import "./globals.css";

export const metadata: Metadata = {
  title: "System Admin | eContract Hub | Quản lý Hợp đồng",
  description:
    "Nền tảng số hóa, lưu trữ và quản lý hợp đồng doanh nghiệp (Mua, Bán, Phụ lục, Bàn giao).",
  generator: "Next.js",
  icons: {
    icon: [
      {
        url: "/dtc-light-32x32.ico",
        media: "(prefers-color-scheme: light)",
      },
      {
        url: "/dtc-dark-32x32.ico",
        media: "(prefers-color-scheme: dark)",
      },
      {
        url: "/dtc-32x32.ico",
        type: "image/x-icon",
      },
    ],
    apple: "/apple-icon.png",
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
