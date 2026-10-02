"use client";

import { useEffect, useRef, useState } from "react";
import axios from "axios";
import Link from "next/link";
import { passwordRecoveryApi, RecoveryLink } from "@/services/password-recovery-api";
import { getRecoveryErrorMessage as getApiErrorMessage } from "@/services/password-recovery-api";
import { isValidAccountPassword, PASSWORD_POLICY_MESSAGE } from "@/lib/password-policy";
import { NewPasswordFields } from "@/components/account/new-password-fields";
import { Button } from "@/components/ui/button";

export default function ResetPasswordPage({ tenant = false }: { tenant?: boolean }) {
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
      if (!requestId || !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(requestId) || !token || !/^[A-Za-z0-9_-]{43}$/.test(token)
        || (tenant && !/^[A-Za-z0-9-]{3,50}$/.test(tenantCode))) {
        setChecking(false); setError("Liên kết không hợp lệ. Nếu đã tải lại trang, hãy mở lại liên kết từ email.");
      } else setLink({ requestId, token, ...(tenant ? { tenantCode } : {}) });
    });
  }, [tenant]);
  useEffect(() => {
    if (!link) return;
    const controller = new AbortController();
    queueMicrotask(() => { if (!controller.signal.aborted) { setChecking(true); setError(""); } });
    void passwordRecoveryApi.validate(link, controller.signal).then(() => {
      if (!controller.signal.aborted) { setValid(true); setChecking(false); }
    }).catch(err => {
      if (controller.signal.aborted || axios.isCancel(err)) return;
      setChecking(false); setValid(false);
      setError(getApiErrorMessage(err, "Không thể kiểm tra liên kết. Hãy thử lại."));
      if (axios.isAxiosError(err) && err.response?.data?.code === "PasswordRecoveryLinkInvalid") setLink(null);
    });
    return () => controller.abort();
  }, [link, validationAttempt]);
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (!link || !valid || lock.current) return;
    if (!isValidAccountPassword(password)) { setError(PASSWORD_POLICY_MESSAGE); return; }
    if (password !== confirm) { setError("Mật khẩu nhập lại chưa khớp."); return; }
    lock.current = true; setBusy(true); setError("");
    try {
      await passwordRecoveryApi.reset(link, password, confirm);
      setDone(true); setLink(null); setValid(false); setPassword(""); setConfirm("");
      // Do not logout a different tenant/sysadmin session sharing the same browser.
    } catch (err) {
      setError(getApiErrorMessage(err, "Không thể đặt lại mật khẩu. Vui lòng thử lại."));
      if (axios.isAxiosError(err) && err.response?.data?.code === "PasswordRecoveryLinkInvalid") {
        setLink(null); setValid(false); setPassword(""); setConfirm("");
      }
    } finally { lock.current = false; setBusy(false); }
  }
  const loginQuery = new URLSearchParams({ password_reset: "1", ...(tenant && code ? { tenantCode: code } : {}) });
  return <main className="mx-auto mt-12 max-w-lg p-6 space-y-6">
    <h1 className="text-2xl font-semibold">Đặt lại mật khẩu</h1>
    {done ? <><p role="status">Đặt lại mật khẩu thành công. Các phiên cũ sẽ hết hiệu lực. Vui lòng đăng nhập bằng mật khẩu mới.</p>
      <Link href={`/?${loginQuery}`} className="text-primary underline">Đăng nhập bằng mật khẩu mới</Link></>
      : checking ? <p role="status">Đang kiểm tra liên kết...</p>
      : valid ? <form onSubmit={event => void submit(event)} className="space-y-5">
          <NewPasswordFields password={password} confirm={confirm} disabled={busy} onPassword={setPassword} onConfirm={setConfirm} />
          <Button type="submit" disabled={busy} className="w-full">{busy ? "Đang đặt lại..." : "Đặt lại mật khẩu"}</Button>
        </form>
      : link && <Button onClick={() => setValidationAttempt(current => current + 1)}>Kiểm tra lại liên kết</Button>}
    {error && <p role="alert" className="text-destructive">{error}</p>}
    {!done && <nav className="flex flex-wrap gap-4"><Link href="/forgot-password" className="text-primary underline">Yêu cầu liên kết mới</Link>
      <Link href="/" className="text-primary underline">Quay lại đăng nhập</Link></nav>}
  </main>;
}
