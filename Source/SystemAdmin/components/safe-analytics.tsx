"use client";
import { usePathname } from "next/navigation";
import { Analytics } from "@vercel/analytics/next";
export function SafeAnalytics() {
  const pathname = usePathname();
  return ["/verify-recovery-email", "/reset-password", "/forgot-password"].includes(pathname) ? null : <Analytics />;
}

