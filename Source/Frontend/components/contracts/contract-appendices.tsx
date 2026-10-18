"use client";

import { useEffect, useMemo, useRef, useState } from "react";
import { ArrowDown, ArrowUp, FileStack, Loader2, Minus, Plus } from "lucide-react";

import { AppendixCard } from "@/components/contract-documents/appendix-card";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { TermFields } from "@/components/contract-documents/term-fields";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { getApiErrorMessage, isStaleRowVersion } from "@/lib/api-error";
import { contractApi, type ContractAppendixResponse, type ContractAppendixTermResponse, type ContractDetailResponse } from "@/services/contract-api";

const isTermDirty = (
  draft: ContractAppendixTermResponse,
  saved: ContractAppendixTermResponse,
) =>
  draft.termCode.trim() !== saved.termCode.trim() ||
  draft.termTitle.trim() !== saved.termTitle.trim() ||
  (draft.termTitleEn?.trim() || "") !== (saved.termTitleEn?.trim() || "") ||
  (draft.termContent?.trim() || "") !== (saved.termContent?.trim() || "") ||
  (draft.termContentEn?.trim() || "") !== (saved.termContentEn?.trim() || "") ||
  draft.bilingualContentMode !== saved.bilingualContentMode;

const nextTermCode = (terms: ContractAppendixTermResponse[]) => {
  const largestNumber = terms.reduce((largest, term) => {
    const match = /^TERM_(\d+)$/i.exec(term.termCode.trim());
    const number = match ? Number(match[1]) : NaN;
    return Number.isSafeInteger(number) ? Math.max(largest, number) : largest;
  }, 0);
  return `TERM_${largestNumber + 1}`;
};

export function ContractAppendices({
  contract,
  canEdit,
  onRefetch,
  drafts,
  onTermDraftChange,
  hasUnsavedChanges,
  isSaving,
  onPendingFormChange,
  onOperationChange,
}: {
  contract: ContractDetailResponse;
  canEdit: boolean;
  onRefetch: () => void | Promise<void>;
  drafts: Record<number, ContractAppendixTermResponse>;
  onTermDraftChange: (term: ContractAppendixTermResponse) => void;
  hasUnsavedChanges: boolean;
  isSaving: boolean;
  onPendingFormChange: (open: boolean) => void;
  onOperationChange: (busy: boolean) => void;
}) {
  const appendices = useMemo(
    () =>
      [...(contract.currentVersion.appendices ?? [])].sort(
        (a, b) => a.displayOrder - b.displayOrder,
      ),
    [contract.currentVersion.appendices],
  );
  const availableOptions =
    contract.currentVersion.availableOptionalAppendices ?? [];
  const [busyKey, setBusyKey] = useState<string | null>(null);
  const operationInFlightRef = useRef(false);
  const [addingTo, setAddingTo] = useState<number | null>(null);
  const [newTerm, setNewTerm] = useState({ code: "", title: "", titleEn: "", content: "", contentEn: "", bilingualContentMode: 1 });
  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newAppendix, setNewAppendix] = useState({
    code: "",
    name: "",
    nameEn: "",
    description: "",
  });

  useEffect(() => {
    onPendingFormChange(addingTo !== null || isCreateOpen);
  }, [addingTo, isCreateOpen, onPendingFormChange]);

  useEffect(() => () => onPendingFormChange(false), [onPendingFormChange]);

  const run = async (key: string, action: () => Promise<unknown>) => {
    if (operationInFlightRef.current) return;
    if (
      (addingTo !== null && key !== `add-${addingTo}`) ||
      (isCreateOpen && key !== "create-appendix")
    ) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form đang mở trước khi cập nhật phụ lục.",
      );
      return;
    }
    if (hasUnsavedChanges || isSaving) {
      toast.error("Vui lòng lưu thay đổi hiện tại trước khi cập nhật phụ lục.");
      return;
    }
    try {
      operationInFlightRef.current = true;
      setBusyKey(key);
      onOperationChange(true);
      await action();
      await Promise.resolve(onRefetch());
    } catch (error) {
      if (isStaleRowVersion(error)) {
        await Promise.resolve(onRefetch());
        toast.error("Dữ liệu đã thay đổi. Danh sách phụ lục đã được tải lại.");
      } else {
        toast.error(getApiErrorMessage(error, "Không thể cập nhật phụ lục."));
      }
    } finally {
      operationInFlightRef.current = false;
      setBusyKey(null);
      onOperationChange(false);
    }
  };

  const addTerm = (appendix: ContractAppendixResponse) => {
    if (!newTerm.code.trim() || !newTerm.title.trim()) {
      toast.error("Nhập mã và tiêu đề điều khoản mới.");
      return;
    }
    if (contract.languageMode === 2 && (!newTerm.titleEn.trim() || newTerm.bilingualContentMode === 1 && newTerm.content.trim() && !newTerm.contentEn.trim())) {
      toast.error("Hợp đồng song ngữ cần tiêu đề điều khoản tiếng Anh.");
      return;
    }
    void run(`add-${appendix.appendixId}`, async () => {
      await contractApi.addAppendixTerm(
        contract.contractId,
        contract.currentVersion.versionId,
        appendix.appendixId,
        {
          termCode: newTerm.code.trim(),
          termTitle: newTerm.title.trim(),
          termTitleEn: newTerm.titleEn.trim() || null,
          termContent: newTerm.content || null,
          termContentEn: newTerm.contentEn || null,
          bilingualContentMode: newTerm.bilingualContentMode,
          displayOrder: appendix.terms.length + 1,
          versionRowVersion: contract.currentVersion.rowVersion,
          appendixRowVersion: appendix.rowVersion,
        },
      );
      setAddingTo(null);
      setNewTerm({ code: "", title: "", titleEn: "", content: "", contentEn: "", bilingualContentMode: 1 });
      toast.success("Đã thêm điều khoản phụ lục.");
    });
  };

  const removeTerm = (
    appendix: ContractAppendixResponse,
    term: ContractAppendixTermResponse,
  ) => {
    if (hasUnsavedChanges || isSaving || addingTo !== null || isCreateOpen) {
      toast.error("Vui lòng lưu thay đổi hiện tại trước khi xóa điều khoản.");
      return;
    }
    showConfirmToast({
      title: "Xóa điều khoản phụ lục?",
      description: `Điều khoản “${term.termTitle}” sẽ bị xóa khỏi phụ lục hiện tại.`,
      confirmLabel: "Xóa điều khoản",
      cancelLabel: "Hủy",
      onConfirm: () =>
        run(`delete-${term.appendixTermId}`, () =>
          contractApi.deleteAppendixTerm(
            contract.contractId,
            contract.currentVersion.versionId,
            appendix.appendixId,
            term.appendixTermId,
            {
              rowVersion: term.rowVersion,
              appendixRowVersion: appendix.rowVersion,
              versionRowVersion: contract.currentVersion.rowVersion,
            },
          ),
        ),
    });
  };

  const moveTerm = (
    appendix: ContractAppendixResponse,
    termIndex: number,
    direction: -1 | 1,
  ) => {
    const target = termIndex + direction;
    if (target < 0 || target >= appendix.terms.length) return;
    const ordered = [...appendix.terms];
    [ordered[termIndex], ordered[target]] = [
      ordered[target],
      ordered[termIndex],
    ];
    void run(`order-${appendix.appendixId}`, () =>
      contractApi.reorderAppendixTerms(
        contract.contractId,
        contract.currentVersion.versionId,
        appendix.appendixId,
        {
          versionRowVersion: contract.currentVersion.rowVersion,
          appendixRowVersion: appendix.rowVersion,
          terms: ordered.map((term, index) => ({
            appendixTermId: term.appendixTermId,
            rowVersion: term.rowVersion,
            displayOrder: index + 1,
          })),
        },
      ),
    );
  };

  const removeAppendix = (appendix: ContractAppendixResponse) => {
    if (hasUnsavedChanges || isSaving || addingTo !== null || isCreateOpen) {
      toast.error("Vui lòng lưu thay đổi hiện tại trước khi rút phụ lục.");
      return;
    }
    const fromTemplate = appendix.sourceTemplateAppendixId != null;
    if (appendix.isRequired) return;
    showConfirmToast({
      title: fromTemplate ? "Rút phụ lục khỏi hợp đồng?" : "Xóa phụ lục?",
      description: fromTemplate
        ? `Phụ lục “${appendix.appendixName}” cùng các điều khoản sẽ bị rút khỏi phiên bản hiện tại. Mẫu phụ lục không thay đổi.`
        : `Phụ lục “${appendix.appendixName}” cùng các điều khoản sẽ bị xóa khỏi phiên bản hiện tại.`,
      confirmLabel: fromTemplate ? "Rút phụ lục" : "Xóa phụ lục",
      cancelLabel: "Hủy",
      onConfirm: () =>
        run(`appendix-${appendix.appendixId}`, () =>
          contractApi.deleteAppendix(
            contract.contractId,
            contract.currentVersion.versionId,
            appendix.appendixId,
            appendix.rowVersion,
            contract.currentVersion.rowVersion,
          ),
        ),
    });
  };

  const moveAppendix = (appendixIndex: number, direction: -1 | 1) => {
    const target = appendixIndex + direction;
    if (target < 0 || target >= appendices.length) return;
    const ordered = [...appendices];
    [ordered[appendixIndex], ordered[target]] = [
      ordered[target],
      ordered[appendixIndex],
    ];
    void run("appendix-order", async () => {
      await contractApi.reorderAppendices(
        contract.contractId,
        contract.currentVersion.versionId,
        {
          versionRowVersion: contract.currentVersion.rowVersion,
          appendices: ordered.map((appendix, index) => ({
            appendixId: appendix.appendixId,
            rowVersion: appendix.rowVersion,
            displayOrder: index + 1,
          })),
        },
      );
      toast.success("Đã cập nhật thứ tự phụ lục.");
    });
  };

  const addAppendix = (templateAppendixId: number) => {
    void run(`option-${templateAppendixId}`, async () => {
      await contractApi.addAppendix(
        contract.contractId,
        contract.currentVersion.versionId,
        templateAppendixId,
        contract.currentVersion.rowVersion,
      );
      toast.success("Đã thêm phụ lục tùy chọn.");
    });
  };

  const openCreateAppendix = () => {
    if (hasUnsavedChanges || isSaving || addingTo !== null) {
      toast.error(
        "Hãy lưu thay đổi hoặc hoàn tất form đang mở trước khi tạo phụ lục.",
      );
      return;
    }
    let index = 1;
    const codes = new Set(
      appendices.map((item) => item.appendixCode.toUpperCase()),
    );
    while (codes.has(`APPENDIX_CUSTOM_${index}`)) index += 1;
    setNewAppendix({
      code: `APPENDIX_CUSTOM_${index}`,
      name: "",
      nameEn: "",
      description: "",
    });
    setIsCreateOpen(true);
  };

  const createAppendix = () => {
    if (!newAppendix.code.trim() || !newAppendix.name.trim()) {
      toast.error("Mã và tên phụ lục là bắt buộc.");
      return;
    }
    void run("create-appendix", async () => {
      await contractApi.createAppendix(
        contract.contractId,
        contract.currentVersion.versionId,
        {
          appendixCode: newAppendix.code.trim(),
          appendixName: newAppendix.name.trim(),
          appendixNameEn: newAppendix.nameEn.trim() || null,
          appendixDescription: newAppendix.description.trim() || null,
          versionRowVersion: contract.currentVersion.rowVersion,
        },
      );
      setIsCreateOpen(false);
      toast.success("Đã tạo phụ lục mới.");
    });
  };

  if (appendices.length === 0 && (!canEdit || availableOptions.length === 0)) {
    return (
      <div className="rounded-xl border border-dashed p-10 text-center text-sm text-muted-foreground">
        <FileStack className="mx-auto mb-3 size-8" />
        Phiên bản này không có phụ lục đính kèm.
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {canEdit && (
        <div className="flex items-center justify-between gap-3 rounded-xl border bg-muted/20 p-4">
          <div>
            <p className="font-medium">
              Phụ lục hợp đồng ({appendices.length})
            </p>
            <p className="text-sm text-muted-foreground">
              Có thể chọn từ mẫu hoặc tạo một phụ lục mới cho phiên bản hiện
              tại.
            </p>
          </div>
          <Button onClick={openCreateAppendix} disabled={busyKey !== null}>
            <Plus className="size-4" /> Tạo phụ lục mới
          </Button>
        </div>
      )}
      {canEdit && availableOptions.length > 0 && (
        <Card>
          <CardHeader>
            <CardTitle className="text-base">
              Phụ lục tùy chọn có thể thêm
            </CardTitle>
          </CardHeader>
          <CardContent className="space-y-2">
            {availableOptions.map((option) => (
              <div
                key={option.templateAppendixId}
                className="flex flex-col gap-3 rounded-xl border p-3 sm:flex-row sm:items-center sm:justify-between"
              >
                <div>
                  <p className="font-medium">
                    {option.appendixCode} — {option.appendixName}
                  </p>
                  {option.appendixDescription && (
                    <p className="mt-1 text-sm text-muted-foreground">
                      {option.appendixDescription}
                    </p>
                  )}
                </div>
                <Button
                  size="sm"
                  variant="outline"
                  disabled={busyKey !== null}
                  onClick={() => addAppendix(option.templateAppendixId)}
                >
                  {busyKey === `option-${option.templateAppendixId}` ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Plus className="size-4" />
                  )}
                  Thêm phụ lục
                </Button>
              </div>
            ))}
          </CardContent>
        </Card>
      )}
      {appendices.map((appendix, appendixIndex) => {
        const hasDirtyTerms = appendix.terms.some((term) => {
          const draft = drafts[term.appendixTermId];
          return draft && isTermDirty(draft, term);
        });
        return (
          <AppendixCard key={appendix.appendixId} index={appendixIndex}
            code={appendix.appendixCode} name={appendix.appendixName}
            nameEn={contract.languageMode === 2 ? appendix.appendixNameEn : undefined}
            description={appendix.appendixDescription} required={appendix.isRequired}
            dirty={hasDirtyTerms} termCount={appendix.terms.length} defaultOpen={appendixIndex === 0}
            className={hasDirtyTerms ? "border-destructive ring-1 ring-destructive/40" : undefined}
            actions={canEdit && <>
              <Button type="button" variant="ghost" size="icon" disabled={busyKey !== null || isSaving || appendixIndex === 0} aria-label="Chuyển phụ lục lên" onClick={() => moveAppendix(appendixIndex, -1)}><ArrowUp className="size-4" /></Button>
              <Button type="button" variant="ghost" size="icon" disabled={busyKey !== null || isSaving || appendixIndex === appendices.length - 1} aria-label="Chuyển phụ lục xuống" onClick={() => moveAppendix(appendixIndex, 1)}><ArrowDown className="size-4" /></Button>
              {!appendix.isRequired && <Button type="button" variant="outline" size="sm" disabled={busyKey !== null || isSaving} onClick={() => removeAppendix(appendix)}><Minus className="size-4" />{appendix.sourceTemplateAppendixId != null ? "Rút phụ lục" : "Xóa phụ lục"}</Button>}
            </>}>
                  <TermsListEditor terms={appendix.terms.map((source) => ({ ...(drafts[source.appendixTermId] ?? source), isNegotiable: false }))}
                    getKey={(term) => term.appendixTermId}
                    getCardProps={(term, index) => ({
                      inputId: `appendix-${appendix.appendixId}-${term.appendixTermId}`,
                      className: isTermDirty(term, appendix.terms.find((source) => source.appendixTermId === term.appendixTermId)!) ? "border-destructive ring-1 ring-destructive/40" : undefined,
                      editable: canEdit && !isSaving && busyKey === null, isBilingual: contract.languageMode === 2, showNegotiation: false,
                      onChange: (field, value) => onTermDraftChange({ ...term, [field]: value }),
                      onValueChange: (patch) => onTermDraftChange({ ...term, ...patch }),
                      onMove: (direction) => moveTerm(appendix, index, direction),
                      onRemove: () => removeTerm(appendix, term),
                    })} />

                  {canEdit && addingTo === appendix.appendixId ? (
                    <div className="space-y-3 rounded-xl border border-dashed p-4">
                      <TermFields id={`contract-appendix-new-${appendix.appendixId}`} value={newTerm}
                        onChange={setNewTerm} isBilingual={contract.languageMode === 2} disabled={busyKey !== null || isSaving} />
                      <div className="flex gap-2 sm:col-span-2">
                        <Button size="sm" onClick={() => addTerm(appendix)}>
                          <Plus className="size-4" /> Thêm
                        </Button>
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => setAddingTo(null)}
                        >
                          Hủy
                        </Button>
                      </div>
                    </div>
                  ) : canEdit ? (
                    <Button
                      variant="outline"
                      onClick={() => {
                        if (hasUnsavedChanges || isSaving) {
                          toast.error(
                            "Hãy lưu thay đổi trước khi thêm điều khoản.",
                          );
                        } else {
                          setNewTerm({
                            code: nextTermCode(appendix.terms),
                            title: "",
                            titleEn: "", content: "", contentEn: "", bilingualContentMode: 1,
                          });
                          setAddingTo(appendix.appendixId);
                        }
                      }}
                    >
                      <Plus className="size-4" /> Thêm điều khoản
                    </Button>
                  ) : null}
          </AppendixCard>
        );
      })}

      <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>Tạo phụ lục mới</DialogTitle>
            <DialogDescription>
              Phụ lục này chỉ thuộc phiên bản hợp đồng hiện tại và không thay
              đổi mẫu.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="contract-appendix-code">
                Mã phụ lục <span className="text-destructive">*</span>
              </Label>
              <Input
                id="contract-appendix-code"
                value={newAppendix.code}
                maxLength={50}
                onChange={(event) =>
                  setNewAppendix((current) => ({
                    ...current,
                    code: event.target.value,
                  }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="contract-appendix-name">
                Tên phụ lục <span className="text-destructive">*</span>
              </Label>
              <Input
                id="contract-appendix-name"
                value={newAppendix.name}
                maxLength={500}
                onChange={(event) =>
                  setNewAppendix((current) => ({
                    ...current,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            {contract.languageMode === 2 && (
              <div className="space-y-2 sm:col-span-2">
                <Label htmlFor="contract-appendix-name-en">Tên tiếng Anh</Label>
                <Input
                  id="contract-appendix-name-en"
                  value={newAppendix.nameEn}
                  maxLength={500}
                  onChange={(event) =>
                    setNewAppendix((current) => ({
                      ...current,
                      nameEn: event.target.value,
                    }))
                  }
                />
              </div>
            )}
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="contract-appendix-description">Mô tả</Label>
              <Textarea
                id="contract-appendix-description"
                value={newAppendix.description}
                maxLength={2000}
                onChange={(event) =>
                  setNewAppendix((current) => ({
                    ...current,
                    description: event.target.value,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIsCreateOpen(false)}>
              Hủy
            </Button>
            <Button onClick={createAppendix} disabled={busyKey !== null}>
              {busyKey === "create-appendix" && (
                <Loader2 className="size-4 animate-spin" />
              )}
              Tạo phụ lục
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
