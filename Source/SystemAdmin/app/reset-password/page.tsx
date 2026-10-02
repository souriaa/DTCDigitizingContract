import type { Metadata } from "next";
import RecoveryPage from "@/components/account/reset-password-page";
export const dynamic = "force-dynamic";
export const metadata: Metadata = { title: "Đặt lại mật khẩu", referrer: "no-referrer" };
export default function Page() { return <RecoveryPage tenant={false} />; }

