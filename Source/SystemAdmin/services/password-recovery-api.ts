import axios from "axios";

export function getRecoveryErrorMessage(error: unknown, message: string) {
  const body = axios.isAxiosError(error) ? error.response?.data : null;
  // Only show the curated public message. Do not expose transport errors or framework JSON/DTO details.
  return typeof body?.message === "string" && body.message.trim() ? body.message : message;
}

export interface RecoveryLink { requestId: string; token: string; tenantCode?: string }
export interface RecoveryResult { message: string; resendAfterSeconds?: number }
const client = axios.create({
  baseURL: process.env.NEXT_PUBLIC_API_URL, adapter: "fetch", withCredentials: false,
  withXSRFToken: false, timeout: 20_000,
});
// Axios fetch adapter maps false to credentials: "omit", including SAME-origin deployments.
// XHR withCredentials=false alone would still send same-origin cookies.
// No session redirect, tenant header, cookie, logging or persistent token storage.
const BASE = "/api/system-auth/password-recovery";
export const passwordRecoveryApi = {
  request: async (name: string) =>
    (await client.post<RecoveryResult>(BASE + "/request", { username: name })).data,
  validate: async (link: RecoveryLink, signal?: AbortSignal) =>
    (await client.post<RecoveryResult>(BASE + "/validate", link, { signal })).data,
  reset: async (link: RecoveryLink, newPassword: string, confirmPassword: string) =>
    (await client.post<RecoveryResult>(BASE + "/reset", { ...link, newPassword, confirmPassword })).data,
};
export function recoveryRetrySeconds(error: unknown) {
  if (!axios.isAxiosError(error) || error.response?.status !== 429) return 0;
  return Math.max(1, Number(error.response.data?.resendAfterSeconds || error.response.headers["retry-after"]) || 60);
}
