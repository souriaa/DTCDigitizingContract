export const VIETNAM_TIME_ZONE = "Asia/Ho_Chi_Minh";

export const parseApiDate = (dateString: string) => {
  const normalized = dateString.trim().replace(" ", "T");
  const hasTimeZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(normalized);
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
