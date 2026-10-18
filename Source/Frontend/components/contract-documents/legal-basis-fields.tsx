"use client";

import { TermLanguageSection } from "@/components/contract-documents/term-language-section";
import { CodeInput } from "@/components/ui/custom/code-input";
import { ContractRichTextContent } from "@/components/ui/custom/contract-rich-text-content";
import { ContractRichTextEditor } from "@/components/ui/custom/contract-rich-text-editor";
import { Label } from "@/components/ui/label";

export interface LegalBasisFieldsValue {
  basisCode: string;
  contentVi: string;
  contentEn?: string | null;
}

export function LegalBasisFields({
  value,
  id,
  isBilingual,
  disabled = false,
  codeDisabled = false,
  showCode = true,
  onChange,
}: {
  value: LegalBasisFieldsValue;
  id: string;
  isBilingual: boolean;
  disabled?: boolean;
  codeDisabled?: boolean;
  showCode?: boolean;
  onChange?: (field: keyof LegalBasisFieldsValue, value: string) => void;
}) {
  const vietnameseContent = (
    <div className="min-w-0 space-y-2">
      {!disabled && (
        <Label htmlFor={`${id}-vi`}>
          Nội dung căn cứ tiếng Việt <span className="text-destructive">*</span>
        </Label>
      )}
      {disabled ? (
        <ContractRichTextContent value={value.contentVi} className="text-foreground" />
      ) : (
        <ContractRichTextEditor
          id={`${id}-vi`}
          value={value.contentVi}
          onChange={(next) => onChange?.("contentVi", next)}
          ariaLabel="Nội dung căn cứ tiếng Việt"
        />
      )}
    </div>
  );
  const englishContent = (
    <div className="min-w-0 space-y-2">
      {!disabled && (
        <Label htmlFor={`${id}-en`}>
          Nội dung căn cứ tiếng Anh <span className="text-destructive">*</span>
        </Label>
      )}
      {disabled ? (
        <ContractRichTextContent
          value={value.contentEn}
          emptyText="Chưa có nội dung tiếng Anh"
          className="text-foreground"
        />
      ) : (
        <ContractRichTextEditor
          id={`${id}-en`}
          value={value.contentEn}
          onChange={(next) => onChange?.("contentEn", next)}
          ariaLabel="Nội dung căn cứ tiếng Anh"
        />
      )}
    </div>
  );

  return (
    <div className="min-w-0 space-y-4">
      {showCode && (
        <div className="space-y-2">
          <Label htmlFor={`${id}-code`}>Mã căn cứ</Label>
          <CodeInput
            id={`${id}-code`}
            value={value.basisCode}
            maxLength={100}
            disabled={disabled || codeDisabled}
            onValueChange={(next) => onChange?.("basisCode", next)}
          />
        </div>
      )}
      {isBilingual ? (
        <>
          <TermLanguageSection language="en">{englishContent}</TermLanguageSection>
          <TermLanguageSection language="vi">{vietnameseContent}</TermLanguageSection>
        </>
      ) : (
        vietnameseContent
      )}
    </div>
  );
}
