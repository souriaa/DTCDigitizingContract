"use client";
import { Building2 } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ContractAcceptancePartyInput } from "@/services/contract-acceptance-api";

export function DocumentField({
  label,
  value,
  onChange,
  type = "text",
  disabled = false,
  required = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  disabled?: boolean;
  required?: boolean;
}) {
  return (
    <div className="space-y-2">
      <Label>{label} {required && <span className="text-red-500">*</span>}</Label>
      <Input
        type={type}
        value={value}
        disabled={disabled}
        required={required}
        onChange={(event) => onChange(event.target.value)}
        className="bg-background"
      />
    </div>
  );
}

export function DocumentPartyFields({
  title,
  party,
  representativeLabel = "Người đại diện",
  disabled = false,
  onChange,
}: {
  title: string;
  party: ContractAcceptancePartyInput;
  disabled?: boolean;
  representativeLabel?: string;
  onChange: (key: keyof ContractAcceptancePartyInput, value: string) => void;
}) {
  return (
    <div className="space-y-3.5 rounded-xl border border-border/70 bg-card p-4 shadow-xs">
      <div className="flex items-center gap-2 border-b border-border/60 pb-2.5">
        <Building2 className="size-4 text-primary shrink-0" />
        <span className="text-sm font-semibold text-foreground">{title}</span>
      </div>
      <DocumentField
        label="Tên pháp nhân"
        required
        value={party.legalName}
        disabled={true}
        onChange={(value) => onChange("legalName", value)}
      />
      <DocumentField label="Tên pháp nhân tiếng Anh" value={party.legalNameEn ?? ""} disabled onChange={() => {}} />
      <DocumentField disabled={disabled} label="Địa chỉ tiếng Anh" value={party.addressEn ?? ""} onChange={(value) => onChange("addressEn", value)} />
      <DocumentField disabled={disabled} label="Chức vụ tiếng Anh" value={party.representativeTitleEn ?? ""} onChange={(value) => onChange("representativeTitleEn", value)} />
      <div className="grid gap-3 sm:grid-cols-2">
        <DocumentField
          disabled={disabled}
          label={representativeLabel}
          required
          value={party.representativeName}
          onChange={(value) => onChange("representativeName", value)}
        />
        <DocumentField
          disabled={disabled}
          label="Chức vụ"
          value={party.representativeTitle ?? ""}
          onChange={(value) => onChange("representativeTitle", value)}
        />
      </div>
      <DocumentField
        disabled={disabled}
        label="Địa chỉ"
        value={party.address ?? ""}
        onChange={(value) => onChange("address", value)}
      />
      <div className="grid gap-3 sm:grid-cols-3">
        <DocumentField
          disabled={disabled}
          label="Điện thoại"
          value={party.phone ?? ""}
          onChange={(value) => onChange("phone", value)}
        />
        <DocumentField
          disabled={disabled}
          label="Fax"
          value={party.fax ?? ""}
          onChange={(value) => onChange("fax", value)}
        />
        <DocumentField
          disabled={disabled}
          label="Mã số thuế"
          value={party.taxCode ?? ""}
          onChange={(value) => onChange("taxCode", value)}
        />
      </div>
    </div>
  );
}
