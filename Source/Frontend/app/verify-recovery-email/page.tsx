import type { Metadata } from "next";
import VerifyRecoveryEmailPage from "@/components/account/verify-recovery-email-page";

export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Xác minh email khôi phục", referrer: "no-referrer" };
export default function Page() { return <VerifyRecoveryEmailPage />; }
