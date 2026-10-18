"use client";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import {
  PaymentDueAnchor,
  PaymentDayCountMode,
  type SaveContractTemplatePaymentMilestoneRequest,
} from "@/services/contract-template-api";
export type PaymentMilestoneFieldsValue = Omit<
  SaveContractTemplatePaymentMilestoneRequest,
  "versionRowVersion"
>;
type Draft = PaymentMilestoneFieldsValue;
export function PaymentMilestoneFields({
  value,
  onChange,
  isBilingual,
  disabled = false,
  codeDisabled = false,
}: {
  value: Draft;
  onChange: (next: Draft) => void;
  isBilingual: boolean;
  disabled?: boolean;
  codeDisabled?: boolean;
}) {
  const set = <K extends keyof Draft>(key: K, next: Draft[K]) =>
    onChange({ ...value, [key]: next });
  return (
    <div className="grid gap-3 md:grid-cols-2">
      <div className="space-y-1.5">
        <Label>Mã đợt</Label>
        <Input
          value={value.milestoneCode}
          maxLength={100}
          disabled={disabled || codeDisabled}
          onChange={(e) => set("milestoneCode", e.target.value.toUpperCase())}
        />
      </div>
      <div className="space-y-1.5">
        <Label>Tên đợt</Label>
        <Input
          value={value.titleVi}
          maxLength={500}
          disabled={disabled}
          onChange={(e) => set("titleVi", e.target.value)}
        />
      </div>
      <div className="space-y-1.5">
        <Label>Tỷ lệ (%)</Label>
        <Input
          type="number"
          min={0.0001}
          max={100}
          step="0.0001"
          value={value.paymentPercent}
          disabled={disabled}
          onChange={(e) => set("paymentPercent", Number(e.target.value))}
        />
      </div>
      <div className="space-y-1.5">
        <Label>Thứ tự</Label>
        <Input type="number" value={value.displayOrder} disabled />
      </div>
      <div className="space-y-1.5">
        <Label>Mốc bắt đầu tính hạn</Label>
        <Select
          value={String(value.dueAnchor)}
          disabled={disabled}
          onValueChange={(val) =>
            set("dueAnchor", Number(val) as PaymentDueAnchor)
          }
        >
          <SelectTrigger className="h-9 w-full">
            <SelectValue placeholder="Chọn mốc bắt đầu" />
          </SelectTrigger>
          <SelectContent showSearch={false}>
            <SelectItem value={String(PaymentDueAnchor.ContractSigned)}>
              Ký hợp đồng
            </SelectItem>
            <SelectItem value={String(PaymentDueAnchor.ContractEffectiveDate)}>
              Hợp đồng có hiệu lực
            </SelectItem>
            <SelectItem value={String(PaymentDueAnchor.AcceptanceCompleted)}>
              Hoàn tất nghiệm thu
            </SelectItem>
            <SelectItem value={String(PaymentDueAnchor.PreviousMilestonePaid)}>
              Thanh toán đủ đợt trước
            </SelectItem>
            <SelectItem value={String(PaymentDueAnchor.ManualDate)}>
              Lịch thủ công (Tự điền ngày)
            </SelectItem>
          </SelectContent>
        </Select>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="space-y-1.5">
          <Label>Số ngày</Label>
          <Input
            type="number"
            min={0}
            value={value.dueOffsetDays}
            disabled={disabled}
            onChange={(e) => set("dueOffsetDays", Number(e.target.value))}
          />
        </div>
        <div className="space-y-1.5">
          <Label>Cách đếm</Label>
          <Select
            value={String(value.dayCountMode)}
            disabled={disabled}
            onValueChange={(val) =>
              set("dayCountMode", Number(val) as PaymentDayCountMode)
            }
          >
            <SelectTrigger className="h-9 w-full">
              <SelectValue placeholder="Chọn cách đếm" />
            </SelectTrigger>
            <SelectContent showSearch={false}>
              <SelectItem value={String(PaymentDayCountMode.CalendarDays)}>
                Ngày lịch
              </SelectItem>
              <SelectItem value={String(PaymentDayCountMode.BusinessDays)}>
                Ngày làm việc
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
      <div className="space-y-1.5 md:col-span-2">
        <Label>Điều kiện bổ sung</Label>
        <Textarea
          value={value.conditionVi ?? ""}
          maxLength={2000}
          disabled={disabled}
          onChange={(e) => set("conditionVi", e.target.value)}
        />
      </div>
      {isBilingual && (
        <>
          <div className="space-y-1.5">
            <Label>Tên đợt tiếng Anh</Label>
            <Input
              value={value.titleEn ?? ""}
              maxLength={500}
              disabled={disabled}
              onChange={(e) => set("titleEn", e.target.value)}
            />
          </div>
          <div className="space-y-1.5">
            <Label>Điều kiện tiếng Anh</Label>
            <Input
              value={value.conditionEn ?? ""}
              maxLength={2000}
              disabled={disabled}
              onChange={(e) => set("conditionEn", e.target.value)}
            />
          </div>
        </>
      )}
      <p className="rounded-md bg-muted/60 p-2 text-sm md:col-span-2">
        Dữ liệu này chỉ dùng để theo dõi thanh toán và không tự thay đổi nội
        dung điều khoản.
        {value.dueAnchor === PaymentDueAnchor.ManualDate && (
          <> Ngày bắt đầu tính hạn được nhập tại từng đợt trong hợp đồng.</>
        )}
      </p>
    </div>
  );
}
