"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { passwordRecoveryApi, recoveryRetrySeconds } from "@/services/password-recovery-api";
import { getRecoveryErrorMessage as getApiErrorMessage } from "@/services/password-recovery-api";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export default function ForgotPasswordPage({ tenant = false }: { tenant?: boolean }) {
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
    const timer = setTimeout(() => setSeconds(current => Math.max(0, current - 1)), 1000);
    return () => clearTimeout(timer);
  }, [seconds]);
  async function submit(event: React.FormEvent) {
    event.preventDefault();
    if (lock.current || seconds > 0) return;
    if (!name.trim() || (tenant && !/^[A-Za-z0-9-]{3,50}$/.test(code.trim()))) {
      setError(tenant ? "Hãy nhập tên tài khoản và mã tổ chức hợp lệ (3–50 ký tự: chữ, số hoặc dấu gạch ngang)." : "Hãy nhập tên tài khoản."); return;
    }
    lock.current = true; setBusy(true); setError("");
    try {
      const result = await passwordRecoveryApi.request(name.trim());
      setMessage(result.message); setSeconds(result.resendAfterSeconds ?? 60);
    } catch (err) {
      setSeconds(recoveryRetrySeconds(err));
      setError(getApiErrorMessage(err, "Không thể kết nối. Vui lòng thử lại."));
    } finally { lock.current = false; setBusy(false); }
  }
  return <main className="mx-auto mt-12 max-w-lg p-6 space-y-6">
    <h1 className="text-2xl font-semibold">Quên mật khẩu</h1>
    <p className="text-sm text-muted-foreground">Liên kết chỉ được gửi đến email khôi phục đã xác minh. Nếu chưa xác minh hoặc không còn truy cập email, hãy liên hệ {tenant ? "người quản lý tổ chức" : "người vận hành hệ thống"}.</p>
    <form onSubmit={event => void submit(event)} className="space-y-4">
      <fieldset disabled={busy || seconds > 0} className="space-y-4">
        {tenant && <div className="space-y-2"><Label htmlFor="tenant-code">Mã tổ chức</Label>
          <Input id="tenant-code" value={code} required maxLength={50} autoComplete="organization"
            onChange={event => setCode(event.target.value)} /></div>}
        <div className="space-y-2"><Label htmlFor="account-name">Tên tài khoản</Label>
          <Input id="account-name" value={name} required maxLength={tenant ? 50 : 100} autoComplete="username"
            onChange={event => setName(event.target.value)} /></div>
      </fieldset>
      <Button type="submit" disabled={busy || seconds > 0} className="w-full">
        {busy ? "Đang tiếp nhận..." : seconds > 0 ? `Gửi lại sau ${seconds} giây` : message ? "Gửi lại liên kết" : "Gửi liên kết đặt lại mật khẩu"}
      </Button>
    </form>
    {message && <p role="status" className="rounded-lg border p-4 text-sm">{message}</p>}
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <Link href="/" className="inline-block text-primary underline">Quay lại đăng nhập</Link>
  </main>;
}
