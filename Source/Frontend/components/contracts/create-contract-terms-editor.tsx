"use client";

import { useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { Plus, ShieldCheck } from "lucide-react";

import { type ContractTermEditableField } from "@/components/contracts/contract-term-card";
import { TermFields } from "@/components/contract-documents/term-fields";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { PaymentPlanEditor } from "@/components/contract-documents/payment-plan-editor";
import type { ContractPaymentMilestoneDraft } from "@/services/contract-api";
import { Button } from "@/components/ui/button";

import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

import { toast } from "@/components/ui/sonner";
import { ContractTermKind } from "@/services/contract-template-api";

export interface CreateContractTermDraft {
  clientId: string;
  paymentMilestones: ContractPaymentMilestoneDraft[];
  sourceTemplateTermId?: number | null;
  termKind: ContractTermKind;
  termCode: string;
  termTitle: string;
  termTitleEn?: string | null;
  termContent?: string | null;
  termContentEn?: string | null;
  bilingualContentMode: number;
  isNegotiable: boolean;
  displayOrder: number;
}

interface CreateContractTermsEditorProps {
  terms: CreateContractTermDraft[];
  templateName?: string;
  isBilingual: boolean;
  onChange: (terms: CreateContractTermDraft[]) => void;
  renderTermChildren?: (term: CreateContractTermDraft) => ReactNode;
  savedRevision?: string;
}

const reorderTerms = (terms: CreateContractTermDraft[]) =>
  terms.map((term, index) => ({ ...term, displayOrder: index + 1 }));

export function CreateContractTermsEditor({
  terms,
  templateName,
  isBilingual,
  onChange,
  renderTermChildren,
  savedRevision,
}: CreateContractTermsEditorProps) {
  const [dirtyIds, setDirtyIds] = useState<Set<string>>(new Set());
  const termsRef = useRef(terms);
  termsRef.current = terms;
  useEffect(() => setDirtyIds(new Set()), [savedRevision]);
  const [deleteTerm, setDeleteTerm] = useState<CreateContractTermDraft | null>(
    null,
  );
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [newTermKind, setNewTermKind] = useState(ContractTermKind.General);
  const [newTermCode, setNewTermCode] = useState("");
  const [newTermTitle, setNewTermTitle] = useState("");
  const [newTermTitleEn, setNewTermTitleEn] = useState("");
  const [newTermContent, setNewTermContent] = useState("");
  const [newTermContentEn, setNewTermContentEn] = useState("");
  const [newContentMode, setNewContentMode] = useState(1);

  const existingCodes = useMemo(
    () => new Set(terms.map((term) => term.termCode.trim().toUpperCase())),
    [terms],
  );

  const updateTerm = (
    clientId: string,
    field: ContractTermEditableField,
    value: string | boolean | number,
  ) => {
    const next = termsRef.current.map((term) =>
      term.clientId === clientId ? { ...term, [field]: value } : term);
    termsRef.current = next;
    onChange(next);
    setDirtyIds((current) => new Set(current).add(clientId));
  };

  const moveTerm = (index: number, direction: -1 | 1) => {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= terms.length) return;

    const next = [...terms];
    const affectedIds = [next[index].clientId, next[targetIndex].clientId];
    [next[index], next[targetIndex]] = [next[targetIndex], next[index]];
    onChange(reorderTerms(next));
    setDirtyIds((current) => new Set([...current, ...affectedIds]));
  };

  const openAddDialog = () => {
    let index = 1;
    while (existingCodes.has(`CUSTOM_${index}`)) index += 1;
    setNewTermKind(ContractTermKind.General);
    setNewTermCode(`CUSTOM_${index}`);
    setNewTermTitle("");
    setNewTermTitleEn("");
    setNewTermContent("");
    setNewTermContentEn("");
    setNewContentMode(1);
    setIsAddOpen(true);
  };

  const addTerm = () => {
    if (
      newTermKind === ContractTermKind.Payment &&
      terms.some((term) => term.termKind === ContractTermKind.Payment)
    ) {
      toast.error("Mỗi hợp đồng chỉ có một điều khoản thanh toán.");
      return;
    }
    const termCode = newTermCode.trim().toUpperCase();
    if (!termCode || !newTermTitle.trim()) {
      toast.error("Vui lòng nhập mã và tiêu đề điều khoản.");
      return;
    }
    if (existingCodes.has(termCode)) {
      toast.error("Mã điều khoản đã tồn tại trong hợp đồng.");
      return;
    }
    if (isBilingual && !newTermTitleEn.trim()) {
      toast.error("Điều khoản song ngữ phải có tiêu đề tiếng Anh.");
      return;
    }
    if (isBilingual && newContentMode === 1 && newTermContent.trim() && !newTermContentEn.trim()) {
      toast.error("Điều khoản song ngữ phải có nội dung tiếng Anh.");
      return;
    }

    const clientId = `custom-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    onChange([
      ...terms,
      {
        clientId,
        sourceTemplateTermId: null,
        termKind: newTermKind,
        paymentMilestones: [],
        termCode,
        termTitle: newTermTitle.trim(),
        termTitleEn: newTermTitleEn.trim() || null,
        termContent: newTermContent.trim() || null,
        termContentEn: newTermContentEn.trim() || null,
        bilingualContentMode: newContentMode,
        isNegotiable: true,
        displayOrder: terms.length + 1,
      },
    ]);
    setDirtyIds((current) => new Set(current).add(clientId));
    setIsAddOpen(false);
  };

  const confirmDelete = () => {
    if (!deleteTerm) return;
    onChange(
      reorderTerms(
        terms.filter((term) => term.clientId !== deleteTerm.clientId),
      ),
    );
    setDirtyIds((current) => {
      const next = new Set(current);
      next.delete(deleteTerm.clientId);
      return next;
    });
    setDeleteTerm(null);
  };

  if (!templateName) {
    return (
      <div className="rounded-2xl border border-dashed bg-muted/20 px-6 py-12 text-center">
        <ShieldCheck className="mx-auto size-9 text-muted-foreground" />
        <p className="mt-3 font-semibold">Chưa có bộ điều khoản</p>
        <p className="mt-1 text-sm text-muted-foreground">
          Quay lại bước đầu tiên và chọn một mẫu hợp đồng.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <TermsListEditor
        terms={terms}
        getKey={(term) => term.clientId}
        title="Điều khoản hợp đồng"
        description={`${templateName}. Các thay đổi sẽ được lưu cùng hợp đồng nháp.`}
        onAdd={openAddDialog}
        getCardProps={(term, index) => ({
          inputId: `create-${term.clientId}`,
          editable: true,
          isBilingual,
          className: dirtyIds.has(term.clientId)
            ? "border-destructive ring-1 ring-destructive/40"
            : undefined,
          onChange: (field, value) => updateTerm(term.clientId, field, value),
          onMove: (direction) => moveTerm(index, direction),
          onRemove: () => setDeleteTerm(term),
          onKindChange: (kind) => {
            if (
              kind === ContractTermKind.Payment &&
              terms.some(
                (other) =>
                  other.clientId !== term.clientId &&
                  other.termKind === ContractTermKind.Payment,
              )
            ) {
              toast.error("Mỗi hợp đồng chỉ có một điều khoản thanh toán.");
              return;
            }
            if (
              kind !== ContractTermKind.Payment &&
              term.paymentMilestones.length
            ) {
              toast.error(
                "Hãy xóa các đợt thanh toán trước khi đổi loại điều khoản.",
              );
              return;
            }
            onChange(
              terms.map((item) =>
                item.clientId === term.clientId
                  ? { ...item, termKind: kind }
                  : item,
              ),
            );
            setDirtyIds((current) => new Set(current).add(term.clientId));
          },
          children: (
            <>
              {term.termKind === ContractTermKind.Payment && (
                <PaymentPlanEditor
                  rows={term.paymentMilestones}
                  isBilingual={isBilingual}
                  onChange={(rows) => {
                    onChange(
                      terms.map((item) =>
                        item.clientId === term.clientId
                          ? { ...item, paymentMilestones: rows }
                          : item,
                      ),
                    );
                    setDirtyIds((current) =>
                      new Set(current).add(term.clientId),
                    );
                  }}
                />
              )}
              {renderTermChildren?.(term)}
            </>
          ),
        })}
      />

      <Dialog open={isAddOpen} onOpenChange={setIsAddOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>Thêm điều khoản hợp đồng</DialogTitle>
            <DialogDescription>
              Điều khoản mới sẽ được thêm vào cuối danh sách và không thay đổi
              mẫu gốc.
            </DialogDescription>
          </DialogHeader>
          <TermFields
            id="create-new-term"
            isBilingual={isBilingual}
            value={{
              code: newTermCode,
              title: newTermTitle,
              titleEn: newTermTitleEn,
              content: newTermContent,
              contentEn: newTermContentEn, bilingualContentMode: newContentMode,
            }}
            onChange={(value) => {
              setNewTermCode(value.code);
              setNewTermTitle(value.title);
              setNewTermTitleEn(value.titleEn);
              setNewTermContent(value.content);
              setNewTermContentEn(value.contentEn); setNewContentMode(value.bilingualContentMode);
            }}
            kind={newTermKind}
            onKindChange={setNewTermKind}
            paymentDisabled={terms.some(
              (term) => term.termKind === ContractTermKind.Payment,
            )}
          />
          <DialogFooter>
            <Button
              type="button"
              variant="outline"
              onClick={() => setIsAddOpen(false)}
            >
              Hủy
            </Button>
            <Button type="button" onClick={addTerm}>
              <Plus className="size-4" /> Thêm điều khoản
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={Boolean(deleteTerm)}
        onClose={() => setDeleteTerm(null)}
        onConfirm={confirmDelete}
        title="Xóa điều khoản?"
        description={`Điều khoản ${deleteTerm?.termCode ?? ""}${deleteTerm?.termKind === ContractTermKind.Payment ? " và toàn bộ các đợt thanh toán của điều khoản" : ""} sẽ bị xóa khỏi bản đang soạn. Mẫu gốc không bị thay đổi.`}
        confirmText="Xóa điều khoản"
        variant="destructive"
      />
    </div>
  );
}
