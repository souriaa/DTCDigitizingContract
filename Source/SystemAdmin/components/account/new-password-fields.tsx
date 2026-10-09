"use client";

import { useState } from "react";
import { Eye, EyeOff } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  getAccountPasswordRules,
  isValidAccountPassword,
  PASSWORD_POLICY_MESSAGE,
} from "@/lib/password-policy";

export function NewPasswordFields({
  password,
  confirm,
  disabled,
  onPassword,
  onConfirm,
}: {
  password: string;
  confirm: string;
  disabled?: boolean;
  onPassword: (value: string) => void;
  onConfirm: (value: string) => void;
}) {
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const validation = getAccountPasswordRules(password);
  const rules = [
    { label: "Từ 12 đến 100 ký tự", pass: validation.hasLength },
    { label: "Có ít nhất một chữ số (0–9)", pass: validation.hasNumber },
    {
      label: "Bao gồm chữ hoa (A–Z) và chữ thường (a–z)",
      pass: validation.hasUpperAndLower,
    },
  ];
  return (
    <fieldset disabled={disabled} className="space-y-4">
      <div className="space-y-2">
        <Label htmlFor="new-password">Mật khẩu mới</Label>
        <div className="relative">
          <Input
            id="new-password"
            type={showPassword ? "text" : "password"}
            autoComplete="new-password"
            required
            minLength={12}
            maxLength={100}
            value={password}
            onChange={(e) => onPassword(e.target.value)}
            aria-describedby="password-rules"
            className="h-11 rounded-xl transition-all focus-visible:ring-2 focus-visible:ring-primary/20 font-medium"
            style={{ paddingRight: "7rem" }}
          />
          <button
            type="button"
            disabled={disabled}
            onClick={() => setShowPassword((current) => !current)}
            className="absolute right-20 top-1/2 -translate-y-1/2 rounded p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
            aria-label={showPassword ? "Ẩn mật khẩu mới" : "Hiện mật khẩu mới"}
            aria-pressed={showPassword}
            aria-controls="new-password"
            title={showPassword ? "Ẩn mật khẩu mới" : "Hiện mật khẩu mới"}
          >
            {showPassword ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>
      </div>
      <div id="password-rules" className="text-sm space-y-1">
        <p>{PASSWORD_POLICY_MESSAGE}</p>
        {rules.map((rule) => (
          <p
            key={rule.label}
            className={
              rule.pass
                ? "text-emerald-700 dark:text-emerald-400"
                : "text-muted-foreground"
            }
          >
            {rule.pass ? "✓" : "○"} {rule.label}
          </p>
        ))}
        <p>Khác với mật khẩu hiện tại — hệ thống kiểm tra khi lưu.</p>
        <p aria-live="polite">
          {password && isValidAccountPassword(password)
            ? "Mật khẩu đáp ứng yêu cầu về độ dài và ký tự."
            : ""}
        </p>
      </div>
      <div className="space-y-2">
        <Label htmlFor="confirm-password">Nhập lại mật khẩu mới</Label>
        <div className="relative">
          <Input
            id="confirm-password"
            type={showConfirm ? "text" : "password"}
            autoComplete="new-password"
            required
            maxLength={100}
            value={confirm}
            onChange={(e) => onConfirm(e.target.value)}
            className="h-11 rounded-xl transition-all focus-visible:ring-2 focus-visible:ring-primary/20 font-medium"
            style={{ paddingRight: "7rem" }}
          />
          <button
            type="button"
            disabled={disabled}
            onClick={() => setShowConfirm((current) => !current)}
            className="absolute right-20 top-1/2 -translate-y-1/2 rounded p-1 text-muted-foreground transition-colors hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring disabled:opacity-50"
            aria-label={showConfirm ? "Ẩn mật khẩu xác nhận" : "Hiện mật khẩu xác nhận"}
            aria-pressed={showConfirm}
            aria-controls="confirm-password"
            title={showConfirm ? "Ẩn mật khẩu xác nhận" : "Hiện mật khẩu xác nhận"}
          >
            {showConfirm ? <EyeOff className="size-4" /> : <Eye className="size-4" />}
          </button>
        </div>
      </div>
    </fieldset>
  );
}
