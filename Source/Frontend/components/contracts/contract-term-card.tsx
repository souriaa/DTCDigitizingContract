"use client";

import type { ReactNode } from "react";
import {
  ArrowDown,
  ArrowUp,
  ChevronDown,
  LockKeyhole,
  Pencil,
  Trash2,
  UnlockKeyhole,
} from "lucide-react";

import { TermFields } from "@/components/contract-documents/term-fields";
import { TermLanguageSection } from "@/components/contract-documents/term-language-section";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { toast } from "@/components/ui/sonner";
import { ContractRichTextContent } from "@/components/ui/custom/contract-rich-text-content";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import { cn } from "@/lib/utils";
import { ContractTermKind } from "@/services/contract-template-api";
import { Card } from "../ui/card";

export type ContractTermEditableField =
  | "termTitle"
  | "termTitleEn"
  | "termContent"
  | "termContentEn"
  | "bilingualContentMode"
  | "isNegotiable";

export interface ContractTermCardValue {
  termCode: string;
  termTitle: string;
  termTitleEn?: string | null;
  termContent?: string | null;
  termContentEn?: string | null;
  bilingualContentMode: number;
  isNegotiable: boolean;
  displayOrder: number;
  termKind?: ContractTermKind;
}

export interface ContractTermCardProps {
  term: ContractTermCardValue;
  inputId: string;
  className?: string;
  editable?: boolean;
  actionsEnabled?: boolean;
  notifyMoveSuccess?: boolean;
  onEdit?: () => void;
  isBilingual?: boolean;
  canMoveUp?: boolean;
  canMoveDown?: boolean;
  englishTitlePlaceholder?: string;
  englishContentPlaceholder?: string;
  orderLabel?: string;
  titleLabel?: string;
  contentLabel?: string;
  showNegotiation?: boolean;
  open?: boolean;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  onChange?: (
    field: ContractTermEditableField,
    value: string | boolean | number,
  ) => void;
  onKindChange?: (value: ContractTermKind) => void;
  onValueChange?: (patch: Partial<ContractTermCardValue>) => void;
  onMove?: (direction: -1 | 1) => void | boolean | Promise<void | boolean>;
  onRemove?: () => void;
  children?: ReactNode;
}

export function ContractTermCard({
  term,
  inputId,
  className,
  editable = false,
  actionsEnabled = editable,
  notifyMoveSuccess = true,
  onEdit,
  isBilingual = false,
  canMoveUp = false,
  canMoveDown = false,
  englishTitlePlaceholder = "English term title",
  englishContentPlaceholder = "English term content",
  orderLabel = "Điều",
  titleLabel = "Tiêu đề điều khoản",
  contentLabel = "Nội dung điều khoản",
  showNegotiation = true,
  open,
  defaultOpen = false,
  onOpenChange,
  onChange,
  onKindChange,
  onValueChange,
  onMove,
  onRemove,
  children,
}: ContractTermCardProps) {
  const handleMove = async (direction: -1 | 1) => {
    if (!onMove) return;
    try {
      const result = await onMove(direction);
      if (result === false) {
        toast.error("Đổi thứ tự thất bại");
        return;
      }
      if (notifyMoveSuccess) toast.info("Thứ tự đã được thay đổi");
    } catch (error) {
      toast.error(
        error instanceof Error && error.message
          ? error.message
          : "Đổi thứ tự thất bại",
      );
    }
  };

  return (
    <Collapsible
      open={open}
      defaultOpen={defaultOpen}
      onOpenChange={onOpenChange}
    >
      <Card className={cn("p-4 shadow-xs", className)}>
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0 space-y-2">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">
                {orderLabel} {term.displayOrder}
              </Badge>
              {showNegotiation && (
                <Badge
                  variant={term.isNegotiable ? "secondary" : "destructive"}
                  className={
                    term.isNegotiable
                      ? "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900 dark:bg-emerald-950/30 dark:text-emerald-300"
                      : "border-amber-200 bg-amber-50 text-amber-700 dark:border-amber-900 dark:bg-amber-950/30 dark:text-amber-300"
                  }
                >
                  {term.isNegotiable ? (
                    <UnlockKeyhole className="mr-1 size-3" />
                  ) : (
                    <LockKeyhole className="mr-1 size-3" />
                  )}
                  {term.isNegotiable ? "Có thể đàm phán" : "Điều khoản cố định"}
                </Badge>
              )}
              <span className="font-mono text-xs text-muted-foreground">
                {term.termCode}
              </span>
              {term.termKind === ContractTermKind.Payment && (
                <Badge>Thanh toán theo đợt</Badge>
              )}
            </div>
            {isBilingual ? (
              <div className="space-y-1.5">
                <div className="flex min-w-0 items-start gap-2">
                  <Badge variant="outline" className="border-sky-200 text-sky-700 dark:border-sky-900 dark:text-sky-300">EN</Badge>
                  <p lang={term.termTitleEn ? "en" : "vi"} className="min-w-0 break-words text-sm font-medium">
                    {term.termTitleEn || "Chưa có tiêu đề tiếng Anh"}
                  </p>
                </div>
                <div className="flex min-w-0 items-start gap-2">
                  <Badge variant="outline" className="border-emerald-200 text-emerald-700 dark:border-emerald-900 dark:text-emerald-300">VI</Badge>
                  <p lang="vi" className="min-w-0 break-words text-sm font-medium">{term.termTitle}</p>
                </div>
              </div>
            ) : (
              <p className="truncate text-sm font-medium">{term.termTitle}</p>
            )}
          </div>

          <div className="flex items-center gap-1 self-end sm:self-start">
            {actionsEnabled && onMove && (
              <>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Chuyển điều khoản lên"
                  disabled={!canMoveUp}
                  onClick={() => handleMove(-1)}
                >
                  <ArrowUp className="size-4" />
                </Button>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label="Chuyển điều khoản xuống"
                  disabled={!canMoveDown}
                  onClick={() => handleMove(1)}
                >
                  <ArrowDown className="size-4" />
                </Button>
              </>
            )}
            {actionsEnabled && onEdit && (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Sửa điều khoản"
                onClick={onEdit}
              >
                <Pencil className="size-4" />
              </Button>
            )}
            {actionsEnabled && onRemove && (
              <Button
                type="button"
                variant="ghost"
                size="icon"
                aria-label="Xóa điều khoản"
                className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                onClick={onRemove}
              >
                <Trash2 className="size-4" />
              </Button>
            )}
            <CollapsibleTrigger asChild>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                className="group"
                aria-label={`Thu gọn hoặc mở rộng ${orderLabel.toLocaleLowerCase()} ${term.termTitle}`}
              >
                <ChevronDown className="size-4 transition-transform group-data-[state=open]:rotate-180" />
              </Button>
            </CollapsibleTrigger>
          </div>
        </div>

        <CollapsibleContent className="pt-4">
          {editable ? (
            <div className="grid gap-4">
              <TermFields
                id={`term-${inputId}`}
                showCode={false}
                isBilingual={isBilingual}
                titleLabel={titleLabel}
                contentLabel={contentLabel}
                englishTitlePlaceholder={englishTitlePlaceholder}
                englishContentPlaceholder={englishContentPlaceholder}
                kind={term.termKind}
                onKindChange={onKindChange}
                value={{
                  code: term.termCode,
                  title: term.termTitle,
                  titleEn: term.termTitleEn ?? "",
                  content: term.termContent ?? "",
                  contentEn: term.termContentEn ?? "",
                  bilingualContentMode: term.bilingualContentMode,
                }}
                onChange={(next) => {
                  if (onValueChange) {
                    onValueChange({
                      termTitle: next.title,
                      termTitleEn: next.titleEn,
                      termContent: next.content,
                      termContentEn: next.contentEn,
                      bilingualContentMode: next.bilingualContentMode,
                    });
                    return;
                  }
                  if (next.title !== term.termTitle)
                    onChange?.("termTitle", next.title);
                  if (next.titleEn !== (term.termTitleEn ?? ""))
                    onChange?.("termTitleEn", next.titleEn);
                  if (next.content !== (term.termContent ?? ""))
                    onChange?.("termContent", next.content);
                  if (next.contentEn !== (term.termContentEn ?? ""))
                    onChange?.("termContentEn", next.contentEn);
                  if (next.bilingualContentMode !== term.bilingualContentMode)
                    onChange?.(
                      "bilingualContentMode",
                      next.bilingualContentMode,
                    );
                }}
              />

              {showNegotiation && (
                <Button
                  variant="link"
                  type="button"
                  className="flex w-fit cursor-pointer items-center gap-2 text-sm font-medium text-muted-foreground transition-colors hover:text-foreground"
                  onClick={() => onChange?.("isNegotiable", !term.isNegotiable)}
                >
                  {term.isNegotiable ? (
                    <UnlockKeyhole className="size-4 text-emerald-600" />
                  ) : (
                    <LockKeyhole className="size-4 text-amber-600" />
                  )}
                  {term.isNegotiable
                    ? "Cho phép chỉnh sửa khi đàm phán"
                    : "Khóa nội dung khi đàm phán"}
                </Button>
              )}
            </div>
          ) : (
            <div className="grid min-w-0 gap-4">
              {isBilingual && term.bilingualContentMode !== 2 ? (
                <>
                  <TermLanguageSection language="en">
                    <p className="break-words text-sm font-semibold" lang={term.termTitleEn ? "en" : "vi"}>
                      {term.termTitleEn || "Chưa có tiêu đề tiếng Anh"}
                    </p>
                    <ContractRichTextContent
                      value={term.termContentEn}
                      emptyText="Chưa có nội dung tiếng Anh"
                      className="text-foreground"
                    />
                  </TermLanguageSection>
                  <TermLanguageSection language="vi">
                    <p className="break-words text-sm font-semibold" lang="vi">{term.termTitle}</p>
                    <ContractRichTextContent value={term.termContent} className="text-foreground" />
                  </TermLanguageSection>
                </>
              ) : isBilingual ? (
                <TermLanguageSection language="combined">
                  <div className="space-y-2 border-b pb-3">
                    <div className="flex items-start gap-2">
                      <Badge variant="outline">EN</Badge>
                      <p className="min-w-0 break-words text-sm font-semibold" lang={term.termTitleEn ? "en" : "vi"}>
                        {term.termTitleEn || "Chưa có tiêu đề tiếng Anh"}
                      </p>
                    </div>
                    <div className="flex items-start gap-2">
                      <Badge variant="outline">VI</Badge>
                      <p className="min-w-0 break-words text-sm font-semibold" lang="vi">{term.termTitle}</p>
                    </div>
                  </div>
                  <ContractRichTextContent
                    value={term.termContent}
                    className="text-foreground"
                  />
                </TermLanguageSection>
              ) : (
                <div>
                  <p className="text-sm font-semibold">{term.termTitle}</p>
                  <ContractRichTextContent value={term.termContent} className="mt-2 text-foreground" />
                </div>
              )}
            </div>
          )}

          {children && <div>{children}</div>}
        </CollapsibleContent>
      </Card>
    </Collapsible>
  );
}
