"use client";

import { useEffect, useRef, useState } from "react";
import { TermLanguageSection } from "@/components/contract-documents/term-language-section";
import { CodeInput } from "@/components/ui/custom/code-input";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { ContractRichTextEditor } from "@/components/ui/custom/contract-rich-text-editor";
import { ContractRichTextContent } from "@/components/ui/custom/contract-rich-text-content";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ContractTermKind } from "@/services/contract-api";

export interface TermFormValue {
  code: string;
  title: string;
  titleEn: string;
  content: string;
  contentEn: string;
  bilingualContentMode: number;
}

export function TermFields({
  value,
  onChange,
  id,
  isBilingual,
  disabled = false,
  codeDisabled = false,
  kind,
  onKindChange,
  paymentDisabled = false,
  showCode = true,
  titleLabel = "Tiêu đề",
  contentLabel = "Nội dung",
  englishTitlePlaceholder,
  englishContentPlaceholder,
}: {
  value: TermFormValue;
  onChange: (value: TermFormValue) => void;
  id: string;
  isBilingual: boolean;
  disabled?: boolean;
  codeDisabled?: boolean;
  kind?: ContractTermKind;
  onKindChange?: (kind: ContractTermKind) => void;
  paymentDisabled?: boolean;
  showCode?: boolean;
  titleLabel?: string;
  contentLabel?: string;
  englishTitlePlaceholder?: string;
  englishContentPlaceholder?: string;
}) {
  const [pendingMode, setPendingMode] = useState<number | null>(null);
  const [contentSource, setContentSource] = useState("english");
  const previousDrafts = useRef<
    Partial<Record<number, Pick<TermFormValue, "content" | "contentEn">>>
  >({});
  useEffect(() => {
    previousDrafts.current = {};
    setPendingMode(null);
  }, [id, value.code, disabled]);
  const set = (key: keyof TermFormValue, next: string) =>
    onChange({ ...value, [key]: next });
  const changeMode = (mode: number) => {
    if (mode === value.bilingualContentMode) return;
    if (!value.content && !value.contentEn) {
      onChange({ ...value, bilingualContentMode: mode });
      return;
    }
    setContentSource(mode === 2 && value.contentEn ? "english" : "vietnamese");
    setPendingMode(mode);
  };
  const confirmModeChange = () => {
    if (pendingMode == null) return;
    previousDrafts.current[value.bilingualContentMode] = {
      content: value.content,
      contentEn: value.contentEn,
    };
    const previous = previousDrafts.current[pendingMode];
    if (contentSource === "restore" && previous) {
      onChange({ ...value, ...previous, bilingualContentMode: pendingMode });
    } else if (pendingMode === 2) {
      onChange({
        ...value,
        bilingualContentMode: 2,
        contentEn: "",
        content:
          contentSource === "english"
            ? value.contentEn
            : contentSource === "vietnamese"
              ? value.content
              : "",
      });
    } else {
      onChange({
        ...value,
        bilingualContentMode: 1,
        content: contentSource === "vietnamese" ? value.content : "",
        contentEn: contentSource === "english" ? value.content : "",
      });
    }
    setPendingMode(null);
  };
  const vietnameseTitleField = (
    <div className="space-y-2">
      <Label htmlFor={id + "-title"}>
        {titleLabel}
        {isBilingual ? " tiếng Việt" : ""}{" "}
        <span className="text-destructive">*</span>
      </Label>
      <Input
        id={id + "-title"}
        value={value.title}
        disabled={disabled}
        maxLength={500}
        onChange={(event) => set("title", event.target.value)}
      />
    </div>
  );
  const englishTitleField = (
    <div className="space-y-2">
      <Label htmlFor={id + "-title-en"}>
        Tiêu đề tiếng Anh <span className="text-destructive">*</span>
      </Label>
      <Input
        id={id + "-title-en"}
        value={value.titleEn}
        disabled={disabled}
        maxLength={500}
        placeholder={englishTitlePlaceholder}
        onChange={(event) => set("titleEn", event.target.value)}
      />
    </div>
  );
  const englishContentField = (
    <div className="space-y-2">
      <Label htmlFor={id + "-content-en"}>Nội dung tiếng Anh</Label>
      {disabled ? (
        <ContractRichTextContent value={value.contentEn} />
      ) : (
        <ContractRichTextEditor
          id={id + "-content-en"}
          value={value.contentEn}
          placeholder={englishContentPlaceholder}
          onChange={(next) => set("contentEn", next)}
          ariaLabel="Nội dung điều khoản tiếng Anh"
        />
      )}
    </div>
  );
  const mainContentField = (
    <div className="space-y-2">
      <Label htmlFor={id + "-content"}>
        {value.bilingualContentMode === 2
          ? "Nội dung song ngữ"
          : contentLabel + (isBilingual ? " tiếng Việt" : "")}
      </Label>
      {disabled ? (
        <ContractRichTextContent value={value.content} />
      ) : (
        <ContractRichTextEditor
          id={id + "-content"}
          value={value.content}
          onChange={(next) => set("content", next)}
        />
      )}
    </div>
  );
  return (
    <div className="grid min-w-0 gap-4 py-2 sm:grid-cols-2">
      {onKindChange && (
        <div className="space-y-2 sm:col-span-2">
          <Label htmlFor={id + "-kind"}>Loại điều khoản</Label>
          <Select
            value={String(kind)}
            disabled={disabled}
            onValueChange={(next) =>
              onKindChange(Number(next) as ContractTermKind)
            }
          >
            <SelectTrigger id={id + "-kind"} className="w-full">
              <SelectValue />
            </SelectTrigger>
            <SelectContent showSearch={false}>
              <SelectItem value={String(ContractTermKind.General)}>
                Điều khoản thường
              </SelectItem>
              <SelectItem
                value={String(ContractTermKind.Payment)}
                disabled={paymentDisabled}
              >
                Điều khoản thanh toán
              </SelectItem>
            </SelectContent>
          </Select>
        </div>
      )}
      {showCode && (
        <div className={isBilingual ? "space-y-2 sm:col-span-2" : "space-y-2"}>
          <Label htmlFor={id + "-code"}>
            Mã điều khoản <span className="text-destructive">*</span>
          </Label>
          <CodeInput
            id={id + "-code"}
            value={value.code}
            disabled={disabled || codeDisabled}
            maxLength={100}
            onValueChange={(next) => set("code", next)}
          />
        </div>
      )}
      {!isBilingual && (
        <div className={showCode ? "min-w-0" : "min-w-0 sm:col-span-2"}>
          {vietnameseTitleField}
        </div>
      )}
      {isBilingual && (
        <>
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor={id + "-content-mode"}>Cách soạn nội dung</Label>
            <Select
              value={String(value.bilingualContentMode)}
              disabled={disabled}
              onValueChange={(mode) => changeMode(Number(mode))}
            >
              <SelectTrigger id={id + "-content-mode"} className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent showSearch={false}>
                <SelectItem value="1">
                  Tách riêng tiếng Anh / tiếng Việt
                </SelectItem>
                <SelectItem value="2">Gộp nội dung song ngữ</SelectItem>
              </SelectContent>
            </Select>
            {value.bilingualContentMode === 2 && (
              <p className="text-sm text-muted-foreground">
                Soạn tiếng Anh trước, tiếng Việt sau trong từng đoạn hoặc ô
                bảng. Nội dung này được kết xuất một lần.
              </p>
            )}
          </div>
        </>
      )}
      {isBilingual ? (
        <>
          <TermLanguageSection
            language="en"
            className={value.bilingualContentMode === 2 ? "" : "sm:col-span-2"}
          >
            {englishTitleField}
            {value.bilingualContentMode === 1 && englishContentField}
          </TermLanguageSection>
          <TermLanguageSection
            language="vi"
            className={value.bilingualContentMode === 2 ? "" : "sm:col-span-2"}
          >
            {vietnameseTitleField}
            {value.bilingualContentMode === 1 && mainContentField}
          </TermLanguageSection>
          {value.bilingualContentMode === 2 && (
            <TermLanguageSection language="combined" className="sm:col-span-2">
              {mainContentField}
            </TermLanguageSection>
          )}
        </>
      ) : (
        <div className="min-w-0 sm:col-span-2">{mainContentField}</div>
      )}
      <ConfirmDialog
        isOpen={pendingMode != null}
        onClose={() => setPendingMode(null)}
        onConfirm={confirmModeChange}
        title="Đổi cách soạn nội dung"
        confirmText="Áp dụng"
        description={
          <div className="space-y-3">
            <p>
              Không tự ghép hoặc dịch hai nội dung. Chọn nội dung khởi đầu; bạn
              có thể hủy để giữ nguyên hoặc khôi phục bản nháp trước khi lưu.
            </p>
            <Select value={contentSource} onValueChange={setContentSource}>
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent showSearch={false}>
                <SelectItem value="english">
                  {pendingMode === 2
                    ? "Lấy nội dung tiếng Anh làm bản gộp"
                    : "Đưa bản gộp vào ô tiếng Anh"}
                </SelectItem>
                <SelectItem value="vietnamese">
                  {pendingMode === 2
                    ? "Lấy nội dung tiếng Việt làm bản gộp"
                    : "Đưa bản gộp vào ô tiếng Việt"}
                </SelectItem>
                <SelectItem value="new">Soạn mới</SelectItem>
                {pendingMode != null && previousDrafts.current[pendingMode] && (
                  <SelectItem value="restore">
                    Khôi phục nội dung trước khi đổi chế độ
                  </SelectItem>
                )}
              </SelectContent>
            </Select>
          </div>
        }
      />
    </div>
  );
}
