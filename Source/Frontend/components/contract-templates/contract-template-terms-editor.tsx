"use client";

import { useEffect, useMemo, useState } from "react";
import { Loader2, Plus, Save } from "lucide-react";
import { toast } from "@/components/ui/sonner";

import {
  ContractTermCard,
  type ContractTermEditableField,
} from "@/components/contracts/contract-term-card";
import { Button } from "@/components/ui/button";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { CodeInput } from "@/components/ui/custom/code-input";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { ContractRichTextEditor } from "@/components/ui/custom/contract-rich-text-editor";
import {
  ContractTemplatePaymentMilestonesEditor,
  paymentMilestoneDraftFromRow,
  type PaymentMilestoneDraft,
} from "./contract-template-payment-milestones-editor";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  contractTemplateApi,
  ContractTermKind,
  TemplateVersionStatus,
  type ContractTemplateTermResponse,
  type ContractTemplateVersionDetailResponse,
  type ContractTemplatePaymentMilestoneResponse,
} from "@/services/contract-template-api";
import { getContractTemplateErrorMessage } from "./contract-template-utils";

interface ContractTemplateTermsEditorProps {
  version: ContractTemplateVersionDetailResponse;
  isBilingual: boolean;
  onRefresh: () => Promise<void>;
  mode?: "contract" | "acceptance";
  openTermIds: ReadonlySet<number>;
  onTermOpenChange: (termId: number, open: boolean) => void;
}

const reorderTerms = (terms: ContractTemplateTermResponse[]) =>
  terms.map((term, index) => ({ ...term, displayOrder: index + 1 }));

const getFirstAvailableDisplayOrder = (
  terms: ContractTemplateTermResponse[],
) => {
  const usedOrders = new Set(terms.map((term) => term.displayOrder));
  let displayOrder = 1;
  while (usedOrders.has(displayOrder)) displayOrder += 1;
  return displayOrder;
};

const normalizedPaymentDraft = (draft: PaymentMilestoneDraft) => ({
  ...draft,
  milestoneCode: draft.milestoneCode.trim().toUpperCase(),
  titleVi: draft.titleVi.trim(),
  titleEn: draft.titleEn?.trim() || "",
  conditionVi: draft.conditionVi?.trim() || "",
  conditionEn: draft.conditionEn?.trim() || "",
});

const paymentDraftMatches = (
  draft: PaymentMilestoneDraft,
  row: ContractTemplatePaymentMilestoneResponse,
) =>
  JSON.stringify(normalizedPaymentDraft(draft)) ===
  JSON.stringify(normalizedPaymentDraft(paymentMilestoneDraftFromRow(row)));

export function ContractTemplateTermsEditor({
  version,
  isBilingual,
  onRefresh,
  mode = "contract",
  openTermIds,
  onTermOpenChange,
}: ContractTemplateTermsEditorProps) {
  const isDraft = version.status === TemplateVersionStatus.Draft;
  const isAcceptance = mode === "acceptance";
  const [terms, setTerms] = useState<ContractTemplateTermResponse[]>(() =>
    [...version.terms].sort((a, b) => a.displayOrder - b.displayOrder),
  );
  const [paymentDrafts, setPaymentDrafts] = useState<
    Record<number, PaymentMilestoneDraft>
  >(() =>
    Object.fromEntries(
      version.terms.flatMap((term) =>
        term.paymentMilestones.map((row) => [
          row.templatePaymentMilestoneId,
          paymentMilestoneDraftFromRow(row),
        ]),
      ),
    ),
  );
  const [dirtyIds, setDirtyIds] = useState<Set<number>>(new Set());
  const [isOrderDirty, setIsOrderDirty] = useState(false);
  const [orderDirtyIds, setOrderDirtyIds] = useState<Set<number>>(new Set());
  const [isSavingChanges, setIsSavingChanges] = useState(false);
  const [isAddingPayment, setIsAddingPayment] = useState(false);
  const [deleteTerm, setDeleteTerm] =
    useState<ContractTemplateTermResponse | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);
  const [isAddOpen, setIsAddOpen] = useState(false);
  const [isAdding, setIsAdding] = useState(false);
  const [newTermCode, setNewTermCode] = useState("");
  const [newTermTitle, setNewTermTitle] = useState("");
  const [newTermTitleEn, setNewTermTitleEn] = useState("");
  const [newTermContent, setNewTermContent] = useState("");
  const [newTermContentEn, setNewTermContentEn] = useState("");
  const [newTermKind, setNewTermKind] = useState(ContractTermKind.General);

  const hasUnsavedContent = dirtyIds.size > 0;
  const existingCodes = useMemo(
    () => new Set(terms.map((term) => term.termCode.toLocaleUpperCase())),
    [terms],
  );
  const paymentTerm = terms.find(
    (term) => term.termKind === ContractTermKind.Payment,
  );
  const pendingPaymentRows = (paymentTerm?.paymentMilestones ?? []).filter(
    (row) =>
      !paymentDraftMatches(
        paymentDrafts[row.templatePaymentMilestoneId] ??
          paymentMilestoneDraftFromRow(row),
        row,
      ),
  );
  const hasUnsavedPayments = pendingPaymentRows.length > 0;
  const hasUnsavedChanges =
    hasUnsavedContent || isOrderDirty || hasUnsavedPayments;

  const updateTerm = (
    termId: number,
    field: ContractTermEditableField,
    value: string | boolean,
  ) => {
    if (!isDraft) return;
    if (isAddingPayment) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form thêm đợt trước khi sửa điều khoản.",
      );
      return;
    }
    if (isOrderDirty) {
      toast.error("Vui lòng lưu thứ tự trước khi sửa nội dung điều khoản.");
      return;
    }
    setTerms((current) =>
      current.map((term) =>
        term.templateTermId === termId ? { ...term, [field]: value } : term,
      ),
    );
    setDirtyIds((current) => new Set(current).add(termId));
  };

  const moveTerm = (index: number, direction: -1 | 1) => {
    if (!isDraft) return;
    if (isAddingPayment) {
      toast.error("Hãy hoàn tất hoặc hủy form thêm đợt trước khi đổi thứ tự.");
      return;
    }
    if (hasUnsavedContent) {
      toast.error(
        "Vui lòng lưu nội dung điều khoản trước khi thay đổi thứ tự.",
      );
      return;
    }
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= terms.length) return;
    const next = [...terms];
    const movedIds = [
      next[index].templateTermId,
      next[targetIndex].templateTermId,
    ];
    [next[index], next[targetIndex]] = [next[targetIndex], next[index]];
    setTerms(reorderTerms(next));
    setIsOrderDirty(true);
    setOrderDirtyIds((current) => new Set([...current, ...movedIds]));
  };

  const saveDirtyTerms = async () => {
    if (!isDraft) return;
    const pendingTerms = terms.filter((term) =>
      dirtyIds.has(term.templateTermId),
    );
    const invalidTerm = pendingTerms.find(
      (term) => !term.termCode.trim() || !term.termTitle.trim(),
    );
    if (invalidTerm) {
      throw new Error(
        `Mã và tiêu đề điều khoản ${invalidTerm.termCode || "đang chỉnh sửa"} không được để trống.`,
      );
    }

    let latestVersion = await contractTemplateApi.getVersion(
      version.templateVersionId,
    );

    for (const term of pendingTerms) {
      const updatedTerm = await contractTemplateApi.updateTerm(
        version.templateVersionId,
        term.templateTermId,
        {
          termCode: term.termCode.trim(),
          termKind: isAcceptance
            ? ContractTermKind.General
            : (term.termKind ?? ContractTermKind.General),
          termTitle: term.termTitle.trim(),
          termTitleEn: term.termTitleEn?.trim() || null,
          termContent: term.termContent?.trim() || null,
          termContentEn: term.termContentEn?.trim() || null,
          isNegotiable: isAcceptance ? false : term.isNegotiable,
          displayOrder: term.displayOrder,
          rowVersion: term.rowVersion,
          versionRowVersion: latestVersion.rowVersion,
        },
      );

      setTerms((current) =>
        current.map((item) =>
          item.templateTermId === updatedTerm.templateTermId
            ? updatedTerm
            : item,
        ),
      );
      setDirtyIds((current) => {
        const next = new Set(current);
        next.delete(updatedTerm.templateTermId);
        return next;
      });

      latestVersion = await contractTemplateApi.getVersion(
        version.templateVersionId,
      );
    }
  };

  const saveOrder = async () => {
    if (!isDraft) return;
    if (hasUnsavedContent) {
      throw new Error(
        "Vui lòng lưu nội dung các điều khoản trước khi lưu thứ tự.",
      );
    }
    await contractTemplateApi.reorderTerms(version.templateVersionId, {
      versionRowVersion: (
        await contractTemplateApi.getVersion(version.templateVersionId)
      ).rowVersion,
      terms: terms.map((term) => ({
        termId: term.templateTermId,
        rowVersion: term.rowVersion,
        displayOrder: term.displayOrder,
      })),
    });
  };

  const savePendingPayments = async () => {
    if (!paymentTerm) return;
    for (const originalRow of pendingPaymentRows) {
      const draft = paymentDrafts[originalRow.templatePaymentMilestoneId];
      const latestVersion = await contractTemplateApi.getVersion(
        version.templateVersionId,
      );
      const latestRow = latestVersion.terms
        .find((term) => term.templateTermId === paymentTerm.templateTermId)
        ?.paymentMilestones.find(
          (row) =>
            row.templatePaymentMilestoneId ===
            originalRow.templatePaymentMilestoneId,
        );
      if (!latestRow) {
        throw new Error("Đợt thanh toán đã thay đổi. Vui lòng tải lại trang.");
      }
      if (paymentDraftMatches(draft, latestRow)) continue;
      if (latestRow.rowVersion !== originalRow.rowVersion) {
        throw new Error("Đợt thanh toán đã thay đổi. Vui lòng tải lại trang.");
      }

      await contractTemplateApi.updatePaymentMilestone(
        version.templateVersionId,
        paymentTerm.templateTermId,
        originalRow.templatePaymentMilestoneId,
        {
          ...draft,
          milestoneCode: draft.milestoneCode.trim(),
          titleVi: draft.titleVi.trim(),
          titleEn: draft.titleEn?.trim() || null,
          conditionVi: draft.conditionVi?.trim() || null,
          conditionEn: draft.conditionEn?.trim() || null,
          rowVersion: latestRow.rowVersion,
          versionRowVersion: latestVersion.rowVersion,
        },
      );
    }
  };

  const saveChanges = async () => {
    if (!isDraft || isSavingChanges || !hasUnsavedChanges) return;
    if (isAddingPayment) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form thêm đợt trước khi lưu thay đổi chung.",
      );
      return;
    }
    try {
      setIsSavingChanges(true);
      const invalidTerm = terms.find(
        (term) =>
          dirtyIds.has(term.templateTermId) &&
          (!term.termCode.trim() || !term.termTitle.trim()),
      );
      if (invalidTerm) {
        throw new Error(
          `Mã và tiêu đề điều khoản ${invalidTerm.termCode || "đang chỉnh sửa"} không được để trống.`,
        );
      }
      if (hasUnsavedPayments) {
        await savePendingPayments();
      }
      if (isOrderDirty) {
        await saveOrder();
      } else if (hasUnsavedContent) {
        await saveDirtyTerms();
      }
      await onRefresh();
      toast.success(
        hasUnsavedPayments && (hasUnsavedContent || isOrderDirty)
          ? "Đã lưu điều khoản và các đợt thanh toán."
          : hasUnsavedPayments
            ? "Đã lưu các đợt thanh toán."
            : `Đã lưu thay đổi ${isAcceptance ? "mục nghiệm thu" : "điều khoản"}.`,
      );
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
    } finally {
      setIsSavingChanges(false);
    }
  };

  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (
        !(event.ctrlKey || event.metaKey) ||
        event.altKey ||
        event.shiftKey ||
        event.key.toLowerCase() !== "s"
      ) {
        return;
      }

      event.preventDefault();
      if (
        event.repeat ||
        !isDraft ||
        !hasUnsavedChanges ||
        isSavingChanges ||
        isAddOpen ||
        isAddingPayment ||
        deleteTerm ||
        document.querySelector(
          '[role="dialog"][data-state="open"], [role="alertdialog"][data-state="open"], [role="listbox"][data-state="open"]',
        ) ||
        (event.target instanceof Element &&
          event.target.closest("[data-template-payment-editor]"))
      ) {
        return;
      }

      void saveChanges();
    };

    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  });

  const openAddDialog = () => {
    if (!isDraft) return;
    if (isAddingPayment) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form thêm đợt trước khi thêm điều khoản.",
      );
      return;
    }
    if (hasUnsavedChanges) {
      toast.error(
        `Vui lòng lưu các thay đổi ${isAcceptance ? "section" : "điều khoản"} trước khi thêm mới.`,
      );
      return;
    }
    let index = 1;
    const prefix = isAcceptance ? "SECTION" : "TERM";
    while (existingCodes.has(`${prefix}_${index}`)) index += 1;
    setNewTermCode(`${prefix}_${index}`);
    setNewTermTitle("");
    setNewTermTitleEn("");
    setNewTermContent("");
    setNewTermContentEn("");
    setNewTermKind(ContractTermKind.General);
    setIsAddOpen(true);
  };

  const addTerm = async () => {
    if (!isDraft) return;
    if (newTermKind === ContractTermKind.Payment && paymentTerm) {
      toast.error("Mỗi mẫu hợp đồng chỉ có một điều khoản thanh toán.");
      return;
    }
    if (!newTermCode.trim() || !newTermTitle.trim()) {
      toast.error("Vui lòng nhập mã và tiêu đề điều khoản.");
      return;
    }
    if (existingCodes.has(newTermCode.trim().toLocaleUpperCase())) {
      toast.error("Mã điều khoản đã tồn tại trong version này.");
      return;
    }
    try {
      setIsAdding(true);
      await contractTemplateApi.addTerm(version.templateVersionId, {
        termKind: isAcceptance ? ContractTermKind.General : newTermKind,
        termCode: newTermCode.trim(),
        termTitle: newTermTitle.trim(),
        termTitleEn: newTermTitleEn.trim() || null,
        termContent: newTermContent.trim() || null,
        termContentEn: newTermContentEn.trim() || null,
        isNegotiable: !isAcceptance,
        displayOrder: getFirstAvailableDisplayOrder(terms),
        versionRowVersion: version.rowVersion,
      });
      toast.success(`Đã thêm ${isAcceptance ? "section" : "điều khoản"}.`);
      setIsAddOpen(false);
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setIsAdding(false);
    }
  };

  const confirmDelete = async () => {
    if (!isDraft) return;
    if (!deleteTerm) return;
    try {
      setIsDeleting(true);
      await contractTemplateApi.deleteTerm(
        version.templateVersionId,
        deleteTerm.templateTermId,
        {
          rowVersion: deleteTerm.rowVersion,
          versionRowVersion: version.rowVersion,
        },
      );
      toast.success("Đã xóa điều khoản.");
      setDeleteTerm(null);
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setIsDeleting(false);
    }
  };

  const requestDeleteTerm = (term: ContractTemplateTermResponse) => {
    if (!isDraft) return;
    if (isAddingPayment) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form thêm đợt trước khi xóa điều khoản.",
      );
      return;
    }
    if (hasUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi điều khoản trước khi xóa.");
      return;
    }
    setDeleteTerm(term);
  };

  return (
    <div className="space-y-4">
      <div className="flex flex-col justify-between gap-3 rounded-xl border bg-muted/20 p-4 sm:flex-row sm:items-center">
        <div>
          <p className="font-semibold">
            {isAcceptance ? "Nội dung nghiệm thu" : "Điều khoản mềm"} (
            {terms.length})
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            {isDraft
              ? "Các card có viền đỏ là những thay đổi chưa được lưu."
              : "Version đã khóa và chỉ có thể xem."}
          </p>
        </div>
        {isDraft && (
          <div className="flex flex-wrap gap-2">
            <Button onClick={openAddDialog}>
              <Plus className="size-4" /> Thêm{" "}
              {isAcceptance ? "section" : "điều khoản"}
            </Button>
          </div>
        )}
      </div>

      {terms.map((term, index) => (
        <ContractTermCard
          key={term.templateTermId}
          term={term}
          inputId={`template-${term.templateTermId}`}
          className={
            dirtyIds.has(term.templateTermId) ||
            orderDirtyIds.has(term.templateTermId)
              ? "border-destructive ring-1 ring-destructive/40"
              : undefined
          }
          editable={isDraft && !isSavingChanges}
          open={openTermIds.has(term.templateTermId)}
          onOpenChange={(open) => onTermOpenChange(term.templateTermId, open)}
          isBilingual={isBilingual}
          orderLabel={isAcceptance ? "Section" : "Điều"}
          titleLabel={isAcceptance ? "Tiêu đề section" : undefined}
          contentLabel={isAcceptance ? "Nội dung section" : undefined}
          showNegotiation={!isAcceptance}
          canMoveUp={index > 0}
          canMoveDown={index < terms.length - 1}
          onChange={(field, value) =>
            updateTerm(term.templateTermId, field, value)
          }
          onKindChange={
            isAcceptance
              ? undefined
              : (value) => {
                  if (!isDraft) return;
                  if (isOrderDirty) {
                    toast.error(
                      "Vui lòng lưu thứ tự trước khi đổi loại điều khoản.",
                    );
                    return;
                  }
                  if (isAddingPayment) {
                    toast.error(
                      "Hãy hoàn tất hoặc hủy form thêm đợt trước khi đổi loại điều khoản.",
                    );
                    return;
                  }
                  if (
                    term.termKind === ContractTermKind.Payment &&
                    value !== ContractTermKind.Payment &&
                    term.paymentMilestones.length > 0
                  ) {
                    toast.error(
                      "Hãy xóa các đợt thanh toán trước khi đổi loại điều khoản.",
                    );
                    return;
                  }
                  if (
                    value === ContractTermKind.Payment &&
                    paymentTerm &&
                    paymentTerm.templateTermId !== term.templateTermId
                  ) {
                    toast.error(
                      "Mỗi mẫu hợp đồng chỉ có một điều khoản thanh toán.",
                    );
                    return;
                  }
                  setTerms((current) =>
                    current.map((item) =>
                      item.templateTermId === term.templateTermId
                        ? { ...item, termKind: value }
                        : item,
                    ),
                  );
                  setDirtyIds((current) =>
                    new Set(current).add(term.templateTermId),
                  );
                }
          }
          onMove={(direction) => moveTerm(index, direction)}
          onRemove={() => requestDeleteTerm(term)}
        >
          {!isAcceptance &&
            term.termKind === ContractTermKind.Payment &&
            !dirtyIds.has(term.templateTermId) && (
              <ContractTemplatePaymentMilestonesEditor
                version={version}
                term={term}
                isBilingual={isBilingual}
                editable={isDraft && !isSavingChanges}
                onRefresh={onRefresh}
                drafts={paymentDrafts}
                onDraftChange={(milestoneId, draft) =>
                  setPaymentDrafts((current) => ({
                    ...current,
                    [milestoneId]: draft,
                  }))
                }
                onSaveAll={saveChanges}
                savingAll={isSavingChanges}
                hasUnsavedChanges={hasUnsavedChanges}
                onAddingChange={setIsAddingPayment}
              />
            )}
          {!isAcceptance &&
            term.termKind === ContractTermKind.Payment &&
            dirtyIds.has(term.templateTermId) && (
              <p className="rounded-md bg-amber-50 p-3 text-sm text-amber-800">
                Lưu loại điều khoản trước khi cấu hình các đợt thanh toán.
              </p>
            )}
        </ContractTermCard>
      ))}

      {terms.length === 0 && (
        <div className="rounded-xl border border-dashed py-12 text-center text-sm text-muted-foreground">
          {isAcceptance
            ? "Template chưa có section. Cần ít nhất một section trước khi phát hành."
            : "Version chưa có điều khoản. Cần ít nhất một điều khoản trước khi dùng để tạo hợp đồng."}
        </div>
      )}

      <Dialog
        open={isAddOpen}
        onOpenChange={(open) => !open && setIsAddOpen(false)}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              Thêm {isAcceptance ? "section nghiệm thu" : "điều khoản"}
            </DialogTitle>
            <DialogDescription>
              {isAcceptance ? "Section" : "Điều khoản"} sẽ được thêm vào cuối
              danh sách hiện tại.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            {!isAcceptance && (
              <div className="space-y-2 sm:col-span-2">
                <Label htmlFor="new-term-kind">Loại điều khoản</Label>
                <Select
                  value={String(newTermKind)}
                  onValueChange={(value) =>
                    setNewTermKind(Number(value) as ContractTermKind)
                  }
                >
                  <SelectTrigger id="new-term-kind" className="h-9 w-full">
                    <SelectValue placeholder="Chọn loại điều khoản" />
                  </SelectTrigger>
                  <SelectContent showSearch={false}>
                    <SelectItem value={String(ContractTermKind.General)}>
                      Điều khoản thường
                    </SelectItem>
                    <SelectItem
                      value={String(ContractTermKind.Payment)}
                      disabled={Boolean(paymentTerm)}
                    >
                      Điều khoản thanh toán
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
            )}
            <div className="space-y-2">
              <Label htmlFor="new-term-code">
                Mã điều khoản <span className="text-destructive">*</span>
              </Label>
              <CodeInput
                id="new-term-code"
                value={newTermCode}
                onValueChange={setNewTermCode}
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="new-term-title">
                Tiêu đề <span className="text-destructive">*</span>
              </Label>
              <Input
                id="new-term-title"
                value={newTermTitle}
                onChange={(event) => setNewTermTitle(event.target.value)}
              />
            </div>
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="new-term-content">Nội dung</Label>
              <ContractRichTextEditor
                id="new-term-content"
                value={newTermContent}
                onChange={setNewTermContent}
              />
            </div>
            {isBilingual && (
              <>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="new-term-title-en">Tiêu đề tiếng Anh</Label>
                  <Input
                    id="new-term-title-en"
                    value={newTermTitleEn}
                    onChange={(event) => setNewTermTitleEn(event.target.value)}
                  />
                </div>
                <div className="space-y-2 sm:col-span-2">
                  <Label htmlFor="new-term-content-en">
                    Nội dung tiếng Anh
                  </Label>
                  <ContractRichTextEditor
                    id="new-term-content-en"
                    value={newTermContentEn}
                    ariaLabel="Nội dung điều khoản tiếng Anh"
                    onChange={setNewTermContentEn}
                  />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsAddOpen(false)}
              disabled={isAdding}
            >
              Hủy
            </Button>
            <Button onClick={addTerm} disabled={isAdding}>
              {isAdding && <Loader2 className="size-4 animate-spin" />} Thêm{" "}
              {isAcceptance ? "section" : "điều khoản"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={Boolean(deleteTerm)}
        onClose={() => setDeleteTerm(null)}
        onConfirm={confirmDelete}
        title="Xóa điều khoản?"
        description={`Điều khoản ${deleteTerm?.termCode ?? ""} sẽ bị xóa khỏi bản nháp. Các điều khoản còn lại không tự đổi thứ tự cho đến khi bạn lưu lại thứ tự.`}
        confirmText="Xóa điều khoản"
        variant="destructive"
        isLoading={isDeleting}
      />

      {isDraft && hasUnsavedChanges && (
        <Alert
          className="fixed bottom-6 right-6 z-50 w-[calc(100%-3rem)] max-w-md animate-in fade-in-0 slide-in-from-bottom-8 border-destructive/50 bg-background shadow-lg duration-300"
          aria-live="polite"
        >
          <AlertTitle>
            Điều khoản hoặc đợt thanh toán có thay đổi chưa được lưu
          </AlertTitle>
          <AlertDescription className="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <span>
              {[
                isOrderDirty ? "Thứ tự điều khoản đã thay đổi" : null,
                dirtyIds.size > 0
                  ? `${dirtyIds.size} điều khoản đang chỉnh sửa`
                  : null,
                hasUnsavedPayments
                  ? `${pendingPaymentRows.length} đợt thanh toán đang chỉnh sửa`
                  : null,
              ]
                .filter(Boolean)
                .join("; ")}
            </span>
            <Button
              onClick={saveChanges}
              disabled={isSavingChanges}
              size="sm"
              className="shrink-0"
            >
              {isSavingChanges ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Save className="size-4" />
              )}
              Lưu thay đổi
              <kbd className="hidden rounded bg-primary-foreground/20 px-1.5 py-0.5 text-[10px] font-mono text-primary-foreground sm:inline-block">
                Ctrl+S
              </kbd>
            </Button>
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
