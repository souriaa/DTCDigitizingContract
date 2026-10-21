"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

export function CatalogUnitFields({
  idPrefix,
  unitName,
  unitNameEn,
  disabled,
  errors,
  onChange,
}: {
  idPrefix: string;
  unitName?: string | null;
  unitNameEn?: string | null;
  disabled?: boolean;
  errors?: { unitName?: string; unitNameEn?: string };
  onChange: (field: "unitName" | "unitNameEn", value: string) => void;
}) {
  return (
    <>
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-unit`}>
          Đơn vị {!disabled && <span className="text-red-500">*</span>}
        </Label>
        <Input
          id={`${idPrefix}-unit`}
          value={unitName ?? ""}
          maxLength={100}
          required
          disabled={disabled}
          aria-invalid={!!errors?.unitName}
          placeholder="Ví dụ: Giấy phép, Tháng, Gói"
          onChange={(event) => onChange("unitName", event.target.value)}
        />
        {errors?.unitName && (
          <p className="text-xs text-destructive">{errors.unitName}</p>
        )}
      </div>
      <div className="space-y-1.5">
        <Label htmlFor={`${idPrefix}-unit-en`}>
          Đơn vị tiếng Anh{" "}
          {!disabled && <span className="text-red-500">*</span>}
        </Label>
        <Input
          id={`${idPrefix}-unit-en`}
          value={unitNameEn ?? ""}
          maxLength={100}
          required
          disabled={disabled}
          aria-invalid={!!errors?.unitNameEn}
          placeholder="Ví dụ: License, Month, Package"
          onChange={(event) => onChange("unitNameEn", event.target.value)}
        />
        {errors?.unitNameEn && (
          <p className="text-xs text-destructive">{errors.unitNameEn}</p>
        )}
      </div>
    </>
  );
}
