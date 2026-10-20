"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export function ContractItemUnitFields({
  idPrefix, unitName, unitNameEn, isBilingual, disabled, invalidUnitName, invalidUnitNameEn, onChange,
}: {
  idPrefix: string;
  unitName?: string | null;
  unitNameEn?: string | null;
  isBilingual: boolean;
  disabled?: boolean;
  invalidUnitName?: boolean;
  invalidUnitNameEn?: boolean;
  onChange: (field: "unitName" | "unitNameEn", value: string) => void;
}) {
  return (
    <>
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-unit`} className="text-xs text-muted-foreground">
          Đơn vị <span className="text-red-500">*</span>
        </Label>
        <Input id={`${idPrefix}-unit`} value={unitName ?? ""} maxLength={100} required
          disabled={disabled} aria-invalid={invalidUnitName} placeholder="Ví dụ: Giấy phép, Tháng, Gói"
          onChange={(event) => onChange("unitName", event.target.value)} />
      </div>
      {isBilingual && (
        <div className="space-y-1.5">
          <Label htmlFor={`${idPrefix}-unit-en`} className="text-xs text-muted-foreground">
            Đơn vị tiếng Anh <span className="text-red-500">*</span>
          </Label>
          <Input id={`${idPrefix}-unit-en`} value={unitNameEn ?? ""} maxLength={100} required
            disabled={disabled} aria-invalid={invalidUnitNameEn} placeholder="Ví dụ: License, Month, Package"
            onChange={(event) => onChange("unitNameEn", event.target.value)} />
        </div>
      )}
    </>
  );
}
