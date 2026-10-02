import type { Metadata } from "next";
import RecoveryPage from "@/components/account/forgot-password-page";
export const dynamic = "force-dynamic";
export const metadata: Metadata = {
  title: "Quên mật khẩu",
  referrer: "no-referrer",
};
export default function Page() {
  return <RecoveryPage tenant={true} />;
}
