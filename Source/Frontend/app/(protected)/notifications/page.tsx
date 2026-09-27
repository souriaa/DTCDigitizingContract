"use client";

import { useEffect, useRef, useState } from "react";
import { Header } from "@/components/ui/custom/header";
import { Button } from "@/components/ui/button";
import { NotificationEntry } from "@/components/notifications/notification-bell";
import { useNotifications } from "@/components/notifications/notification-provider";
import { notificationApi, type NotificationItem } from "@/services/notification-api";

type Filter = "all" | "unread" | "read";

export default function NotificationsPage() {
  const [filter, setFilter] = useState<Filter>("all");
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);
  const requestId = useRef(0);
  const { unreadCount, revision, markAllRead } = useNotifications();

  useEffect(() => {
    const current = ++requestId.current;
    let active = true;
    notificationApi.list(null, 20, filter)
      .then((page) => {
        if (!active || current !== requestId.current) return;
        setItems(page.items);
        setCursor(page.nextCursor);
        setError(null);
      })
      .catch(() => {
        if (active && current === requestId.current)
          setError("Không thể tải thông báo. Vui lòng thử lại.");
      })
      .finally(() => { if (active && current === requestId.current) setLoading(false); });
    return () => { active = false; };
  }, [filter, revision, retryKey]);

  const loadMore = async () => {
    if (!cursor || loadingMore) return;
    setLoadingMore(true);
    const current = requestId.current;
    try {
      const page = await notificationApi.list(cursor, 20, filter);
      if (current !== requestId.current) return;
      setItems((previous) => {
        const known = new Set(previous.map((item) => item.notificationRecipientId));
        return [...previous, ...page.items.filter((item) => !known.has(item.notificationRecipientId))];
      });
      setCursor(page.nextCursor);
      setError(null);
    } catch {
      if (current === requestId.current) setError("Không thể tải thêm thông báo.");
    } finally { setLoadingMore(false); }
  };

  return (
    <div className="flex min-h-0 flex-1 flex-col">
      <Header title="Thông báo" />
      <div className="mx-auto w-full max-w-3xl overflow-y-auto p-4 sm:p-6">
        <div className="mb-4 flex flex-wrap items-center gap-2">
          {(["all", "unread", "read"] as const).map((value) =>
            <Button key={value} type="button" size="sm" variant={filter === value ? "default" : "outline"}
              onClick={() => { if (value !== filter) { setLoading(true); setFilter(value); } }}>
              {value === "all" ? "Tất cả" : value === "unread" ? `Chưa đọc (${unreadCount})` : "Đã đọc"}
            </Button>)}
          <Button type="button" size="sm" variant="ghost" className="ml-auto" disabled={unreadCount === 0}
            onClick={() => { void markAllRead(); }}>Đánh dấu tất cả đã đọc</Button>
        </div>
        <div className="overflow-hidden rounded-lg border bg-card">
          {loading && <p className="p-5 text-sm text-muted-foreground">Đang tải thông báo...</p>}
          {!loading && error && <div className="p-5 text-sm text-destructive">{error} <button type="button" className="underline" onClick={() => { setLoading(true); setRetryKey((key) => key + 1); }}>Thử lại</button></div>}
          {!loading && !error && items.length === 0 && <p className="p-5 text-sm text-muted-foreground">
            {filter === "all" ? "Chưa có thông báo." : "Không có thông báo phù hợp."}
          </p>}
          {!loading && items.map((item) => <NotificationEntry key={item.notificationRecipientId} item={item} />)}
        </div>
        {cursor && <Button type="button" variant="outline" className="mt-4 w-full" disabled={loadingMore}
          onClick={() => { void loadMore(); }}>
          {loadingMore ? "Đang tải..." : "Tải thêm"}
        </Button>}
      </div>
    </div>
  );
}
