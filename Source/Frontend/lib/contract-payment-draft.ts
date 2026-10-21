import {
  ContractTermKind,
  type ContractPaymentMilestoneDraft,
} from "@/services/contract-api";

export function getPaymentDraftError(
  terms: ReadonlyArray<{
    termKind: ContractTermKind;
    paymentMilestones: ReadonlyArray<ContractPaymentMilestoneDraft>;
  }>,
  bilingual: boolean,
): string | null {
  if (
    terms.filter((term) => term.termKind === ContractTermKind.Payment).length >
    1
  )
    return "Mỗi hợp đồng chỉ có một điều khoản thanh toán.";
  for (const term of terms) {
    const rows = term.paymentMilestones;
    if (term.termKind !== ContractTermKind.Payment && rows.length)
      return "Chỉ điều khoản thanh toán mới được có các đợt thanh toán.";
    const codes = new Set<string>();
    for (const [index, row] of rows.entries()) {
      const code = row.milestoneCode.trim().toUpperCase();
      if (!code || !row.titleVi.trim())
        return "Vui lòng nhập mã và tên cho tất cả đợt thanh toán.";
      if (codes.has(code)) return `Mã đợt thanh toán '${code}' bị trùng.`;
      codes.add(code);
      if (
        !Number.isFinite(row.paymentPercent) ||
        row.paymentPercent <= 0 ||
        row.paymentPercent > 100 ||
        Math.abs(
          row.paymentPercent * 10000 - Math.round(row.paymentPercent * 10000),
        ) > 0.0000001
      )
        return `Tỷ lệ của đợt '${code}' phải lớn hơn 0, không quá 100 và tối đa 4 chữ số thập phân.`;
      if (
        !Number.isInteger(row.dueOffsetDays) ||
        row.dueOffsetDays < 0 ||
        row.dueOffsetDays > 2147483647
      )
        return `Số ngày của đợt '${code}' phải là số nguyên không âm hợp lệ.`;
      if (
        ![1, 2, 3, 4, 5].includes(row.dueAnchor) ||
        ![1, 2].includes(row.dayCountMode)
      )
        return `Cấu hình tính hạn của đợt '${code}' không hợp lệ.`;
      if (index === 0 && row.dueAnchor === 4)
        return "Đợt đầu tiên không được phụ thuộc thanh toán đợt trước.";
      if (
        bilingual &&
        (!row.titleEn?.trim() ||
          (row.conditionVi?.trim() && !row.conditionEn?.trim()))
      )
        return `Đợt '${code}' chưa đủ tên hoặc điều kiện tiếng Anh.`;
    }
    if (
      Math.round(
        rows.reduce((sum, row) => sum + row.paymentPercent, 0) * 10000,
      ) > 1000000
    )
      return "Tổng tỷ lệ thanh toán không được vượt quá 100%.";
  }
  return null;
}
