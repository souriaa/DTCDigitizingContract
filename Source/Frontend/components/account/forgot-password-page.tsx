"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { APP_NAME } from "@/lib/application-branding";
import {
  KeyRound,
  Building2,
  User,
  ArrowLeft,
  Loader2,
  Clock,
  CheckCircle2,
  AlertCircle,
  ShieldCheck,
  Send,
  FileText,
} from "lucide-react";
import {
  passwordRecoveryApi,
  recoveryRetrySeconds,
  getRecoveryErrorMessage as getApiErrorMessage,
} from "@/services/password-recovery-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { ThemeToggle } from "@/components/theme-toggle";

export default function ForgotPasswordPage({
  tenant = false,
}: {
  tenant?: boolean;
}) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);
  const lock = useRef(false);
  const read = useRef(false);
  const [seconds, setSeconds] = useState(0);
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (read.current) return;
    read.current = true;
    const query = new URLSearchParams(window.location.search);
    window.history.replaceState(null, "", window.location.pathname);
    queueMicrotask(() => {
      setName((query.get("accountName") ?? "").slice(0, tenant ? 50 : 100));
      setCode((query.get("tenantCode") ?? "").slice(0, 50));
    });
  }, [tenant]);

  useEffect(() => {
    if (seconds <= 0) return;
    const timer = setTimeout(
      () => setSeconds((current) => Math.max(0, current - 1)),
      1000,
    );
    return () => clearTimeout(timer);
  }, [seconds]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (lock.current || seconds > 0) return;
    if (!name.trim() || (tenant && !/^[A-Za-z0-9-]{3,50}$/.test(code.trim()))) {
      setError(
        tenant
          ? "Hãy nhập tên tài khoản và mã tổ chức hợp lệ (3–50 ký tự: chữ, số hoặc dấu gạch ngang)."
          : "Hãy nhập tên tài khoản.",
      );
      return;
    }
    lock.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await passwordRecoveryApi.request(
        name.trim(),
        tenant ? code.trim() : undefined,
      );
      setMessage(result.message);
      setSeconds(result.resendAfterSeconds ?? 60);
    } catch (err) {
      setSeconds(recoveryRetrySeconds(err));
      setError(getApiErrorMessage(err, "Không thể kết nối. Vui lòng thử lại."));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }

  const backToLoginUrl =
    tenant && code.trim()
      ? `/?tenantCode=${encodeURIComponent(code.trim())}`
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
          <div className="w-12 h-12 bg-primary/10 dark:bg-primary/20 text-primary rounded-2xl flex items-center justify-center mx-auto sm:mx-0 shadow-xs ring-1 ring-primary/20 mb-1">
            <KeyRound className="w-6 h-6 text-primary" />
          </div>
          <div className="space-y-1.5">
            <CardTitle className="text-2xl sm:text-3xl font-bold text-foreground tracking-tight">
              Quên mật khẩu
            </CardTitle>
            <CardDescription className="text-muted-foreground text-sm leading-relaxed">
              Nhập thông tin tài khoản của bạn để nhận liên kết khôi phục và
              thiết lập mật khẩu mới an toàn.
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="p-6 sm:p-8 pt-2 space-y-5">
          <form onSubmit={(event) => void submit(event)} className="space-y-4">
            <fieldset disabled={busy || seconds > 0} className="space-y-4">
              {tenant && (
                <div className="space-y-2">
                  <Label
                    htmlFor="tenant-code"
                    className="text-foreground/90 font-medium flex items-center gap-1.5 text-xs sm:text-sm"
                  >
                    <Building2 className="w-3.5 h-3.5 text-muted-foreground" />
                    Mã tổ chức / Mã công ty
                  </Label>
                  <div className="relative">
                    <Input
                      id="tenant-code"
                      value={code}
                      required
                      maxLength={50}
                      autoComplete="organization"
                      onChange={(event) => {
                        setCode(event.target.value);
                        if (error) setError("");
                      }}
                      className="h-11 rounded-xl transition-all focus-visible:ring-2 focus-visible:ring-primary/20 font-medium"
                    />
                  </div>
                  <p className="text-[11px] text-muted-foreground">
                    Từ 3–50 ký tự gồm chữ cái, chữ số hoặc dấu gạch ngang (-).
                  </p>
                </div>
              )}

              <div className="space-y-2">
                <Label
                  htmlFor="account-name"
                  className="text-foreground/90 font-medium flex items-center gap-1.5 text-xs sm:text-sm"
                >
                  <User className="w-3.5 h-3.5 text-muted-foreground" />
                  Tên tài khoản
                </Label>
                <div className="relative">
                  <Input
                    id="account-name"
                    value={name}
                    required
                    maxLength={tenant ? 50 : 100}
                    autoComplete="username"
                    onChange={(event) => {
                      setName(event.target.value);
                      if (error) setError("");
                    }}
                    className="h-11 rounded-xl transition-all focus-visible:ring-2 focus-visible:ring-primary/20 font-medium"
                  />
                </div>
              </div>
            </fieldset>

            <Button
              type="submit"
              disabled={busy || seconds > 0}
              className="w-full h-11 rounded-xl font-semibold shadow-md shadow-primary/20 transition-all hover:shadow-lg hover:shadow-primary/25 active:scale-[0.99] mt-2 cursor-pointer disabled:cursor-not-allowed"
            >
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                  Đang tiếp nhận...
                </>
              ) : seconds > 0 ? (
                <>
                  <Clock className="w-4 h-4 mr-2" />
                  Gửi lại sau {seconds} giây
                </>
              ) : message ? (
                <>
                  <Send className="w-4 h-4 mr-2" />
                  Gửi lại liên kết
                </>
              ) : (
                <>
                  <Send className="w-4 h-4 mr-2" />
                  Gửi liên kết đặt lại mật khẩu
                </>
              )}
            </Button>
          </form>

          {/* Success message banner */}
          {message && (
            <div
              role="status"
              className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 dark:bg-emerald-950/20 p-4 text-emerald-900 dark:text-emerald-200 space-y-2 animate-in fade-in slide-in-from-top-1 duration-200"
            >
              <div className="flex items-start gap-3">
                <CheckCircle2 className="w-5 h-5 text-emerald-600 dark:text-emerald-400 shrink-0 mt-0.5" />
                <div className="space-y-1 text-xs sm:text-sm">
                  <p className="font-semibold text-emerald-900 dark:text-emerald-100">
                    Yêu cầu đã được tiếp nhận
                  </p>
                  <p className="text-emerald-800/90 dark:text-emerald-200/90 leading-relaxed">
                    {message}
                  </p>
                </div>
              </div>
              <div className="pl-8 pt-1 text-xs text-emerald-700 dark:text-emerald-300/80">
                Lưu ý: Hãy kiểm tra cả hộp thư <strong>Spam/Thư rác</strong> nếu
                không thấy email trong hộp thư đến.
              </div>
            </div>
          )}

          {/* Error message banner */}
          {error && (
            <div
              role="alert"
              className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-destructive space-y-1 animate-in fade-in slide-in-from-top-1 duration-200"
            >
              <div className="flex items-start gap-3">
                <AlertCircle className="w-5 h-5 text-destructive shrink-0 mt-0.5" />
                <div className="space-y-0.5 text-xs sm:text-sm">
                  <p className="font-semibold">Không thể gửi yêu cầu</p>
                  <p className="leading-relaxed opacity-90">{error}</p>
                </div>
              </div>
            </div>
          )}

          {/* Security guidance callout */}
          <div className="rounded-xl border border-border/60 bg-muted/40 p-3.5 flex items-start gap-3 text-xs text-muted-foreground leading-relaxed">
            <ShieldCheck className="w-4 h-4 text-primary shrink-0 mt-0.5" />
            <div>
              Liên kết khôi phục chỉ được gửi đến email đã xác minh. Nếu chưa
              cấu hình hoặc mất quyền truy cập email, hãy liên hệ{" "}
              <strong className="text-foreground/80 font-medium">
                {tenant ? "người quản lý tổ chức" : "người vận hành hệ thống"}
              </strong>{" "}
              để được hỗ trợ.
            </div>
          </div>
        </CardContent>

        {/* Footer with Back to Login navigation */}
        <CardFooter className="p-6 sm:p-8 pt-4 pb-6 flex flex-col gap-3 border-t border-border/50 bg-muted/15">
          <Link
            href={backToLoginUrl}
            className="inline-flex items-center justify-center gap-2 text-sm font-medium text-muted-foreground hover:text-foreground transition-colors py-2 rounded-xl hover:bg-muted/50 w-full group"
          >
            <ArrowLeft className="w-4 h-4 transition-transform group-hover:-translate-x-1" />
            Quay lại trang đăng nhập
          </Link>
        </CardFooter>
      </Card>

      {/* Footer copyright note */}
      <p className="mt-8 text-xs text-muted-foreground/70 text-center">
        &copy; {new Date().getFullYear()} DTC.,Ltd. All rights reserved.
      </p>
    </div>
  );
}
