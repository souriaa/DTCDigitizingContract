export const VIETNAM_TIME_ZONE = "Asia/Ho_Chi_Minh";

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
