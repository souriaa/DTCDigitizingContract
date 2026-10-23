"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import {
  AlertCircle,
  ArrowUpRight,
  Bell,
  BellOff,
  BellRing,
  CalendarDays,
  Check,
  CheckCheck,
  CheckCircle2,
  Clock,
  Copy,
  ExternalLink,
  FileSignature,
  FileText,
  Filter,
  History,
  Inbox,
  Loader2,
  MessageSquare,
  RefreshCw,
  ShieldCheck,
} from "lucide-react";

import { Header } from "@/components/ui/custom/header";
import { WaitingTime } from "@/components/ui/custom/waiting-time";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardTitle } from "@/components/ui/card";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "@/components/ui/empty";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { toast } from "@/components/ui/sonner";
import { useNotifications } from "@/components/notifications/notification-provider";
import {
  notificationApi,
  notificationHref,
  type NotificationItem,
} from "@/services/notification-api";
import { cn } from "@/lib/utils";
import {
  formatDateTime,
  getVietnamDayNumber,
  parseApiDate,
} from "@/lib/format-date-time";

type StatusFilter = "all" | "unread" | "read";

interface CategoryMeta {
  label: string;
  badgeLabel: string;
  icon: React.ComponentType<{ className?: string }>;
  iconBg: string;
  badgeClass: string;
  tabLabel: string;
}

const CATEGORY_MAP: Record<string, CategoryMeta> = {
  approval: {
    label: "Phê duyệt hợp đồng",
    badgeLabel: "Phê duyệt",
    icon: ShieldCheck,
    iconBg:
      "bg-purple-500/10 text-purple-600 dark:bg-purple-500/20 dark:text-purple-400",
    badgeClass:
      "bg-purple-500/10 text-purple-700 dark:text-purple-300 border-purple-200/60 dark:border-purple-800/60",
    tabLabel: "Quy trình phê duyệt",
  },
  negotiation: {
    label: "Đàm phán & Trao đổi",
    badgeLabel: "Đàm phán",
    icon: MessageSquare,
    iconBg:
      "bg-blue-500/10 text-blue-600 dark:bg-blue-500/20 dark:text-blue-400",
    badgeClass:
      "bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-200/60 dark:border-blue-800/60",
    tabLabel: "Trao đổi & Đàm phán",
  },
  signature: {
    label: "Ký kết hợp đồng",
    badgeLabel: "Ký kết",
    icon: FileSignature,
    iconBg:
      "bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400",
    badgeClass:
      "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-200/60 dark:border-emerald-800/60",
    tabLabel: "Tiến độ ký kết",
  },
  closing: {
    label: "Nghiệm thu & Thanh lý",
    badgeLabel: "Nghiệm thu",
    icon: CheckCircle2,
    iconBg:
      "bg-amber-500/10 text-amber-600 dark:bg-amber-500/20 dark:text-amber-400",
    badgeClass:
      "bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-200/60 dark:border-amber-800/60",
    tabLabel: "Nghiệm thu & Đóng hợp đồng",
  },
  terms: {
    label: "Điều khoản hợp đồng",
    badgeLabel: "Điều khoản",
    icon: FileText,
    iconBg: "bg-sky-500/10 text-sky-600 dark:bg-sky-500/20 dark:text-sky-400",
    badgeClass:
      "bg-sky-500/10 text-sky-700 dark:text-sky-300 border-sky-200/60 dark:border-sky-800/60",
    tabLabel: "Điều khoản & Phụ lục",
  },
  "customer-access": {
    label: "Khách hàng truy cập",
    badgeLabel: "Khách hàng",
    icon: ExternalLink,
    iconBg:
      "bg-cyan-500/10 text-cyan-600 dark:bg-cyan-500/20 dark:text-cyan-400",
    badgeClass:
      "bg-cyan-500/10 text-cyan-700 dark:text-cyan-300 border-cyan-200/60 dark:border-cyan-800/60",
    tabLabel: "Cổng truy cập khách hàng",
  },
  activity: {
    label: "Nhật ký hoạt động",
    badgeLabel: "Nhật ký",
    icon: History,
    iconBg:
      "bg-indigo-500/10 text-indigo-600 dark:bg-indigo-500/20 dark:text-indigo-400",
    badgeClass:
      "bg-indigo-500/10 text-indigo-700 dark:text-indigo-300 border-indigo-200/60 dark:border-indigo-800/60",
    tabLabel: "Lịch sử hoạt động",
  },
};

const DEFAULT_CATEGORY: CategoryMeta = {
  label: "Thông báo hệ thống",
  badgeLabel: "Hệ thống",
  icon: Bell,
  iconBg:
    "bg-slate-500/10 text-slate-600 dark:bg-slate-500/20 dark:text-slate-400",
  badgeClass:
    "bg-slate-500/10 text-slate-700 dark:text-slate-300 border-slate-200/60 dark:border-slate-800/60",
  tabLabel: "Chi tiết hợp đồng",
};

const CATEGORY_OPTIONS = [
  { value: "all", label: "Tất cả danh mục" },
  { value: "approval", label: "Phê duyệt" },
  { value: "negotiation", label: "Đàm phán & Trao đổi" },
  { value: "signature", label: "Ký kết" },
  { value: "closing", label: "Nghiệm thu & Đóng" },
  { value: "terms", label: "Điều khoản" },
  { value: "customer-access", label: "Khách hàng" },
  { value: "activity", label: "Nhật ký hoạt động" },
] as const;

function getCategoryMeta(item: NotificationItem): CategoryMeta {
  if (!item.actionKey) return DEFAULT_CATEGORY;
  return CATEGORY_MAP[item.actionKey.toLowerCase()] ?? DEFAULT_CATEGORY;
}

type DateGroupKey = "today" | "yesterday" | "week" | "earlier";

function getDateGroupKey(dateString: string): DateGroupKey {
  const today = getVietnamDayNumber(new Date());
  const itemDay = getVietnamDayNumber(parseApiDate(dateString));

  if (itemDay >= today) return "today";
  if (itemDay >= today - 1) return "yesterday";
  if (itemDay >= today - 7) return "week";
  return "earlier";
}

const GROUP_TITLES: Record<DateGroupKey, string> = {
  today: "Hôm nay",
  yesterday: "Hôm qua",
  week: "7 ngày qua",
  earlier: "Trước đó",
};

export default function NotificationsPage() {
  const router = useRouter();
  const [filter, setFilter] = useState<StatusFilter>("all");
  const [categoryFilter, setCategoryFilter] = useState<string>("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [items, setItems] = useState<NotificationItem[]>([]);
  const [cursor, setCursor] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [loadingMore, setLoadingMore] = useState(false);
  const [markingAll, setMarkingAll] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [retryKey, setRetryKey] = useState(0);

  const requestId = useRef(0);
  const { unreadCount, revision, markAllRead, markRead } = useNotifications();

  // Load initial notifications
  useEffect(() => {
    const current = ++requestId.current;
    let active = true;

    notificationApi
      .list(null, 20, filter)
      .then((page) => {
        if (!active || current !== requestId.current) return;
        setItems(page.items);
        setCursor(page.nextCursor);
        setError(null);
      })
      .catch(() => {
        if (active && current === requestId.current) {
          setError(
            "Không thể tải thông báo. Vui lòng kiểm tra kết nối mạng và thử lại.",
          );
        }
      })
      .finally(() => {
        if (active && current === requestId.current) {
          setLoading(false);
          setRefreshing(false);
        }
      });

    return () => {
      active = false;
    };
  }, [filter, revision, retryKey]);

  // Load more pagination
  const handleLoadMore = async () => {
    if (!cursor || loadingMore) return;
    setLoadingMore(true);
    const current = requestId.current;
    try {
      const page = await notificationApi.list(cursor, 20, filter);
      if (current !== requestId.current) return;
      setItems((previous) => {
        const known = new Set(
          previous.map((item) => item.notificationRecipientId),
        );
        return [
          ...previous,
          ...page.items.filter(
            (item) => !known.has(item.notificationRecipientId),
          ),
        ];
      });
      setCursor(page.nextCursor);
      setError(null);
    } catch {
      if (current === requestId.current) {
        toast.error("Không thể tải thêm thông báo. Vui lòng thử lại.");
      }
    } finally {
      setLoadingMore(false);
    }
  };

  // Refresh manually
  const handleRefresh = () => {
    setRefreshing(true);
    setRetryKey((key) => key + 1);
  };

  // Mark all notifications as read
  const handleMarkAllRead = async () => {
    if (unreadCount === 0 || markingAll) return;
    setMarkingAll(true);
    try {
      await markAllRead();
      setItems((prev) =>
        prev.map((item) => ({
          ...item,
          readAtUtc: item.readAtUtc ?? new Date().toISOString(),
        })),
      );
      toast.success("Đã đánh dấu tất cả thông báo là đã đọc.");
    } catch {
      toast.error("Không thể đánh dấu đã đọc. Vui lòng thử lại.");
    } finally {
      setMarkingAll(false);
    }
  };

  // Mark individual item as read
  const handleMarkItemRead = async (
    e: React.MouseEvent,
    item: NotificationItem,
  ) => {
    e.stopPropagation();
    if (item.readAtUtc) return;

    // Optimistic update
    setItems((prev) =>
      prev.map((it) =>
        it.notificationRecipientId === item.notificationRecipientId
          ? { ...it, readAtUtc: new Date().toISOString() }
          : it,
      ),
    );

    const success = await markRead(item.notificationRecipientId);
    if (!success) {
      toast.error("Lỗi khi cập nhật trạng thái thông báo.");
    }
  };

  // Navigate to target contract and mark read
  const handleOpenNotification = async (item: NotificationItem) => {
    const href = notificationHref(item);

    if (!item.readAtUtc) {
      setItems((prev) =>
        prev.map((it) =>
          it.notificationRecipientId === item.notificationRecipientId
            ? { ...it, readAtUtc: new Date().toISOString() }
            : it,
        ),
      );
      void markRead(item.notificationRecipientId);
    }

    if (href) {
      router.push(href);
    }
  };

  // Copy contract link
  const handleCopyLink = async (
    e: React.MouseEvent,
    item: NotificationItem,
  ) => {
    e.stopPropagation();
    const href = notificationHref(item);
    if (!href) return;
    const url = `${window.location.origin}${href}`;
    try {
      await navigator.clipboard.writeText(url);
      toast.success("Đã sao chép liên kết hợp đồng.");
    } catch {
      toast.error("Không thể sao chép liên kết.");
    }
  };

  // Filter items based on Category & Search Query
  const filteredItems = useMemo(() => {
    return items.filter((item) => {
      // Category filter
      if (categoryFilter !== "all") {
        if (
          !item.actionKey ||
          item.actionKey.toLowerCase() !== categoryFilter
        ) {
          return false;
        }
      }
      // Search query
      if (searchQuery.trim()) {
        const query = searchQuery.toLowerCase().trim();
        const matchTitle = item.title?.toLowerCase().includes(query);
        const matchBody = item.body?.toLowerCase().includes(query);
        const matchSubject =
          String(item.subjectId).includes(query) ||
          `hợp đồng ${item.subjectId}`.includes(query) ||
          `#${item.subjectId}`.includes(query);

        if (!matchTitle && !matchBody && !matchSubject) return false;
      }
      return true;
    });
  }, [items, categoryFilter, searchQuery]);

  // Group items by date buckets
  const groupedItems = useMemo(() => {
    const groups: Record<DateGroupKey, NotificationItem[]> = {
      today: [],
      yesterday: [],
      week: [],
      earlier: [],
    };

    for (const item of filteredItems) {
      const key = getDateGroupKey(item.createdAtUtc);
      groups[key].push(item);
    }

    return groups;
  }, [filteredItems]);

  // Metric stats
  const stats = useMemo(() => {
    const todayCount = items.filter(
      (item) => getDateGroupKey(item.createdAtUtc) === "today",
    ).length;
    const readCount = items.filter((item) => item.readAtUtc !== null).length;

    return {
      total: items.length,
      unread: unreadCount,
      today: todayCount,
      read: readCount,
    };
  }, [items, unreadCount]);

  const hasActiveFilters =
    categoryFilter !== "all" || searchQuery.trim().length > 0;

  const handleResetFilters = () => {
    setCategoryFilter("all");
    setSearchQuery("");
  };

  return (
    <TooltipProvider delayDuration={200}>
      <div className="flex min-h-0 flex-1 flex-col bg-muted/20 dark:bg-background">
        <Header title="Thông báo hệ thống" />

        <div className="flex-1 overflow-y-auto">
          <div className="mx-auto w-full space-y-6 p-4 sm:p-6 lg:p-8">
            {/* Hero & Statistics Banner using shadcn Card */}
            <Card className="gap-6 border-border/80 bg-gradient-to-br from-card via-card to-primary/[0.04] p-5 shadow-xs sm:p-6">
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <CardTitle className="text-2xl font-bold tracking-tight text-foreground sm:text-3xl">
                    Thông báo
                  </CardTitle>
                </div>

                <div className="flex flex-wrap items-center gap-2 self-start sm:self-center">
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="h-9 gap-1.5"
                    disabled={refreshing || loading}
                    onClick={handleRefresh}
                  >
                    <RefreshCw
                      className={cn(
                        "size-3.5",
                        (refreshing || loading) && "animate-spin",
                      )}
                    />
                    <span>Làm mới</span>
                  </Button>

                  <Button
                    type="button"
                    variant="default"
                    size="sm"
                    className="h-9 gap-1.5 shadow-xs"
                    disabled={unreadCount === 0 || markingAll}
                    onClick={() => {
                      void handleMarkAllRead();
                    }}
                  >
                    {markingAll ? (
                      <Loader2 className="size-3.5 animate-spin" />
                    ) : (
                      <CheckCheck className="size-3.5" />
                    )}
                    <span>Đánh dấu tất cả đã đọc</span>
                  </Button>
                </div>
              </div>

              {/* Metric Quick Stats Grid using shadcn Cards */}
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-4 sm:gap-4">
                <Card className="gap-0 border-border/60 bg-background/60 p-3.5 shadow-xs backdrop-blur-sm">
                  <CardContent className="flex items-center gap-3 p-0">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-blue-500/10 text-blue-600 dark:bg-blue-500/20 dark:text-blue-400">
                      <Inbox className="size-5" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-medium text-muted-foreground">
                        Tổng thông báo
                      </p>
                      <p className="text-xl font-bold tracking-tight text-foreground">
                        {loading ? "-" : stats.total}
                      </p>
                    </div>
                  </CardContent>
                </Card>

                <Card className="gap-0 border-border/60 bg-background/60 p-3.5 shadow-xs backdrop-blur-sm">
                  <CardContent className="flex items-center gap-3 p-0">
                    <div
                      className={cn(
                        "flex size-10 shrink-0 items-center justify-center rounded-lg",
                        stats.unread > 0
                          ? "bg-rose-500/10 text-rose-600 dark:bg-rose-500/20 dark:text-rose-400"
                          : "bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400",
                      )}
                    >
                      {stats.unread > 0 ? (
                        <BellRing className="size-5" />
                      ) : (
                        <CheckCircle2 className="size-5" />
                      )}
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-medium text-muted-foreground">
                        Chưa đọc
                      </p>
                      <div className="flex items-center gap-1.5">
                        <p className="text-xl font-bold tracking-tight text-foreground">
                          {loading ? "-" : stats.unread}
                        </p>
                        {stats.unread > 0 && (
                          <span className="inline-block size-2 rounded-full bg-rose-500 ring-2 ring-rose-200 dark:ring-rose-950" />
                        )}
                      </div>
                    </div>
                  </CardContent>
                </Card>

                <Card className="gap-0 border-border/60 bg-background/60 p-3.5 shadow-xs backdrop-blur-sm">
                  <CardContent className="flex items-center gap-3 p-0">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                      <Clock className="size-5" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-medium text-muted-foreground">
                        Hôm nay
                      </p>
                      <p className="text-xl font-bold tracking-tight text-foreground">
                        {loading ? "-" : stats.today}
                      </p>
                    </div>
                  </CardContent>
                </Card>

                <Card className="gap-0 border-border/60 bg-background/60 p-3.5 shadow-xs backdrop-blur-sm">
                  <CardContent className="flex items-center gap-3 p-0">
                    <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-sky-500/10 text-sky-600 dark:bg-sky-500/20 dark:text-sky-400">
                      <Check className="size-5" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-xs font-medium text-muted-foreground">
                        Đã xem
                      </p>
                      <p className="text-xl font-bold tracking-tight text-foreground">
                        {loading ? "-" : stats.read}
                      </p>
                    </div>
                  </CardContent>
                </Card>
              </div>
            </Card>

            {/* Filter & Search Toolbar using shadcn Card & Tabs */}
            <Card className="gap-0 border-border/80 p-4 shadow-xs">
              <CardContent className="flex flex-col gap-3 p-0 sm:flex-row sm:items-center sm:justify-between">
                {/* Segmented Status Tabs using shadcn Tabs */}
                <Tabs
                  value={filter}
                  onValueChange={(val) => {
                    if (val !== filter) {
                      setLoading(true);
                      setFilter(val as StatusFilter);
                    }
                  }}
                >
                  <TabsList className="h-9">
                    <TabsTrigger value="all">Tất cả</TabsTrigger>
                    <TabsTrigger value="unread" className="gap-1.5">
                      <span>Chưa đọc</span>
                      {unreadCount > 0 && (
                        <Badge
                          variant="destructive"
                          className="h-4.5 min-w-4.5 rounded-full px-1 text-[10px] font-bold"
                        >
                          {unreadCount > 99 ? "99+" : unreadCount}
                        </Badge>
                      )}
                    </TabsTrigger>
                    <TabsTrigger value="read">Đã đọc</TabsTrigger>
                  </TabsList>
                </Tabs>

                {/* Category Select & Search Query */}
                <div className="flex flex-1 flex-col gap-2 sm:max-w-md sm:flex-row sm:items-center sm:justify-end">
                  {/* Category Dropdown using shadcn Select */}
                  <div className="w-full sm:w-[190px]">
                    <Select
                      value={categoryFilter}
                      onValueChange={(val) => setCategoryFilter(val)}
                    >
                      <SelectTrigger
                        size="sm"
                        className="h-9 w-full bg-background"
                      >
                        <Filter className="mr-1.5 size-3.5 text-muted-foreground" />
                        <SelectValue placeholder="Danh mục" />
                      </SelectTrigger>
                      <SelectContent showSearch={false} align="end">
                        {CATEGORY_OPTIONS.map((cat) => (
                          <SelectItem key={cat.value} value={cat.value}>
                            {cat.label}
                          </SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>

                  {/* Search Input with Clear Button */}
                  <div className="relative w-full sm:w-[480px]">
                    <Input
                      type="text"
                      placeholder="Tìm theo tiêu đề, #hợp đồng..."
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      className="h-9 bg-background pr-8 text-sm"
                    />
                  </div>
                </div>
              </CardContent>
            </Card>

            {/* Active Filters Reset Alert */}
            {hasActiveFilters && (
              <Alert className="flex items-center justify-between border-primary/20 bg-primary/5 px-4 py-2 text-xs text-muted-foreground">
                <div className="flex items-center gap-2">
                  <Filter className="size-3.5 text-primary shrink-0" />
                  <span>
                    Đang lọc kết quả:{" "}
                    {categoryFilter !== "all" && (
                      <strong className="text-foreground">
                        {
                          CATEGORY_OPTIONS.find(
                            (c) => c.value === categoryFilter,
                          )?.label
                        }{" "}
                      </strong>
                    )}
                    {searchQuery && (
                      <span>
                        với từ khóa &ldquo;
                        <strong className="text-foreground">
                          {searchQuery}
                        </strong>
                        &rdquo;
                      </span>
                    )}
                    {" — "}
                    tìm thấy <strong>{filteredItems.length}</strong> thông báo.
                  </span>
                </div>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="h-7 text-xs font-medium text-primary hover:text-primary/90"
                  onClick={handleResetFilters}
                >
                  Xóa bộ lọc
                </Button>
              </Alert>
            )}

            {/* Main Notifications Content */}
            <div className="space-y-6">
              {/* Loading Skeleton */}
              {loading && (
                <div className="space-y-3">
                  {Array.from({ length: 5 }).map((_, idx) => (
                    <Card
                      key={idx}
                      className="flex flex-row items-start gap-4 border-border/60 bg-card p-4 shadow-xs"
                    >
                      <Skeleton className="size-10 shrink-0 rounded-lg" />
                      <div className="flex-1 space-y-2">
                        <div className="flex items-center justify-between gap-4">
                          <Skeleton className="h-4 w-48" />
                          <Skeleton className="h-3 w-20" />
                        </div>
                        <Skeleton className="h-3.5 w-full max-w-md" />
                        <div className="flex items-center gap-2 pt-1">
                          <Skeleton className="h-5 w-20 rounded-md" />
                          <Skeleton className="h-5 w-24 rounded-md" />
                        </div>
                      </div>
                    </Card>
                  ))}
                </div>
              )}

              {/* Error State using shadcn Alert */}
              {!loading && error && (
                <Alert
                  variant="destructive"
                  className="flex flex-col items-center justify-center border-destructive/30 bg-destructive/5 p-8 text-center"
                >
                  <div className="mb-2 flex size-12 items-center justify-center rounded-full bg-destructive/10 text-destructive">
                    <AlertCircle className="size-6" />
                  </div>
                  <AlertTitle className="text-base font-semibold text-foreground">
                    Không thể tải thông báo
                  </AlertTitle>
                  <AlertDescription className="mt-1 max-w-md text-center text-sm text-muted-foreground">
                    {error}
                  </AlertDescription>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    className="mt-4 gap-1.5 border-destructive/30 hover:bg-destructive/10"
                    onClick={() => {
                      setLoading(true);
                      setRetryKey((k) => k + 1);
                    }}
                  >
                    <RefreshCw className="size-3.5" />
                    Thử lại
                  </Button>
                </Alert>
              )}

              {/* Empty States using shadcn Empty component */}
              {!loading && !error && filteredItems.length === 0 && (
                <Empty className="rounded-2xl border border-dashed border-border/80 bg-card/50 p-12">
                  {filter === "unread" && !hasActiveFilters ? (
                    <>
                      <EmptyMedia
                        variant="icon"
                        className="size-14 rounded-2xl bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400"
                      >
                        <CheckCircle2 className="size-7" />
                      </EmptyMedia>
                      <EmptyHeader>
                        <EmptyTitle className="text-lg font-semibold text-foreground">
                          Bạn đã cập nhật hết thông tin!
                        </EmptyTitle>
                        <EmptyDescription>
                          Không có thông báo chưa đọc nào cần xử lý. Tất cả các
                          cập nhật hợp đồng đã được xem.
                        </EmptyDescription>
                      </EmptyHeader>
                      <EmptyContent>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={() => {
                            setLoading(true);
                            setFilter("all");
                          }}
                        >
                          Xem tất cả thông báo
                        </Button>
                      </EmptyContent>
                    </>
                  ) : hasActiveFilters ? (
                    <>
                      <EmptyMedia
                        variant="icon"
                        className="size-14 rounded-2xl bg-muted text-muted-foreground"
                      ></EmptyMedia>
                      <EmptyHeader>
                        <EmptyTitle className="text-lg font-semibold text-foreground">
                          Không tìm thấy thông báo phù hợp
                        </EmptyTitle>
                        <EmptyDescription>
                          Không có kết quả nào khớp với tiêu chí tìm kiếm hoặc
                          bộ lọc hiện tại của bạn.
                        </EmptyDescription>
                      </EmptyHeader>
                      <EmptyContent>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          onClick={handleResetFilters}
                        >
                          Đặt lại bộ lọc
                        </Button>
                      </EmptyContent>
                    </>
                  ) : (
                    <>
                      <EmptyMedia
                        variant="icon"
                        className="size-14 rounded-2xl bg-muted text-muted-foreground"
                      >
                        <BellOff className="size-7" />
                      </EmptyMedia>
                      <EmptyHeader>
                        <EmptyTitle className="text-lg font-semibold text-foreground">
                          Hộp thông báo trống
                        </EmptyTitle>
                        <EmptyDescription>
                          Hiện tại chưa có thông báo nào được gửi đến bạn từ các
                          quy trình hợp đồng.
                        </EmptyDescription>
                      </EmptyHeader>
                    </>
                  )}
                </Empty>
              )}

              {/* Timeline Grouped Notifications using shadcn Cards */}
              {!loading &&
                !error &&
                (Object.keys(groupedItems) as DateGroupKey[]).map(
                  (groupKey) => {
                    const groupList = groupedItems[groupKey];
                    if (groupList.length === 0) return null;

                    return (
                      <section key={groupKey} className="space-y-3">
                        {/* Timeline Section Header with shadcn Separator */}
                        <div className="sticky top-0 z-10 -ml-8 -mr-8 flex items-center gap-2 py-1.5 backdrop-blur-md dark:bg-background/80">
                          <div className="flex items-center gap-1.5 text-xs font-semibold text-foreground mx-10">
                            <CalendarDays className="size-3.5 text-primary" />
                            <span>{GROUP_TITLES[groupKey]}</span>
                            <Badge
                              variant="secondary"
                              className="ml-1 h-4 min-w-4 px-1.5 text-[10px] font-medium"
                            >
                              {groupList.length}
                            </Badge>
                          </div>
                        </div>

                        {/* Notification Cards List using shadcn Card */}
                        <div className="space-y-2.5">
                          {groupList.map((item) => {
                            const meta = getCategoryMeta(item);
                            const IconComp = meta.icon;
                            const isUnread = !item.readAtUtc;
                            const href = notificationHref(item);

                            return (
                              <Card
                                key={item.notificationRecipientId}
                                onClick={() => {
                                  void handleOpenNotification(item);
                                }}
                                className={cn(
                                  "group relative flex cursor-pointer flex-col gap-3 p-4 transition-all duration-200 sm:flex-row sm:items-start sm:gap-4",
                                  isUnread
                                    ? "border-l-4 border-l-primary border-t-border/70 border-r-border/70 border-b-border/70 bg-card shadow-xs hover:border-primary/40 hover:shadow-md"
                                    : "border-border/60 bg-card/75 opacity-90 hover:opacity-100 hover:border-border hover:bg-card hover:shadow-xs",
                                )}
                              >
                                {/* Left Icon Container */}
                                <div
                                  className={cn(
                                    "flex size-10 shrink-0 items-center justify-center rounded-xl transition-transform group-hover:scale-105",
                                    meta.iconBg,
                                  )}
                                >
                                  <IconComp className="size-5" />
                                </div>

                                {/* Notification Main Info */}
                                <div className="min-w-0 flex-1 space-y-1.5">
                                  {/* Title and Timestamp Row */}
                                  <div className="flex items-start justify-between gap-3">
                                    <div className="flex items-center gap-2">
                                      {isUnread && (
                                        <span
                                          className="size-2 shrink-0 rounded-full bg-primary ring-2 ring-primary/20"
                                          title="Chưa đọc"
                                        />
                                      )}
                                      <h4
                                        className={cn(
                                          "text-sm tracking-tight text-foreground transition-colors group-hover:text-primary",
                                          isUnread
                                            ? "font-bold text-foreground"
                                            : "font-medium text-foreground/90",
                                        )}
                                      >
                                        {item.title}
                                      </h4>
                                    </div>

                                    <Tooltip>
                                      <TooltipTrigger asChild>
                                        <span className="shrink-0 text-xs font-normal text-muted-foreground">
                                          <WaitingTime
                                            value={item.createdAtUtc}
                                            referenceTime={Date.now()}
                                          />
                                        </span>
                                      </TooltipTrigger>
                                      <TooltipContent side="top">
                                        {formatDateTime(item.createdAtUtc)}
                                      </TooltipContent>
                                    </Tooltip>
                                  </div>

                                  {/* Body Description */}
                                  {item.body && (
                                    <p className="text-xs text-muted-foreground/95 line-clamp-2 leading-relaxed sm:text-sm">
                                      {item.body}
                                    </p>
                                  )}

                                  {/* Badges and Quick Actions Footer */}
                                  <div className="flex flex-wrap items-center justify-between gap-2 pt-1.5">
                                    <div className="flex flex-wrap items-center gap-1.5">
                                      {/* Category Badge using shadcn Badge */}
                                      <Badge
                                        variant="outline"
                                        className={cn(
                                          "gap-1 px-2 py-0.5 text-[11px] font-medium",
                                          meta.badgeClass,
                                        )}
                                      >
                                        {meta.badgeLabel}
                                      </Badge>

                                      {/* Contract ID Tag using shadcn Badge */}
                                      {item.subjectId > 0 && (
                                        <Badge
                                          variant="outline"
                                          className="h-5 gap-1 bg-muted/30 px-1.5 text-[11px] font-mono text-muted-foreground hover:text-foreground"
                                        >
                                          #{item.subjectId}
                                        </Badge>
                                      )}

                                      {/* Action target hint */}
                                      {item.actionKey && (
                                        <span className="hidden text-[11px] text-muted-foreground/75 sm:inline">
                                          · {meta.tabLabel}
                                        </span>
                                      )}
                                    </div>

                                    {/* Hover Interactive Actions using shadcn Button & Tooltip */}
                                    <div className="flex items-center gap-1 transition-opacity">
                                      {isUnread && (
                                        <Tooltip>
                                          <TooltipTrigger asChild>
                                            <Button
                                              type="button"
                                              variant="ghost"
                                              size="icon"
                                              className="size-7 text-muted-foreground hover:bg-primary/10 hover:text-primary"
                                              onClick={(e) => {
                                                void handleMarkItemRead(
                                                  e,
                                                  item,
                                                );
                                              }}
                                            >
                                              <Check className="size-3.5" />
                                            </Button>
                                          </TooltipTrigger>
                                          <TooltipContent side="top">
                                            Đánh dấu đã đọc
                                          </TooltipContent>
                                        </Tooltip>
                                      )}

                                      {href && (
                                        <>
                                          <Tooltip>
                                            <TooltipTrigger asChild>
                                              <Button
                                                type="button"
                                                variant="ghost"
                                                size="icon"
                                                className="size-7 text-muted-foreground hover:bg-accent hover:text-foreground"
                                                onClick={(e) => {
                                                  void handleCopyLink(e, item);
                                                }}
                                              >
                                                <Copy className="size-3.5" />
                                              </Button>
                                            </TooltipTrigger>
                                            <TooltipContent side="top">
                                              Sao chép liên kết
                                            </TooltipContent>
                                          </Tooltip>

                                          <Tooltip>
                                            <TooltipTrigger asChild>
                                              <Button
                                                type="button"
                                                variant="ghost"
                                                size="icon"
                                                className="size-7 text-muted-foreground hover:bg-primary/10 hover:text-primary"
                                                onClick={(e) => {
                                                  e.stopPropagation();
                                                  void handleOpenNotification(
                                                    item,
                                                  );
                                                }}
                                              >
                                                <ArrowUpRight className="size-3.5" />
                                              </Button>
                                            </TooltipTrigger>
                                            <TooltipContent side="top">
                                              Mở hợp đồng
                                            </TooltipContent>
                                          </Tooltip>
                                        </>
                                      )}
                                    </div>
                                  </div>
                                </div>
                              </Card>
                            );
                          })}
                        </div>
                      </section>
                    );
                  },
                )}

              {/* Load More Pagination using shadcn Button */}
              {!loading && !error && cursor && (
                <div className="flex flex-col items-center justify-center gap-2 pt-4">
                  <p className="text-xs text-muted-foreground">
                    Đang hiển thị <strong>{filteredItems.length}</strong> thông
                    báo
                  </p>
                  <Button
                    type="button"
                    variant="outline"
                    className="h-10 min-w-[200px] gap-2 rounded-xl shadow-xs hover:border-primary/40"
                    disabled={loadingMore}
                    onClick={() => {
                      void handleLoadMore();
                    }}
                  >
                    {loadingMore ? (
                      <>
                        <Loader2 className="size-4 animate-spin" />
                        <span>Đang tải thêm...</span>
                      </>
                    ) : (
                      <>
                        <ArrowUpRight className="size-4 rotate-45" />
                        <span>Tải thêm thông báo</span>
                      </>
                    )}
                  </Button>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </TooltipProvider>
  );
}
