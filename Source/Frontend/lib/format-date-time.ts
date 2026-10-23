export const VIETNAM_TIME_ZONE = "Asia/Ho_Chi_Minh";

const vietnamDayFormatter = new Intl.DateTimeFormat("en-CA", {
  timeZone: VIETNAM_TIME_ZONE,
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
});

// Calendar-day index for display groups such as Today/Yesterday, not API filters.
export const getVietnamDayNumber = (date: Date) => {
  if (Number.isNaN(date.getTime())) return NaN;
  const parts = vietnamDayFormatter.formatToParts(date);
  const get = (type: string) =>
    Number(parts.find((part) => part.type === type)?.value);
  return Date.UTC(get("year"), get("month") - 1, get("day")) / 86_400_000;
};

export const parseApiDate = (dateString: string) => {
  const normalized = dateString.trim().replace(" ", "T");
  const hasTimeZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized);

  // SQL Server DateTime không giữ DateTimeKind nên ASP.NET có thể trả UTC
  // nhưng thiếu hậu tố Z. Chuẩn hóa về UTC trước khi đưa cho trình duyệt.
  return new Date(hasTimeZone ? normalized : `${normalized}Z`);
};

export const formatDateTime = (dateString?: string | null) => {
  if (!dateString) return "N/A";

  const date = parseApiDate(dateString);
  if (Number.isNaN(date.getTime())) return "N/A";

  return date.toLocaleString("vi-VN", {
    timeZone: VIETNAM_TIME_ZONE,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
};

export const formatTime = (dateString?: string | null, includeSeconds = false) => {
  if (!dateString) return "N/A";
  const date = parseApiDate(dateString);
  if (Number.isNaN(date.getTime())) return "N/A";
  return date.toLocaleTimeString("vi-VN", {
    timeZone: VIETNAM_TIME_ZONE,
    hourCycle: "h23",
    hour: "2-digit",
    minute: "2-digit",
    ...(includeSeconds ? { second: "2-digit" as const } : {}),
  });
};
