"use client";

import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { format } from "date-fns";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { DateFilter } from "@/components/ui/custom/date-filter";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";
import { PaymentMilestoneFields } from "./payment-milestone-fields";
import type { ContractPaymentMilestoneDraft } from "@/services/contract-api";
import {
  PaymentDueAnchor,
  PaymentDayCountMode,
} from "@/services/contract-template-api";

export function PaymentPlanEditor({
  rows,
  onChange,
  isBilingual,
  editable = true,
}: {
  rows: ContractPaymentMilestoneDraft[];
  onChange: (rows: ContractPaymentMilestoneDraft[]) => void;
  isBilingual: boolean;
  editable?: boolean;
}) {
  const total =
    Math.round(rows.reduce((sum, row) => sum + row.paymentPercent, 0) * 10000) /
    10000;
  const reorder = (next: ContractPaymentMilestoneDraft[]) =>
    onChange(next.map((row, index) => ({ ...row, displayOrder: index + 1 })));
  const move = (index: number, direction: -1 | 1) => {
    const next = [...rows];
    [next[index], next[index + direction]] = [
      next[index + direction],
      next[index],
    ];
    reorder(next);
  };
  return (
    <div className="space-y-3 rounded-xl border bg-muted/20 p-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="font-semibold">Các đợt thanh toán · Tổng {total}%</h4>
        {editable && (
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => {
              let index = 1;
              while (
                rows.some(
                  (row) =>
                    row.milestoneCode.toUpperCase() === `PAYMENT_${index}`,
                )
              )
                index++;
              onChange([
                ...rows,
                {
                  milestoneCode: `PAYMENT_${index}`,
                  titleVi: `Đợt ${rows.length + 1}`,
                  titleEn: "",
                  paymentPercent: 0,
                  dueAnchor: rows.length
                    ? PaymentDueAnchor.PreviousMilestonePaid
                    : PaymentDueAnchor.ContractSigned,
                  dueOffsetDays: 0,
                  dayCountMode: PaymentDayCountMode.CalendarDays,
                  conditionVi: "",
                  conditionEn: "",
                  displayOrder: rows.length + 1,
                },
              ]);
            }}
          >
            <Plus className="size-4" />
            Thêm đợt
          </Button>
        )}
      </div>
      {total !== 100 && (
        <p className="text-sm text-amber-700">
          Cần tổng tỷ lệ đúng 100% trước khi gửi duyệt.
        </p>
      )}
      {rows.map((row, index) => (
        <div
          key={
            row.paymentMilestoneId ??
            row.sourceTemplatePaymentMilestoneId ??
            `new-${index}`
          }
          className="space-y-3 rounded-lg border bg-background p-3"
        >
          {editable && (
            <div className="flex justify-end gap-1">
              <Button
                type="button"
                size="icon"
                variant="ghost"
                disabled={index === 0}
                aria-label="Chuyển đợt lên"
                onClick={() => move(index, -1)}
              >
                <ArrowUp className="size-4" />
              </Button>
              <Button
                type="button"
                size="icon"
                variant="ghost"
                disabled={index === rows.length - 1}
                aria-label="Chuyển đợt xuống"
                onClick={() => move(index, 1)}
              >
                <ArrowDown className="size-4" />
              </Button>
              <Button
                type="button"
                size="icon"
                variant="ghost"
                aria-label="Xóa đợt thanh toán"
                onClick={() =>
                  showConfirmToast({
                    title: "Xóa đợt thanh toán?",
                    description: `Xóa “${row.titleVi}” khỏi kế hoạch đang soạn.`,
                    confirmLabel: "Xóa",
                    cancelLabel: "Hủy",
                    onConfirm: () =>
                      reorder(rows.filter((_, i) => i !== index)),
                  })
                }
              >
                <Trash2 className="size-4 text-destructive" />
              </Button>
            </div>
          )}
          <PaymentMilestoneFields
            value={{
              ...row,
              dueAnchor: row.dueAnchor as PaymentDueAnchor,
              dayCountMode: row.dayCountMode as PaymentDayCountMode,
            }}
            disabled={!editable}
            codeDisabled={(row.paymentMilestoneId ?? 0) > 0}
            isBilingual={isBilingual}
            onChange={(value) =>
              onChange(
                rows.map((item, i) =>
                  i === index ? { ...item, ...value } : item,
                ),
              )
            }
          />
          {row.dueAnchor === PaymentDueAnchor.ManualDate && (
            <div className="space-y-2">
              <Label>Ngày bắt đầu tính hạn</Label>
              <DateFilter
                date={
                  row.anchorDate
                    ? new Date(`${row.anchorDate.slice(0, 10)}T00:00:00`)
                    : undefined
                }
                disabled={!editable}
                onChange={(date) =>
                  onChange(
                    rows.map((item, i) =>
                      i === index
                        ? {
                            ...item,
                            anchorDate: date
                              ? format(date, "yyyy-MM-dd")
                              : null,
                          }
                        : item,
                    ),
                  )
                }
              />
            </div>
          )}
        </div>
      ))}
    </div>
  );
}
