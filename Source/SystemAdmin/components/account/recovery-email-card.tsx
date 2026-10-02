"use client";

import { FormEvent, useEffect, useState } from "react";
import {
  Mail,
  ShieldCheck,
  CheckCircle2,
  Clock,
  AlertCircle,
  Eye,
  EyeOff,
  Loader2,
  Send,
  KeyRound,
  Lock,
} from "lucide-react";
import {
  recoveryEmailApi,
  type RecoveryEmailStatus,
} from "@/services/recovery-email-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { getApiErrorMessage } from "@/lib/api-error";
import { formatDateTime } from "@/lib/format-date-time";

export function RecoveryEmailCard({
  email,
  verifiedAt,
  saveRequired,
  mustChangePassword,
  onReload,
}: {
  email: string | null;
  verifiedAt: string | null;
  saveRequired: boolean;
  mustChangePassword: boolean;
  onReload: () => Promise<void>;
}) {
  const [status, setStatus] = useState<RecoveryEmailStatus | null>(null);
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [remaining, setRemaining] = useState(0);
  const [open, setOpen] = useState(false);

  useEffect(() => {
    if (mustChangePassword) return;
    let active = true;
    recoveryEmailApi
      .getStatus()
      .then((data) => {
        if (active) {
          setStatus(data);
          setRemaining(data.resendAfterSeconds);
        }
      })
      .catch((err) => {
        if (active)
          setError(
            getApiErrorMessage(err, "Không thể đọc trạng thái xác minh email."),
          );
      });
    return () => {
      active = false;
    };
  }, [email, verifiedAt, mustChangePassword]);

  useEffect(() => {
    if (remaining <= 0) return;
    const timer = window.setTimeout(
      () => setRemaining((value) => Math.max(0, value - 1)),
      1000,
    );
    return () => window.clearTimeout(timer);
  }, [remaining]);

  async function send(event: FormEvent) {
    event.preventDefault();
    if (
      busy ||
      remaining > 0 ||
      saveRequired ||
      mustChangePassword ||
      !status?.enabled ||
      !password ||
      !email
    )
      return;
    setBusy(true);
    setError("");
    setMessage("");
    try {
      const data = await recoveryEmailApi.requestVerification(password);
      setPassword("");
      setRemaining(data.resendAfterSeconds);
      setMessage(data.message);
      setStatus((current) =>
        current ? { ...current, pending: true } : current,
      );
      // Issuance touches account rowversion. Reload before the next profile edit.
      await onReload();
    } catch (err) {
      setError(getApiErrorMessage(err, "Không thể yêu cầu xác minh email."));
    } finally {
      setBusy(false);
    }
  }

  const isVerified = Boolean(status?.verifiedAt || verifiedAt);
  const isPending = Boolean(status?.pending && !isVerified);

  const handleOpenDialog = () => {
    setError("");
    setMessage("");
    setPassword("");
    setShowPassword(false);
    setOpen(true);
  };

  return (
    <>
      {/* Compact summary card */}
      <section
        className="rounded-xl border border-border/80 bg-card p-4 sm:p-5 shadow-xs transition-all"
        aria-label="Email khôi phục mật khẩu"
      >
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-start gap-3.5 min-w-0">
            <div
              className={`w-10 h-10 rounded-xl flex items-center justify-center shrink-0 mt-0.5 ${
                isVerified
                  ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 ring-1 ring-emerald-500/20"
                  : isPending
                    ? "bg-amber-500/10 text-amber-600 dark:text-amber-400 ring-1 ring-amber-500/20"
                    : "bg-primary/10 text-primary ring-1 ring-primary/20"
              }`}
            >
              {isVerified ? (
                <ShieldCheck className="w-5 h-5" />
              ) : isPending ? (
                <Clock className="w-5 h-5" />
              ) : (
                <Mail className="w-5 h-5" />
              )}
            </div>

            <div className="space-y-1 min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <h4 className="font-semibold text-sm sm:text-base text-foreground leading-tight">
                  Email khôi phục mật khẩu
                </h4>

                {/* Status Badge */}
                {isVerified ? (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-emerald-500/10 text-emerald-700 dark:text-emerald-400 border border-emerald-500/20">
                    <CheckCircle2 className="w-3 h-3 shrink-0" />
                    Đã xác minh
                  </span>
                ) : isPending ? (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-amber-500/10 text-amber-700 dark:text-amber-400 border border-amber-500/20">
                    <Clock className="w-3 h-3 shrink-0" />
                    Chờ xác minh
                  </span>
                ) : (
                  <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-xs font-medium bg-muted text-muted-foreground border border-border">
                    <AlertCircle className="w-3 h-3 shrink-0" />
                    Chưa xác minh
                  </span>
                )}
              </div>

              <div className="flex flex-wrap items-center gap-x-2 text-xs sm:text-sm text-muted-foreground">
                {email ? (
                  <span className="font-mono text-foreground/90 font-medium">
                    {email}
                  </span>
                ) : (
                  <span className="italic text-muted-foreground">
                    Chưa thiết lập email trong hồ sơ
                  </span>
                )}

                {isVerified && verifiedAt && (
                  <span className="text-xs text-muted-foreground">
                    • {formatDateTime(verifiedAt)}
                  </span>
                )}
              </div>

              <p className="text-xs text-muted-foreground leading-relaxed pt-0.5">
                {isVerified
                  ? "Email đã được xác thực an toàn. Dùng để nhận liên kết khôi phục khi quên mật khẩu."
                  : isPending
                    ? "Liên kết xác thực đã được gửi. Vui lòng mở liên kết trong hộp thư của bạn."
                    : "Xác minh email để có thể sử dụng chức năng tự đặt lại mật khẩu khi quên."}{" "}
                Thay đổi địa chỉ email ở hồ sơ sẽ tự động thu hồi xác minh cũ.
              </p>
            </div>
          </div>

          {/* Action Trigger Button */}
          <div className="shrink-0 flex items-center gap-2 self-start sm:self-center pl-13 sm:pl-0">
            <Button
              variant={isVerified ? "outline" : "default"}
              size="sm"
              onClick={handleOpenDialog}
              disabled={
                mustChangePassword || !email || saveRequired || !status?.enabled
              }
              className="rounded-lg h-9 px-3.5 font-medium shadow-xs gap-1.5 cursor-pointer disabled:cursor-not-allowed"
            >
              {isVerified ? (
                <>
                  <KeyRound className="w-3.5 h-3.5" />
                  Xác minh lại
                </>
              ) : isPending ? (
                <>
                  <Send className="w-3.5 h-3.5" />
                  {remaining > 0
                    ? `Gửi lại (${remaining}s)`
                    : "Gửi lại liên kết"}
                </>
              ) : (
                <>
                  <KeyRound className="w-3.5 h-3.5" />
                  Xác minh ngay
                </>
              )}
            </Button>
          </div>
        </div>

        {/* In-card contextual warnings */}
        {saveRequired && (
          <div className="mt-3 pt-3 border-t border-border/60 text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" />
            <span>
              Hãy lưu thay đổi hồ sơ trước khi gửi yêu cầu xác minh email.
            </span>
          </div>
        )}
        {!email && (
          <div className="mt-3 pt-3 border-t border-border/60 text-xs text-muted-foreground flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" />
            <span>
              Vui lòng nhập và lưu địa chỉ email trong form hồ sơ ở trên trước.
            </span>
          </div>
        )}
        {mustChangePassword && (
          <div className="mt-3 pt-3 border-t border-border/60 text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" />
            <span>
              Hãy hoàn tất đổi mật khẩu bắt buộc trước khi xác minh email khôi
              phục.
            </span>
          </div>
        )}
        {status && !status.enabled && (
          <div className="mt-3 pt-3 border-t border-border/60 text-xs text-muted-foreground flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5 shrink-0" />
            <span>
              Chức năng xác minh email khôi phục chưa được bật hoặc chưa sẵn
              sàng.
            </span>
          </div>
        )}
        {!open && error && (
          <p role="alert" className="mt-3 text-xs text-destructive">
            {error}
          </p>
        )}
      </section>

      {/* Modal Dialog for Verification Request */}
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader className="space-y-2">
            <div className="w-10 h-10 rounded-xl bg-primary/10 text-primary flex items-center justify-center mb-0.5">
              <ShieldCheck className="w-5 h-5 text-primary" />
            </div>
            <DialogTitle className="text-lg font-bold">
              Xác minh email khôi phục
            </DialogTitle>
            <DialogDescription className="text-xs sm:text-sm text-muted-foreground leading-relaxed">
              Hệ thống sẽ gửi liên kết xác thực đến hộp thư của bạn. Vui lòng
              nhập mật khẩu hiện tại để xác nhận quyền sở hữu tài khoản.
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={send} className="space-y-4 py-1">
            {/* Target email badge */}
            <div className="rounded-xl border border-border/70 bg-muted/30 p-3 flex items-center gap-3">
              <div className="w-8 h-8 rounded-lg bg-background flex items-center justify-center shrink-0 border border-border/60">
                <Mail className="w-4 h-4 text-muted-foreground" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-[11px] font-medium text-muted-foreground uppercase tracking-wider">
                  Địa chỉ email nhận liên kết
                </p>
                <p className="font-mono text-sm font-semibold text-foreground truncate">
                  {email}
                </p>
              </div>
            </div>

            {/* Password input field */}
            <div className="space-y-2">
              <Label
                htmlFor="recovery-current-password"
                className="text-xs sm:text-sm font-medium flex items-center gap-1.5"
              >
                <Lock className="w-3.5 h-3.5 text-muted-foreground" />
                Mật khẩu tài khoản hiện tại
              </Label>
              <div className="flex gap-2 items-center">
                <Input
                  id="recovery-current-password"
                  type={showPassword ? "text" : "password"}
                  autoComplete="current-password"
                  maxLength={100}
                  placeholder="Nhập mật khẩu hiện tại"
                  value={password}
                  onChange={(event) => {
                    setPassword(event.target.value);
                    if (error) setError("");
                  }}
                  disabled={busy || remaining > 0}
                  className="h-10 pr-10"
                />
                <Button
                  type="button"
                  variant="outline"
                  tabIndex={-1}
                  disabled={busy}
                  onClick={() => setShowPassword(!showPassword)}
                  aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  aria-pressed={showPassword}
                  aria-controls="recovery-current-password"
                >
                  {showPassword ? (
                    <EyeOff className="w-4 h-4" />
                  ) : (
                    <Eye className="w-4 h-4" />
                  )}
                </Button>
              </div>
              <p className="text-[11px] text-muted-foreground">
                Mật khẩu dùng để xác nhận thao tác và bảo vệ tài khoản khỏi truy
                cập trái phép.
              </p>
            </div>

            {/* Success message banner */}
            {message && (
              <div
                role="status"
                className="rounded-xl border border-emerald-500/30 bg-emerald-500/10 dark:bg-emerald-950/20 p-3.5 text-emerald-900 dark:text-emerald-200 space-y-1 animate-in fade-in slide-in-from-top-1 duration-200"
              >
                <div className="flex items-start gap-2.5">
                  <CheckCircle2 className="w-4 h-4 text-emerald-600 dark:text-emerald-400 shrink-0 mt-0.5" />
                  <div className="space-y-0.5 text-xs sm:text-sm">
                    <p className="font-semibold text-emerald-900 dark:text-emerald-100">
                      Gửi yêu cầu thành công
                    </p>
                    <p className="text-emerald-800/90 dark:text-emerald-200/90 leading-relaxed text-xs">
                      {message}
                    </p>
                  </div>
                </div>
                <p className="pl-6.5 text-[11px] text-emerald-700 dark:text-emerald-300/80">
                  Vui lòng kiểm tra cả hộp thư <strong>Spam/Thư rác</strong> nếu
                  chưa thấy email đến.
                </p>
              </div>
            )}

            {/* Error message banner */}
            {error && (
              <div
                role="alert"
                className="rounded-xl border border-destructive/30 bg-destructive/10 p-3 text-destructive space-y-1 animate-in fade-in slide-in-from-top-1 duration-200 text-xs sm:text-sm"
              >
                <div className="flex items-start gap-2.5">
                  <AlertCircle className="w-4 h-4 text-destructive shrink-0 mt-0.5" />
                  <div className="space-y-0.5">
                    <p className="font-semibold">Không thể gửi yêu cầu</p>
                    <p className="leading-relaxed opacity-90 text-xs">
                      {error}
                    </p>
                  </div>
                </div>
              </div>
            )}

            <DialogFooter className="pt-2 gap-2">
              <Button
                type="button"
                variant="outline"
                onClick={() => setOpen(false)}
                disabled={busy}
              >
                Đóng
              </Button>
              <Button
                type="submit"
                disabled={
                  busy ||
                  remaining > 0 ||
                  saveRequired ||
                  !email ||
                  !password.trim() ||
                  !status?.enabled
                }
                className="gap-1.5 cursor-pointer disabled:cursor-not-allowed"
              >
                {busy ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    Đang gửi yêu cầu...
                  </>
                ) : remaining > 0 ? (
                  <>
                    <Clock className="w-4 h-4" />
                    Gửi lại sau {remaining}s
                  </>
                ) : isPending ? (
                  <>
                    <Send className="w-4 h-4" />
                    Gửi lại liên kết
                  </>
                ) : (
                  <>
                    <Send className="w-4 h-4" />
                    Gửi email xác minh
                  </>
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
