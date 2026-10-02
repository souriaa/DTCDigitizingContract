export const PASSWORD_POLICY_MESSAGE = "Mật khẩu phải có từ 12 đến 100 ký tự, ít nhất một chữ số (0–9), chữ hoa (A–Z) và chữ thường (a–z).";
export function getAccountPasswordRules(value: string) {
  return {
    hasLength: value.length >= 12 && value.length <= 100,
    hasNumber: /[0-9]/.test(value),
    hasUpperAndLower: /[A-Z]/.test(value) && /[a-z]/.test(value),
  };
}
export function isValidAccountPassword(value: string, currentPassword?: string) {
  const rules = getAccountPasswordRules(value);
  return rules.hasLength && rules.hasNumber && rules.hasUpperAndLower
    && (currentPassword === undefined || value !== currentPassword);
}
