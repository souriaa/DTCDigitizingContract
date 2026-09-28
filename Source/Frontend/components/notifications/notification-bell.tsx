"use client";

import { Bell, CheckCheck } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import { useNotifications } from "@/components/notifications/notification-provider";
import {
  notificationHref,
  type NotificationItem,
} from "@/services/notification-api";
import { formatDateTime } from "@/lib/format-date-time";

export function NotificationEntry({
  item,
  onOpen,
}: {
  item: NotificationItem;
  onOpen?: () => void;
}) {
  const router = useRouter();
  const { markRead } = useNotifications();
  const href = notificationHref(item);
  const open = async () => {
    if (href) {
      onOpen?.();
      router.push(href);
    }
    await markRead(item.notificationRecipientId);
  };
  return (
    <button
      type="button"
      onClick={() => {
        void open();
      }}
      className="flex w-full gap-3 border-b px-4 py-3 text-left transition-colors hover:bg-accent focus-visible:outline-2 focus-visible:outline-primary last:border-b-0"
      aria-label={`${item.readAtUtc ? "Đã đọc" : "Chưa đọc"}: ${item.title}`}
    >
      <span
        aria-hidden="true"
        className={`mt-2 size-2 shrink-0 rounded-full ${item.readAtUtc ? "bg-transparent" : "bg-primary"}`}
      />
      <span className="min-w-0 flex-1">
        <span
          className={`block text-sm ${item.readAtUtc ? "font-medium" : "font-semibold"}`}
        >
          {item.title}
        </span>
        {item.body && (
          <span className="mt-1 block text-sm text-muted-foreground">
            {item.body}
          </span>
        )}
        <span className="mt-1 block text-xs text-muted-foreground">
          {formatDateTime(item.createdAtUtc)}
        </span>
      </span>
    </button>
  );
}

export function NotificationBell() {
  const [open, setOpen] = useState(false);
  const { items, unreadCount, loading, error, refresh, markAllRead } =
    useNotifications();
  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="relative rounded-full p-2 text-muted-foreground transition-colors hover:bg-accent hover:text-foreground focus-visible:outline-2 focus-visible:outline-primary"
          aria-label={`Thông báo${unreadCount ? `, ${unreadCount} chưa đọc` : ""}`}
        >
          <Bell className="size-5" />
          {unreadCount > 0 && (
            <span className="absolute -right-1 -top-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-600 px-1 text-[10px] font-semibold text-white">
              {unreadCount > 99 ? "99+" : unreadCount}
            </span>
          )}
        </button>
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-[min(24rem,calc(100vw-1rem))] p-0"
      >
        <div className="flex items-center justify-between border-b px-4 py-3">
          <span className="font-semibold">Thông báo</span>
          {unreadCount > 0 && (
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => {
                void markAllRead();
              }}
            >
              <CheckCheck className="mr-1 size-4" /> Đánh dấu đã đọc
            </Button>
          )}
        </div>
        <div className="max-h-[min(28rem,70vh)] overflow-y-auto">
          {loading && (
            <p className="p-4 text-sm text-muted-foreground">
              Đang tải thông báo...
            </p>
          )}
          {!loading && error && (
            <div className="p-4 text-sm text-destructive">
              {error}{" "}
              <button
                type="button"
                className="underline"
                onClick={() => {
                  void refresh();
                }}
              >
                Thử lại
              </button>
            </div>
          )}
          {!loading && !error && items.length === 0 && (
            <p className="p-4 text-sm text-muted-foreground">
              Chưa có thông báo.
            </p>
          )}
          {items.slice(0, 10).map((item) => (
            <NotificationEntry
              key={item.notificationRecipientId}
              item={item}
              onOpen={() => setOpen(false)}
            />
          ))}
        </div>
        <Link
          href="/notifications"
          onClick={() => setOpen(false)}
          className="block border-t px-4 py-3 text-center text-sm font-medium text-primary hover:bg-accent"
        >
          Xem tất cả thông báo
        </Link>
      </PopoverContent>
    </Popover>
  );
}
