import { parseApiDate } from "@/lib/format-date-time";

interface WaitingTimeProps {
  value?: string;
  referenceTime?: number | null;
}

export function formatWaitingTime(value?: string, referenceTime?: number | null) {
  if (!value) return "Chưa có dữ liệu";
  if (referenceTime == null) return "Đang tính...";

  const elapsedMilliseconds = Math.max(
    0,
    referenceTime - parseApiDate(value).getTime(),
  );
  const elapsedHours = Math.floor(elapsedMilliseconds / 3_600_000);

  if (elapsedHours < 1) return "Dưới 1 giờ";
  if (elapsedHours < 24) return `${elapsedHours} giờ`;
  return `${Math.floor(elapsedHours / 24)} ngày`;
}

export function WaitingTime({ value, referenceTime }: WaitingTimeProps) {
  return <>{formatWaitingTime(value, referenceTime)}</>;
}
