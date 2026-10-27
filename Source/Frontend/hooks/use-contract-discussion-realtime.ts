"use client";

import { useEffect, useRef } from "react";
import { HubConnectionState } from "@microsoft/signalr";
import { createRealtimeConnection } from "@/lib/realtime-connection";

/** One connection per contract page, not one socket per term or thread. */
export function useContractDiscussionRealtime(
  path: string | null,
  onRefresh: (signal: AbortSignal) => Promise<void>,
) {
  const refreshRef = useRef(onRefresh);
  useEffect(() => { refreshRef.current = onRefresh; }, [onRefresh]);

  useEffect(() => {
    if (!path) return;
    const controller = new AbortController();
    let disposed = false;
    let dirty = false;
    let refreshing = false;
    let refreshTimer: ReturnType<typeof setTimeout> | null = null;
    let retryTimer: ReturnType<typeof setTimeout> | null = null;
    let pendingStart: Promise<void> | null = null;
    let retryDelay = 2000;
    const connection = createRealtimeConnection(path);

    const refresh = async () => {
      if (refreshing || disposed) return;
      refreshing = true;
      try {
        // Keep another invalidation received during the REST request; never drop it.
        while (dirty && !disposed) {
          dirty = false;
          try { await refreshRef.current(controller.signal); }
          catch { /* REST handles authorization; reconnect/focus resynchronizes transient failures. */ }
        }
      } finally { refreshing = false; }
    };
    const scheduleRefresh = () => {
      if (disposed) return;
      dirty = true;
      if (refreshTimer) return;
      refreshTimer = setTimeout(() => { refreshTimer = null; void refresh(); }, 150);
    };
    const start = () => {
      if (disposed || pendingStart || connection.state !== HubConnectionState.Disconnected) return;
      const attempt = connection.start();
      pendingStart = attempt;
      void attempt.then(() => {
        if (!disposed) { retryDelay = 2000; scheduleRefresh(); }
      }).catch(() => {
        if (!disposed) {
          // REST rechecks expired/revoked access even if a hub handshake fails.
          scheduleRefresh();
          retryTimer = setTimeout(start, retryDelay);
          retryDelay = Math.min(retryDelay * 2, 60000);
        }
      }).finally(() => {
        if (pendingStart === attempt) pendingStart = null;
        if (disposed) void connection.stop();
      });
    };
    const onVisibility = () => { if (document.visibilityState === "visible") scheduleRefresh(); };
    connection.on("DiscussionChanged", scheduleRefresh);
    connection.onreconnected(scheduleRefresh);
    connection.onclose(() => {
      if (!disposed) { scheduleRefresh(); retryTimer = setTimeout(start, retryDelay); }
    });
    window.addEventListener("focus", scheduleRefresh);
    document.addEventListener("visibilitychange", onVisibility);
    start();
    return () => {
      disposed = true;
      controller.abort();
      if (refreshTimer) clearTimeout(refreshTimer);
      if (retryTimer) clearTimeout(retryTimer);
      window.removeEventListener("focus", scheduleRefresh);
      document.removeEventListener("visibilitychange", onVisibility);
      // Do not stop an in-flight negotiation (React StrictMode/unmount).
      if (!pendingStart) void connection.stop();
    };
  }, [path]);
}
