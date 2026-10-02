import axios from "axios";

export interface RecoveryEmailStatus {
  enabled: boolean; verifiedAt: string | null; pending: boolean; resendAfterSeconds: number;
}
export interface RecoveryEmailResult { message: string; resendAfterSeconds: number }
// Authenticated verification only. Do not use for Phase 4 public password-reset endpoints.
// No automatic auth redirect: the email-link page must explain login + reopening the link.
const client = axios.create({ baseURL: process.env.NEXT_PUBLIC_API_URL, withCredentials: true });
const BASE_URL = "/api/system-auth/recovery-email";
export const recoveryEmailApi = {
  getStatus: async () => (await client.get<RecoveryEmailStatus>(`${BASE_URL}/status`)).data,
  requestVerification: async (currentPassword: string) =>
    (await client.post<RecoveryEmailResult>(`${BASE_URL}/request-verification`, { currentPassword })).data,
  verify: async (requestId: string, token: string) =>
    (await client.post<RecoveryEmailResult>(`${BASE_URL}/verify`, { requestId, token })).data,
};

