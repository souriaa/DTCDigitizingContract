"use client";

import { useEffect, useRef, useState } from "react";
import axios from "axios";
import Link from "next/link";
import { APP_NAME } from "@/lib/application-branding";
import {
  KeyRound,
  CheckCircle2,
  AlertCircle,
  Loader2,
  FileText,
  Building2,
  ArrowLeft,
  LogIn,
  RotateCcw,
  ShieldCheck,
} from "lucide-react";
import {
  passwordRecoveryApi,
  RecoveryLink,
  getRecoveryErrorMessage as getApiErrorMessage,
} from "@/services/password-recovery-api";
import {
  isValidAccountPassword,
  PASSWORD_POLICY_MESSAGE,
} from "@/lib/password-policy";
import { NewPasswordFields } from "@/components/account/new-password-fields";
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

export default function ResetPasswordPage({
  tenant = false,
}: {
  tenant?: boolean;
}) {
  const [link, setLink] = useState<RecoveryLink | null>(null);
  const read = useRef(false);
  const lock = useRef(false);
  const [valid, setValid] = useState(false);
  const [checking, setChecking] = useState(true);
  const [validationAttempt, setValidationAttempt] = useState(0);
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    if (read.current) return;
    read.current = true;
    const query = new URLSearchParams(window.location.search);
    const fragment = new URLSearchParams(window.location.hash.slice(1));
    const requestId = query.get("requestId");
    const token = fragment.get("token");
    const tenantCode = query.get("tenantCode") ?? "";
    // Remove token/identifiers before any API call. No cookies or browser storage are used.
    window.history.replaceState(null, "", window.location.pathname);
    queueMicrotask(() => {
      setCode(tenantCode);
      if (
        !requestId ||
        !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
          requestId,
        ) ||
        !token ||
        !/^[A-Za-z0-9_-]{43}$/.test(token) ||
        (tenant && !/^[A-Za-z0-9-]{3,50}$/.test(tenantCode))
      ) {
        setChecking(false);
        setError(
          "Liên kết không hợp lệ hoặc đã hết hạn. Nếu bạn đã tải lại trang, vui lòng mở lại liên kết từ email.",
        );
      } else setLink({ requestId, token, ...(tenant ? { tenantCode } : {}) });
    });
  }, [tenant]);

  useEffect(() => {
    if (!link) return;
    const controller = new AbortController();
    queueMicrotask(() => {
      if (!controller.signal.aborted) {
        setChecking(true);
        setError("");
      }
    });
    void passwordRecoveryApi
      .validate(link, controller.signal)
      .then(() => {
        if (!controller.signal.aborted) {
          setValid(true);
          setChecking(false);
        }
      })
      .catch((err) => {
        if (controller.signal.aborted || axios.isCancel(err)) return;
        setChecking(false);
        setValid(false);
        setError(
          getApiErrorMessage(
            err,
            "Không thể kiểm tra liên kết. Vui lòng thử lại.",
          ),
        );
        if (
          axios.isAxiosError(err) &&
          err.response?.data?.code === "PasswordRecoveryLinkInvalid"
        )
          setLink(null);
      });
    return () => controller.abort();
  }, [link, validationAttempt]);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!link || !valid || lock.current) return;
    if (!isValidAccountPassword(password)) {
      setError(PASSWORD_POLICY_MESSAGE);
      return;
    }
    if (password !== confirm) {
      setError("Mật khẩu nhập lại chưa khớp.");
      return;
    }
    lock.current = true;
    setBusy(true);
    setError("");
    try {
      await passwordRecoveryApi.reset(link, password, confirm);
      setDone(true);
      setLink(null);
      setValid(false);
      setPassword("");
      setConfirm("");
      // Do not logout a different tenant/sysadmin session sharing the same browser.
    } catch (err) {
      setError(
        getApiErrorMessage(
          err,
          "Không thể đặt lại mật khẩu. Vui lòng thử lại.",
        ),
      );
      if (
        axios.isAxiosError(err) &&
        err.response?.data?.code === "PasswordRecoveryLinkInvalid"
      ) {
        setLink(null);
        setValid(false);
        setPassword("");
        setConfirm("");
      }
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }

  const loginQuery = new URLSearchParams({
    password_reset: "1",
    ...(tenant && code ? { tenantCode: code } : {}),
  });

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
                : checking
                  ? "bg-primary/10 text-primary ring-primary/20"
                  : valid
                    ? "bg-primary/10 dark:bg-primary/20 text-primary ring-primary/20"
                    : "bg-destructive/10 text-destructive ring-destructive/20"
            }`}
          >
            {done ? (
              <CheckCircle2 className="w-6 h-6" />
            ) : checking ? (
              <Loader2 className="w-6 h-6 animate-spin" />
            ) : valid ? (
              <KeyRound className="w-6 h-6" />
            ) : (
              <AlertCircle className="w-6 h-6" />
            )}
          </div>

          <div className="space-y-1.5">
            <CardTitle className="text-2xl sm:text-3xl font-bold text-foreground tracking-tight">
              {done
                ? "Đặt lại mật khẩu thành công"
                : checking
                  ? "Kiểm tra liên kết..."
                  : valid
                    ? "Đặt lại mật khẩu"
                    : "Liên kết không hợp lệ"}
            </CardTitle>
            <CardDescription className="text-muted-foreground text-sm leading-relaxed">
              {done
                ? "Mật khẩu của bạn đã được cập nhật an toàn. Mọi phiên đăng nhập cũ đã được vô hiệu hóa."
                : checking
                  ? "Hệ thống đang kiểm tra tính hợp lệ của liên kết đặt lại mật khẩu..."
                  : valid
                    ? "Thiết lập mật khẩu mới cho tài khoản của bạn để tiếp tục sử dụng hệ thống."
                    : "Không thể xác thực liên kết này. Liên kết có thể đã được sử dụng hoặc đã hết hạn."}
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="p-6 sm:p-8 pt-2 space-y-5">
          {/* Organization badge if available */}
          {tenant && code && (
            <div className="flex items-center gap-2 p-3 rounded-xl bg-muted/40 border border-border/60 text-xs sm:text-sm">
              <Building2 className="w-4 h-4 text-primary shrink-0" />
              <span className="text-muted-foreground">
                Tổ chức / Công ty:{" "}
                <strong className="font-semibold text-foreground">
                  {code}
                </strong>
              </span>
            </div>
          )}

          {/* STATE 1: SUCCESS */}
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
                      Cập nhật mật khẩu thành công
                    </p>
                    <p className="text-emerald-800/90 dark:text-emerald-200/90 leading-relaxed">
                      Các phiên đăng nhập cũ trên các thiết bị khác đã hết hiệu
                      lực. Bạn có thể đăng nhập ngay bằng mật khẩu mới vừa tạo.
                    </p>
                  </div>
                </div>
              </div>

              <Link href={`/?${loginQuery}`} className="w-full block pt-1">
                <Button className="w-full h-11 rounded-xl font-semibold gap-2 shadow-md shadow-primary/20">
                  <LogIn className="w-4 h-4" />
                  Đăng nhập bằng mật khẩu mới
                </Button>
              </Link>
            </div>
          ) : checking ? (
            /* STATE 2: CHECKING LINK */
            <div className="py-8 flex flex-col items-center justify-center space-y-3 text-center">
              <Loader2 className="w-8 h-8 text-primary animate-spin" />
              <p
                role="status"
                className="text-sm text-muted-foreground font-medium"
              >
                Đang xác thực liên kết khôi phục...
              </p>
            </div>
          ) : valid ? (
            /* STATE 3: VALID FORM */
            <form
              onSubmit={(event) => void submit(event)}
              className="space-y-5"
            >
              <NewPasswordFields
                password={password}
                confirm={confirm}
                disabled={busy}
                onPassword={setPassword}
                onConfirm={setConfirm}
              />

              {error && (
                <div
                  role="alert"
                  className="rounded-xl border border-destructive/30 bg-destructive/10 p-3.5 text-destructive text-xs sm:text-sm flex items-start gap-2.5 animate-in fade-in slide-in-from-top-1 duration-200"
                >
                  <AlertCircle className="w-4 h-4 shrink-0 mt-0.5" />
                  <p className="leading-relaxed">{error}</p>
                </div>
              )}

              <Button
                type="submit"
                disabled={busy}
                className="w-full h-11 rounded-xl font-semibold shadow-md shadow-primary/20 transition-all hover:shadow-lg hover:shadow-primary/25 active:scale-[0.99] gap-2 cursor-pointer disabled:cursor-not-allowed mt-2"
              >
                {busy ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                    Đang đặt lại mật khẩu...
                  </>
                ) : (
                  <>
                    <KeyRound className="w-4 h-4 mr-2" />
                    Đặt lại mật khẩu
                  </>
                )}
              </Button>
            </form>
          ) : (
            /* STATE 4: INVALID LINK / ERROR */
            <div className="space-y-4">
              {error && (
                <div
                  role="alert"
                  className="rounded-xl border border-destructive/30 bg-destructive/10 p-4 text-destructive space-y-1 animate-in fade-in slide-in-from-top-1 duration-200"
                >
                  <div className="flex items-start gap-3">
                    <AlertCircle className="w-5 h-5 text-destructive shrink-0 mt-0.5" />
                    <div className="space-y-0.5 text-xs sm:text-sm">
                      <p className="font-semibold">Lưu ý liên kết</p>
                      <p className="leading-relaxed opacity-90">{error}</p>
                    </div>
                  </div>
                </div>
              )}

              <div className="rounded-xl border border-border/60 bg-muted/40 p-3.5 flex items-start gap-3 text-xs text-muted-foreground leading-relaxed">
                <ShieldCheck className="w-4 h-4 text-primary shrink-0 mt-0.5" />
                <div>
                  Vì lý do bảo mật, liên kết đặt lại mật khẩu chỉ sử dụng được{" "}
                  <strong>01 lần duy nhất</strong>. Nếu bạn đã từng bấm hoặc
                  liên kết đã quá hạn, vui lòng gửi yêu cầu mới.
                </div>
              </div>

              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
                {link && (
                  <Button
                    onClick={() =>
                      setValidationAttempt((current) => current + 1)
                    }
                    variant="outline"
                    className="w-full h-11 rounded-xl font-semibold gap-2"
                  >
                    <RotateCcw className="w-4 h-4" />
                    Thử kiểm tra lại
                  </Button>
                )}
                <Link href="/forgot-password" className="w-full block">
                  <Button className="w-full h-11 rounded-xl font-semibold gap-2 shadow-sm">
                    <KeyRound className="w-4 h-4" />
                    Yêu cầu liên kết mới
                  </Button>
                </Link>
              </div>
            </div>
          )}
        </CardContent>

        {/* Footer with navigation */}
        {!done && (
          <CardFooter className="p-6 sm:p-8 pt-4 pb-6 flex items-center justify-between border-t border-border/50 bg-muted/15 text-xs sm:text-sm">
            <Link
              href="/"
              className="inline-flex items-center gap-1.5 font-medium text-muted-foreground hover:text-foreground transition-colors group"
            >
              <ArrowLeft className="w-3.5 h-3.5 transition-transform group-hover:-translate-x-1" />
              Quay lại đăng nhập
            </Link>
            <Link
              href="/forgot-password"
              className="font-medium text-primary hover:text-primary/80 transition-colors"
            >
              Yêu cầu liên kết mới
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
