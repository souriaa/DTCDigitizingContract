import { parseApiDate, VIETNAM_TIME_ZONE } from "@/lib/format-date-time";

export function formatDate(value?: string | null) {
  if (!value) return "Chưa cập nhật";
  const normalized = value.trim();
  // A date-only value is a business date, not a timestamp to shift across days.
  const date = /^\d{4}-\d{2}-\d{2}$/.test(normalized)
    ? new Date(`${normalized}T00:00:00Z`)
    : parseApiDate(normalized);
  if (Number.isNaN(date.getTime())) return "Chưa cập nhật";

  return date.toLocaleDateString("vi-VN", {
    timeZone: VIETNAM_TIME_ZONE,
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}
