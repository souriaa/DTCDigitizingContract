"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { getAccountPasswordRules, isValidAccountPassword, PASSWORD_POLICY_MESSAGE } from "@/lib/password-policy";

export function NewPasswordFields({ password, confirm, disabled, onPassword, onConfirm }: {
  password: string; confirm: string; disabled?: boolean;
  onPassword: (value: string) => void; onConfirm: (value: string) => void;
}) {
  const validation = getAccountPasswordRules(password);
  const rules = [
    { label: "Từ 12 đến 100 ký tự", pass: validation.hasLength },
    { label: "Có ít nhất một chữ số (0–9)", pass: validation.hasNumber },
    { label: "Bao gồm chữ hoa (A–Z) và chữ thường (a–z)", pass: validation.hasUpperAndLower },
  ];
  return <fieldset disabled={disabled} className="space-y-4">
    <div className="space-y-2"><Label htmlFor="new-password">Mật khẩu mới</Label>
      <Input id="new-password" type="password" autoComplete="new-password" required minLength={12} maxLength={100}
        value={password} onChange={e => onPassword(e.target.value)} aria-describedby="password-rules" />
    </div>
    <div id="password-rules" className="text-sm space-y-1">
      <p>{PASSWORD_POLICY_MESSAGE}</p>
      {rules.map(rule => <p key={rule.label} className={rule.pass ? "text-emerald-700 dark:text-emerald-400" : "text-muted-foreground"}>
        {rule.pass ? "✓" : "○"} {rule.label}
      </p>)}
      <p>Khác với mật khẩu hiện tại — hệ thống kiểm tra khi lưu.</p>
      <p aria-live="polite">{password && isValidAccountPassword(password) ? "Mật khẩu đáp ứng yêu cầu về độ dài và ký tự." : ""}</p>
    </div>
    <div className="space-y-2"><Label htmlFor="confirm-password">Nhập lại mật khẩu mới</Label>
      <Input id="confirm-password" type="password" autoComplete="new-password" required maxLength={100}
        value={confirm} onChange={e => onConfirm(e.target.value)} />
    </div>
  </fieldset>;
}
