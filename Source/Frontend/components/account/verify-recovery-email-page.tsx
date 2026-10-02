"use client";

import { useEffect, useRef, useState } from "react";
import axios from "axios";
import { recoveryEmailApi } from "@/services/recovery-email-api";
import Link from "next/link";
import { APP_NAME } from "@/lib/application-branding";
import {
  ShieldCheck,
  CheckCircle2,
  AlertCircle,
  Loader2,
  FileText,
  Building2,
  ArrowLeft,
  User,
  LogIn,
  MailCheck,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme-toggle";
import { getApiErrorMessage } from "@/lib/api-error";

export default function VerifyRecoveryEmailPage() {
  const [link, setLink] = useState<{
    requestId: string;
    token: string;
    tenantCode: string | null;
  } | null>(null);
  const read = useRef(false);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState("");

  useEffect(() => {
    // StrictMode replays effects: do not read a fragment that has already been removed.
    if (read.current) return;
    read.current = true;
    const query = new URLSearchParams(window.location.search);
    const fragment = new URLSearchParams(window.location.hash.slice(1));
    const requestId = query.get("requestId");
    const token = fragment.get("token");
    const tenantCode = query.get("tenantCode");
    window.history.replaceState(null, "", window.location.pathname);
    // Publish the external browser-location snapshot after hydration.
    queueMicrotask(() => {
      if (!requestId || !token)
        setError(
          "Liên kết không hợp lệ hoặc đã hết hạn. Nếu bạn đã tải lại trang, vui lòng mở lại liên kết từ email.",
        );
      else setLink({ requestId, token, tenantCode });
    });
  }, []);

  async function verify() {
    if (!link || busy) return;
    setBusy(true);
    setError("");
    try {
      await recoveryEmailApi.verify(link.requestId, link.token);
      setDone(true);
      setLink(null);
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 401)
        setError(
          "Hãy đăng nhập đúng tài khoản, sau đó mở lại liên kết xác minh từ email. Liên kết không được lưu trong trình duyệt.",
        );
      else
        setError(
          getApiErrorMessage(
            err,
            "Không thể xác minh email. Hãy yêu cầu liên kết mới trong phần Hồ sơ tài khoản.",
          ),
        );
    } finally {
      setBusy(false);
    }
  }

  const loginUrl = link?.tenantCode
    ? `/?tenantCode=${encodeURIComponent(link.tenantCode)}`
    : "/";

  return (
    <div className="min-h-screen bg-linear-to-br from-background via-secondary/30 to-primary/5 flex flex-col items-center justify-center p-4 sm:p-6 relative">
      {/* Decorative ambient background glow */}
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-primary/10 rounded-full blur-3xl pointer-events-none -z-10" />

      {/* Top right theme toggle */}
      <div className="absolute top-4 right-4 sm:top-6 sm:right-6">
        <ThemeToggle />
      </div>

      {/* Brand logo header */}
      <div className="flex items-center gap-2.5 mb-6">
        <div className="w-10 h-10 bg-primary rounded-xl flex items-center justify-center shadow-lg shadow-primary/25">
          <FileText className="w-6 h-6 text-primary-foreground" />
        </div>
        <div className="flex flex-col">
          <span className="text-foreground font-bold text-lg tracking-tight leading-none">
            {APP_NAME}
          </span>
          <span className="text-xs text-muted-foreground mt-0.5">
            Quản trị &amp; Số hóa Hợp đồng
          </span>
        </div>
      </div>

      {/* Main card */}
      <Card className="w-full max-w-lg bg-card/95 backdrop-blur-md rounded-2xl shadow-[0_8px_30px_rgb(0,0,0,0.06)] dark:shadow-[0_8px_30px_rgb(0,0,0,0.35)] border-border/80 overflow-hidden py-0 gap-0">
        <CardHeader className="p-6 sm:p-8 pb-4 space-y-3 text-center sm:text-left">
          <div
            className={`w-12 h-12 rounded-2xl flex items-center justify-center mx-auto sm:mx-0 shadow-xs ring-1 mb-1 ${
              done
                ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 ring-emerald-500/20"
                : error && !link
                  ? "bg-destructive/10 text-destructive ring-destructive/20"
                  : "bg-primary/10 dark:bg-primary/20 text-primary ring-primary/20"
            }`}
          >
            {done ? (
              <CheckCircle2 className="w-6 h-6" />
            ) : error && !link ? (
              <AlertCircle className="w-6 h-6" />
            ) : (
              <MailCheck className="w-6 h-6" />
            )}
          </div>

          <div className="space-y-1.5">
            <CardTitle className="text-2xl sm:text-3xl font-bold text-foreground tracking-tight">
              {done
                ? "Xác minh thành công"
                : error && !link
                  ? "Liên kết không hợp lệ"
                  : "Xác minh email khôi phục"}
            </CardTitle>
            <CardDescription className="text-muted-foreground text-sm leading-relaxed">
              {done
                ? "Địa chỉ email khôi phục của bạn đã được kích hoạt thành công trên hệ thống."
                : error && !link
                  ? "Không tìm thấy thông tin xác minh hợp lệ hoặc liên kết đã hết hiệu lực."
                  : "Xác nhận địa chỉ email để kích hoạt cơ chế khôi phục mật khẩu an toàn."}
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="p-6 sm:p-8 pt-2 space-y-5">
          {/* Organization badge if available */}
          {link?.tenantCode && !done && (
            <div className="flex items-center gap-2 p-3 rounded-xl bg-muted/40 border border-border/60 text-xs sm:text-sm">
              <Building2 className="w-4 h-4 text-primary shrink-0" />
              <span className="text-muted-foreground">
                Tổ chức / Công ty:{" "}
                <strong className="font-semibold text-foreground">
                  {link.tenantCode}
                </strong>
              </span>
            </div>
          )}

          {/* Success state display */}
          {done ? (
            <div className="space-y-5">
              <div
                role="status"
                className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 dark:bg-emerald-950/20 p-4 text-emerald-900 dark:text-emerald-200 space-y-2 animate-in fade-in slide-in-from-top-1 duration-200"
              >
                <div className="flex items-start gap-3">
                  <CheckCircle2 className="w-5 h-5 text-emerald-600 dark:text-emerald-400 shrink-0 mt-0.5" />
                  <div className="space-y-1 text-xs sm:text-sm">
                    <p className="font-semibold text-emerald-900 dark:text-emerald-100">
                      Email đã được xác thực an toàn
                    </p>
                    <p className="text-emerald-800/90 dark:text-emerald-200/90 leading-relaxed">
                      Kể từ bây giờ, bạn có thể tự sử dụng chức năng Quên mật
                      khẩu để nhận mã và liên kết đặt lại mật khẩu về email này.
                    </p>
                  </div>
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-1">
                <Link href="/profile" className="w-full">
                  <Button className="w-full h-11 rounded-xl font-semibold gap-2 shadow-sm">
                    <User className="w-4 h-4" />
                    Vào Hồ sơ tài khoản
                  </Button>
                </Link>
                <Link href="/" className="w-full">
                  <Button
                    variant="outline"
                    className="w-full h-11 rounded-xl font-semibold gap-2"
                  >
                    <LogIn className="w-4 h-4" />
                    Quay về
                  </Button>
                </Link>
              </div>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Error state display */}
              {error && (
                <div
                  role="alert"
                  className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-destructive space-y-1 animate-in fade-in slide-in-from-top-1 duration-200"
                >
                  <div className="flex items-start gap-3">
                    <AlertCircle className="w-5 h-5 text-destructive shrink-0 mt-0.5" />
                    <div className="space-y-0.5 text-xs sm:text-sm">
                      <p className="font-semibold">Lưu ý xác minh</p>
                      <p className="leading-relaxed opacity-90">{error}</p>
                    </div>
                  </div>
                </div>
              )}

              {/* Security info note */}
              <div className="rounded-xl border border-border/60 bg-muted/40 p-3.5 flex items-start gap-3 text-xs text-muted-foreground leading-relaxed">
                <ShieldCheck className="w-4 h-4 text-primary shrink-0 mt-0.5" />
                <div>
                  Đăng nhập đúng tài khoản đã yêu cầu xác minh
                  {link?.tenantCode ? ` tại tổ chức ${link.tenantCode}` : ""},
                  sau đó bấm <strong>Xác nhận email</strong>. Nếu chưa đăng
                  nhập, vui lòng đăng nhập trước rồi mở lại email này.
                </div>
              </div>

              {/* Action Button */}
              {link ? (
                <Button
                  disabled={busy}
                  onClick={() => void verify()}
                  className="w-full h-11 rounded-xl font-semibold shadow-md shadow-primary/20 transition-all hover:shadow-lg hover:shadow-primary/25 active:scale-[0.99] gap-2 cursor-pointer disabled:cursor-not-allowed mt-2"
                >
                  {busy ? (
                    <>
                      <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                      Đang xác minh...
                    </>
                  ) : (
                    <>
                      <CheckCircle2 className="w-4 h-4 mr-2" />
                      Xác nhận email
                    </>
                  )}
                </Button>
              ) : (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
                  <Link href={loginUrl} className="w-full">
                    <Button className="w-full h-11 rounded-xl font-semibold gap-2 shadow-sm">
                      <LogIn className="w-4 h-4" />
                      Đăng nhập tài khoản
                    </Button>
                  </Link>
                  <Link href="/profile" className="w-full">
                    <Button
                      variant="outline"
                      className="w-full h-11 rounded-xl font-semibold gap-2"
                    >
                      <User className="w-4 h-4" />
                      Yêu cầu lại trong Hồ sơ
                    </Button>
                  </Link>
                </div>
              )}
            </div>
          )}
        </CardContent>

        {/* Footer with navigation */}
        {!done && link && (
          <CardFooter className="p-6 sm:p-8 pt-4 pb-6 flex items-center justify-between border-t border-border/50 bg-muted/15 text-xs sm:text-sm">
            <Link
              href={loginUrl}
              className="inline-flex items-center gap-1.5 font-medium text-muted-foreground hover:text-foreground transition-colors group"
            >
              <ArrowLeft className="w-3.5 h-3.5 transition-transform group-hover:-translate-x-1" />
              Quay về
            </Link>
            <Link
              href="/profile"
              className="font-medium text-primary hover:text-primary/80 transition-colors"
            >
              Hồ sơ tài khoản
            </Link>
          </CardFooter>
        )}
      </Card>

      {/* Footer copyright note */}
      <p className="mt-8 text-xs text-muted-foreground/70 text-center">
        &copy; {new Date().getFullYear()} DTC.,Ltd. All rights reserved.
      </p>
    </div>
  );
}
