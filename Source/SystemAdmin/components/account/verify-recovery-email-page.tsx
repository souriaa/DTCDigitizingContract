"use client";

import { useEffect, useRef, useState } from "react";
import axios from "axios";
import { recoveryEmailApi } from "@/services/recovery-email-api";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import { getApiErrorMessage } from "@/lib/api-error";

export default function VerifyRecoveryEmailPage() {
  const [link, setLink] = useState<{ requestId: string; token: string; tenantCode: string | null } | null>(null);
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
      if (!requestId || !token) setError("Liên kết không hợp lệ. Nếu đã tải lại trang, hãy mở lại liên kết từ email.");
      else setLink({ requestId, token, tenantCode });
    });
  }, []);
  async function verify() {
    if (!link || busy) return;
    setBusy(true); setError("");
    try {
      await recoveryEmailApi.verify(link.requestId, link.token);
      setDone(true); setLink(null);
    } catch (err) {
      if (axios.isAxiosError(err) && err.response?.status === 401)
        setError("Hãy đăng nhập đúng tài khoản, sau đó mở lại liên kết xác minh từ email. Liên kết không được lưu trong trình duyệt.");
      else setError(getApiErrorMessage(err, "Không thể xác minh email. Hãy yêu cầu liên kết mới trong Hồ sơ."));
    } finally { setBusy(false); }
  }
  return <main className="mx-auto max-w-lg p-6 mt-12 space-y-5">
    <h1 className="text-2xl font-semibold">Xác minh email khôi phục</h1>
    <p>Đăng nhập đúng tài khoản đã yêu cầu xác minh{link?.tenantCode ? ` tại tổ chức ${link.tenantCode}` : ""}, sau đó bấm xác nhận. Nếu chưa đăng nhập, đăng nhập rồi mở lại email.</p>
    {done ? <p role="status">Email khôi phục đã được xác minh.</p>
      : <Button disabled={!link || busy} onClick={() => void verify()}>{busy ? "Đang xác minh..." : "Xác nhận email"}</Button>}
    {error && <p role="alert" className="text-destructive">{error}</p>}
    <div className="flex gap-4"><Link href="/">Đăng nhập</Link><Link href="/profile">Hồ sơ tài khoản</Link></div>
  </main>;
}
