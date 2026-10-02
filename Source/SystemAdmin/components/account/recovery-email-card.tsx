"use client";

import { FormEvent, useEffect, useState } from "react";
import { recoveryEmailApi, type RecoveryEmailStatus } from "@/services/recovery-email-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getApiErrorMessage } from "@/lib/api-error";

export function RecoveryEmailCard({ email, verifiedAt, saveRequired, mustChangePassword, onReload }: {
  email: string | null; verifiedAt: string | null; saveRequired: boolean;
  mustChangePassword: boolean; onReload: () => Promise<void>;
}) {
  const [status, setStatus] = useState<RecoveryEmailStatus | null>(null);
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const [remaining, setRemaining] = useState(0);
  useEffect(() => {
    if (mustChangePassword) return;
    let active = true;
    recoveryEmailApi.getStatus()
      .then((data) => { if (active) { setStatus(data); setRemaining(data.resendAfterSeconds); } })
      .catch((err) => { if (active) setError(getApiErrorMessage(err, "Không thể đọc trạng thái xác minh email.")); });
    return () => { active = false; };
  }, [email, verifiedAt, mustChangePassword]);
  useEffect(() => {
    if (remaining <= 0) return;
    const timer = window.setTimeout(() => setRemaining((value) => Math.max(0, value - 1)), 1000);
    return () => window.clearTimeout(timer);
  }, [remaining]);
  async function send(event: FormEvent) {
    event.preventDefault();
    if (busy || remaining > 0 || saveRequired || !status?.enabled || !password) return;
    setBusy(true); setError(""); setMessage("");
    try {
      const data = await recoveryEmailApi.requestVerification(password);
      setPassword(""); setRemaining(data.resendAfterSeconds); setMessage(data.message);
      setStatus((current) => current ? { ...current, pending: true } : current);
      // Issuance touches account rowversion. Reload before the next profile edit.
      await onReload();
    } catch (err) { setError(getApiErrorMessage(err, "Không thể yêu cầu xác minh email.")); }
    finally { setBusy(false); }
  }
  return <section className="rounded-lg border p-4 space-y-3" aria-label="Email khôi phục">
    <h3 className="font-semibold">Email khôi phục mật khẩu</h3>
    <p className="text-sm text-muted-foreground">
      {(status?.verifiedAt || verifiedAt) ? "Đã xác minh email hiện tại." : status?.pending ? "Đang chờ xác minh. Hãy mở liên kết trong email." : "Email chưa được xác minh."}
      {" "}Hãy xác minh trước khi quên mật khẩu. Thay đổi email sẽ thu hồi xác minh và liên kết cũ.
    </p>
    {mustChangePassword ? <p className="text-sm">Hãy hoàn tất đổi mật khẩu bắt buộc trước khi xác minh email.</p>
      : !status?.enabled ? <p className="text-sm">Chức năng xác minh chưa được bật hoặc chưa sẵn sàng.</p>
      : !(status.verifiedAt || verifiedAt) && <form onSubmit={send} className="space-y-3">
        {saveRequired && <p className="text-sm text-amber-600">Hãy lưu thay đổi hồ sơ trước khi gửi xác minh.</p>}
        {!email && <p className="text-sm">Hãy nhập và lưu email ở hồ sơ trước.</p>}
        <Label htmlFor="recovery-current-password">Mật khẩu hiện tại</Label>
        <Input id="recovery-current-password" type="password" autoComplete="current-password" maxLength={1000}
          value={password} onChange={(event) => setPassword(event.target.value)}
          disabled={busy || saveRequired || !email} />
        <Button type="submit" disabled={busy || remaining > 0 || saveRequired || !email || !password}>
          {busy ? "Đang yêu cầu..." : remaining > 0 ? `Gửi lại sau ${remaining} giây` : status.pending ? "Gửi lại liên kết xác minh" : "Xác minh email khôi phục"}
        </Button>
      </form>}
    {message && <p role="status" className="text-sm text-emerald-600">{message}</p>}
    {error && <p role="alert" className="text-sm text-destructive">{error}</p>}
  </section>;
}
