"use client";

import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from "react";
import { HubConnectionState, LogLevel } from "@microsoft/signalr";
import { createRealtimeConnection } from "@/lib/realtime-connection";
import { notificationApi, type NotificationItem } from "@/services/notification-api";

interface NotificationContextValue {
  items: NotificationItem[];
  unreadCount: number;
  loading: boolean;
  loadingMore: boolean;
  error: string | null;
  nextCursor: string | null;
  revision: number;
  refresh: () => Promise<void>;
  loadMore: () => Promise<void>;
  markRead: (id: number) => Promise<boolean>;
  markAllRead: () => Promise<void>;
}

const NotificationContext = createContext<NotificationContextValue | null>(null);
const channelName = "contract-management-notifications";

function broadcastChange(tenantId: number, employeeId: number) {
  if (typeof BroadcastChannel === "undefined") return;
  const channel = new BroadcastChannel(channelName);
  channel.postMessage({ tenantId, employeeId });
  channel.close();
}

export function useNotifications(): NotificationContextValue {
  const value = useContext(NotificationContext);
  if (!value) throw new Error("NotificationProvider is missing.");
  return value;
}

export function NotificationProvider({
  employeeId, tenantId, children,
}: {
  employeeId: number;
  tenantId: number;
  children: React.ReactNode;
}) {
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [unreadCount, setUnreadCount] = useState(0);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [revision, setRevision] = useState(0);
  const active = useRef(true);
  const generation = useRef(0);
  const refreshing = useRef<Promise<void> | null>(null);
  const refreshToken = useRef<object | null>(null);
  const pendingRefresh = useRef(false);
  const loadingMoreRef = useRef(false);

  const refresh = useCallback(async () => {
    if (refreshing.current) {
      pendingRefresh.current = true;
      return refreshing.current;
    }
    const token = {};
    refreshToken.current = token;
    const task = (async () => {
      try {
        do {
          pendingRefresh.current = false;
          const current = ++generation.current;
          try {
            const [page, count] = await Promise.all([
              notificationApi.list(null, 20), notificationApi.unreadCount(),
            ]);
            if (!active.current || current !== generation.current) return;
            setItems(page.items);
            setNextCursor(page.nextCursor);
            setUnreadCount(count.count);
            setRevision((value) => value + 1);
            setError(null);
          } catch {
            if (active.current && current === generation.current)
              setError("Không thể tải thông báo. Vui lòng thử lại.");
          } finally {
            if (active.current) setLoading(false);
          }
        } while (active.current && pendingRefresh.current);
      } finally {
        if (refreshToken.current === token) {
          refreshing.current = null;
          refreshToken.current = null;
        }
      }
    })();
    refreshing.current = task;
    await task;
  }, []);

  const loadMore = useCallback(async () => {
    if (!nextCursor || loadingMoreRef.current) return;
    loadingMoreRef.current = true;
    setLoadingMore(true);
    const current = generation.current;
    try {
      const page = await notificationApi.list(nextCursor, 20);
      if (!active.current || current !== generation.current) return;
      setItems((previous) => {
        const known = new Set(previous.map((item) => item.notificationRecipientId));
        return [...previous, ...page.items.filter((item) => !known.has(item.notificationRecipientId))];
      });
      setNextCursor(page.nextCursor);
      setError(null);
    } catch {
      if (active.current) setError("Không thể tải thêm thông báo.");
    } finally {
      loadingMoreRef.current = false;
      if (active.current) setLoadingMore(false);
    }
  }, [nextCursor]);

  const markRead = useCallback(async (id: number) => {
    const target = items.find((item) => item.notificationRecipientId === id);
    const now = new Date().toISOString();
    if (target && !target.readAtUtc) {
      setItems((previous) => previous.map((item) => item.notificationRecipientId === id
        ? { ...item, readAtUtc: now } : item));
      setUnreadCount((count) => Math.max(0, count - 1));
    }
    try {
      await notificationApi.markRead(id);
      void refresh();
      broadcastChange(tenantId, employeeId);
      return true;
    } catch {
      await refresh();
      return false;
    }
  }, [items, refresh, tenantId, employeeId]);

  const markAllRead = useCallback(async () => {
    const now = new Date().toISOString();
    setItems((previous) => previous.map((item) => ({ ...item, readAtUtc: item.readAtUtc ?? now })));
    setUnreadCount(0);
    try {
      await notificationApi.markAllRead();
      broadcastChange(tenantId, employeeId);
      await refresh();
    }
    catch { await refresh(); }
  }, [refresh, tenantId, employeeId]);

  useEffect(() => {
    active.current = true;
    const generationRef = generation;
    const refreshingRef = refreshing;
    const refreshTokenRef = refreshToken;
    const pendingRef = pendingRefresh;
    void refresh();
    const channel = typeof BroadcastChannel !== "undefined"
      ? new BroadcastChannel(channelName) : null;
    let timer: ReturnType<typeof setTimeout> | null = null;
    const scheduleRefresh = () => {
      if (timer) clearTimeout(timer);
      timer = setTimeout(() => { void refresh(); }, 150);
    };
    if (channel) channel.onmessage = (event) => {
      if (event.data?.tenantId === tenantId && event.data?.employeeId === employeeId)
        scheduleRefresh();
    };
    const onFocus = () => scheduleRefresh();
    const onVisibility = () => { if (document.visibilityState === "visible") scheduleRefresh(); };
    window.addEventListener("focus", onFocus);
    document.addEventListener("visibilitychange", onVisibility);

    const connection = createRealtimeConnection("/api/hubs/notifications", LogLevel.Warning);
    let disposed = false;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    let retryDelay = 2000;
    let pendingStart: Promise<void> | null = null;
    const start = () => {
      if (disposed || pendingStart || connection.state !== HubConnectionState.Disconnected) return;
      const attempt = connection.start();
      pendingStart = attempt;
      void attempt.then(() => {
        if (!disposed) {
          retryDelay = 2000;
          scheduleRefresh();
        }
      }).catch(() => {
        if (!disposed) {
          retryTimer = setTimeout(start, retryDelay);
          retryDelay = Math.min(retryDelay * 2, 60000);
        }
      }).finally(() => {
        if (pendingStart === attempt) pendingStart = null;
        if (disposed) void connection.stop();
      });
    };
    connection.on("NotificationsChanged", () => {
      scheduleRefresh();
      channel?.postMessage({ tenantId, employeeId });
    });
    connection.onreconnected(scheduleRefresh);
    connection.onclose(() => {
      if (!disposed) retryTimer = setTimeout(start, retryDelay);
    });
    start();

    return () => {
      disposed = true;
      active.current = false;
      generationRef.current++;
      refreshingRef.current = null;
      refreshTokenRef.current = null;
      pendingRef.current = false;
      if (timer) clearTimeout(timer);
      if (retryTimer) clearTimeout(retryTimer);
      window.removeEventListener("focus", onFocus);
      document.removeEventListener("visibilitychange", onVisibility);
      channel?.close();
      // Stopping during negotiation makes SignalR report a spurious connection error.
      // The in-flight start will stop itself in its finally handler.
      if (!pendingStart) void connection.stop();
    };
  }, [employeeId, tenantId, refresh]);

  const value = useMemo(() => ({ items, unreadCount, loading, loadingMore, error,
    revision,
    nextCursor, refresh, loadMore, markRead, markAllRead }),
    [items, unreadCount, loading, loadingMore, error, revision, nextCursor, refresh, loadMore, markRead, markAllRead]);
  return <NotificationContext.Provider value={value}>{children}</NotificationContext.Provider>;
}
