import axiosClient from "@/lib/axios-interceptor";

export interface NotificationItem {
  notificationRecipientId: number;
  notificationId: number;
  title: string;
  body: string | null;
  subjectType: string;
  subjectId: number;
  actionKey: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationPage {
  items: NotificationItem[];
  nextCursor: string | null;
}

export const notificationApi = {
  list: (
    cursor?: string | null,
    limit = 20,
    status: "all" | "read" | "unread" = "all",
  ) =>
    axiosClient.get<unknown, NotificationPage>("/api/notifications", {
      params: { ...(cursor ? { cursor } : {}), limit, status },
    }),
  unreadCount: () =>
    axiosClient.get<unknown, { count: number }>(
      "/api/notifications/unread-count",
    ),
  markRead: (id: number) =>
    axiosClient.patch<unknown, void>(`/api/notifications/${id}/read`, {}),
  markAllRead: () =>
    axiosClient.post<unknown, { changed: number }>(
      "/api/notifications/mark-all-read",
      {},
    ),
};

const contractTabs = new Set([
  "overview",
  "negotiation",
  "terms",
  "customer-access",
  "approval",
  "signature",
  "closing",
  "handover",
  "activity",
]);

export function notificationHref(item: NotificationItem): string | null {
  if (
    item.subjectType !== "Contract" ||
    !Number.isSafeInteger(item.subjectId) ||
    item.subjectId <= 0
  ) {
    return null;
  }
  const tab = item.actionKey ?? "overview";
  if (tab === "handover") return `/contracts/${item.subjectId}?tab=closing&section=handover`;
  if (!contractTabs.has(tab)) return null;
  return `/contracts/${item.subjectId}?tab=${encodeURIComponent(tab)}`;
}
