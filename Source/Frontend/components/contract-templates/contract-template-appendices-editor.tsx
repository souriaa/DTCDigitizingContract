"use client";

import { useMemo, useState } from "react";
import {
  ArrowDown,
  ArrowUp,
  FileStack,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { AppendixCard } from "@/components/contract-documents/appendix-card";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { TermFields } from "@/components/contract-documents/term-fields";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";

import { Button } from "@/components/ui/button";

import { CodeInput } from "@/components/ui/custom/code-input";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";

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
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import {
  contractTemplateApi,
  TemplateVersionStatus,
  type ContractTemplateAppendixResponse,
  type ContractTemplateAppendixTermResponse,
  type ContractTemplateVersionDetailResponse,
} from "@/services/contract-template-api";
import { getContractTemplateErrorMessage } from "./contract-template-utils";

interface Props {
  version: ContractTemplateVersionDetailResponse;
  isBilingual: boolean;
  publishError?: string | null;
  openAppendixIds?: Set<number>;
  onAppendixOpenChange?: (appendixId: number, open: boolean) => void;
  onRefresh: () => Promise<void>;
}

type AppendixForm = {
  code: string;
  name: string;
  nameEn: string;
  description: string;
  isRequired: boolean;
  isSelectedByDefault: boolean;
};

type TermForm = {
  code: string;
  title: string;
  titleEn: string;
  content: string;
  contentEn: string;
  bilingualContentMode: number;
};

const emptyAppendixForm: AppendixForm = {
  code: "",
  name: "",
  nameEn: "",
  description: "",
  isRequired: false,
  isSelectedByDefault: false,
};

const emptyTermForm: TermForm = {
  code: "",
  title: "",
  titleEn: "",
  content: "",
  contentEn: "",
  bilingualContentMode: 1,
};

const nextOrder = (items: Array<{ displayOrder: number }>) =>
  items.reduce((maximum, item) => Math.max(maximum, item.displayOrder), 0) + 1;

export function ContractTemplateAppendicesEditor({
  version,
  isBilingual,
  publishError,
  openAppendixIds,
  onAppendixOpenChange,
  onRefresh,
}: Props) {
  const isDraft = version.status === TemplateVersionStatus.Draft;
  const appendices = useMemo(
    () =>
      [...(version.appendices ?? [])].sort(
        (left, right) => left.displayOrder - right.displayOrder,
      ),
    [version.appendices],
  );

  const [editingAppendix, setEditingAppendix] =
    useState<ContractTemplateAppendixResponse | null>(null);
  const [appendixForm, setAppendixForm] =
    useState<AppendixForm>(emptyAppendixForm);
  const [isAppendixDialogOpen, setIsAppendixDialogOpen] = useState(false);
  const [deletingAppendix, setDeletingAppendix] =
    useState<ContractTemplateAppendixResponse | null>(null);

  const [termAppendix, setTermAppendix] =
    useState<ContractTemplateAppendixResponse | null>(null);
  const [editingTerm, setEditingTerm] =
    useState<ContractTemplateAppendixTermResponse | null>(null);
  const [termForm, setTermForm] = useState<TermForm>(emptyTermForm);
  const [isTermDialogOpen, setIsTermDialogOpen] = useState(false);
  const [deletingTerm, setDeletingTerm] = useState<{
    appendix: ContractTemplateAppendixResponse;
    term: ContractTemplateAppendixTermResponse;
  } | null>(null);

  const [busyAction, setBusyAction] = useState<string | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const copyAppendixMarker = async () => {
    try {
      await navigator.clipboard.writeText("{{CONTRACT_APPENDICES}}");
      toast.success("Đã sao chép {{CONTRACT_APPENDICES}}.");
    } catch {
      toast.error("Không thể sao chép marker.");
    }
  };

  const runAction = async (action: string, operation: () => Promise<void>) => {
    try {
      setBusyAction(action);
      setActionError(null);
      await operation();
    } catch (error) {
      const message = getContractTemplateErrorMessage(error);
      setActionError(message);
      toast.error(message);
      await onRefresh();
    } finally {
      setBusyAction(null);
    }
  };

  const openCreateAppendix = () => {
    let index = 1;
    const codes = new Set(
      appendices.map((appendix) => appendix.appendixCode.toUpperCase()),
    );
    while (codes.has(`APPENDIX_${index}`)) index += 1;
    setEditingAppendix(null);
    setAppendixForm({ ...emptyAppendixForm, code: `APPENDIX_${index}` });
    setIsAppendixDialogOpen(true);
  };

  const openEditAppendix = (appendix: ContractTemplateAppendixResponse) => {
    setEditingAppendix(appendix);
    setAppendixForm({
      code: appendix.appendixCode,
      name: appendix.appendixName,
      nameEn: appendix.appendixNameEn ?? "",
      description: appendix.appendixDescription ?? "",
      isRequired: appendix.isRequired,
      isSelectedByDefault: appendix.isSelectedByDefault,
    });
    setIsAppendixDialogOpen(true);
  };

  const saveAppendix = async () => {
    if (!appendixForm.code.trim() || !appendixForm.name.trim()) {
      toast.error("Vui lòng nhập mã và tên phụ lục.");
      return;
    }
    if (isBilingual && !appendixForm.nameEn.trim()) {
      toast.error("Mẫu song ngữ bắt buộc có tên phụ lục tiếng Anh.");
      return;
    }
    await runAction("save-appendix", async () => {
      const common = {
        appendixCode: appendixForm.code.trim(),
        appendixName: appendixForm.name.trim(),
        appendixNameEn: appendixForm.nameEn.trim() || null,
        appendixDescription: appendixForm.description.trim() || null,
        isRequired: appendixForm.isRequired,
        isSelectedByDefault:
          appendixForm.isRequired || appendixForm.isSelectedByDefault,
        displayOrder: editingAppendix?.displayOrder ?? nextOrder(appendices),
        versionRowVersion: version.rowVersion,
      };
      if (editingAppendix) {
        await contractTemplateApi.updateAppendix(
          version.templateVersionId,
          editingAppendix.templateAppendixId,
          { ...common, rowVersion: editingAppendix.rowVersion },
        );
        toast.success("Đã cập nhật phụ lục.");
      } else {
        await contractTemplateApi.addAppendix(
          version.templateVersionId,
          common,
        );
        toast.success("Đã thêm phụ lục.");
      }
      setIsAppendixDialogOpen(false);
      await onRefresh();
    });
  };

  const moveAppendix = async (index: number, direction: -1 | 1) => {
    const targetIndex = index + direction;
    if (!isDraft || targetIndex < 0 || targetIndex >= appendices.length) return;
    const reordered = [...appendices];
    [reordered[index], reordered[targetIndex]] = [
      reordered[targetIndex],
      reordered[index],
    ];
    await runAction("move-appendix", async () => {
      await contractTemplateApi.reorderAppendices(
        version.templateVersionId,
        version.rowVersion,
        reordered.map((appendix, order) => ({
          templateAppendixId: appendix.templateAppendixId,
          rowVersion: appendix.rowVersion,
          displayOrder: order + 1,
        })),
      );
      toast.success("Đã cập nhật thứ tự phụ lục.");
      await onRefresh();
    });
  };

  const confirmDeleteAppendix = async () => {
    if (!deletingAppendix) return;
    await runAction("delete-appendix", async () => {
      await contractTemplateApi.deleteAppendix(
        version.templateVersionId,
        deletingAppendix.templateAppendixId,
        deletingAppendix.rowVersion,
        version.rowVersion,
      );
      toast.success("Đã xóa phụ lục.");
      setDeletingAppendix(null);
      await onRefresh();
    });
  };

  const openCreateTerm = (appendix: ContractTemplateAppendixResponse) => {
    let index = 1;
    const codes = new Set(
      appendix.terms.map((term) => term.termCode.toUpperCase()),
    );
    while (codes.has(`TERM_${index}`)) index += 1;
    setTermAppendix(appendix);
    setEditingTerm(null);
    setTermForm({ ...emptyTermForm, code: `TERM_${index}` });
    setIsTermDialogOpen(true);
  };

  const openEditTerm = (
    appendix: ContractTemplateAppendixResponse,
    term: ContractTemplateAppendixTermResponse,
  ) => {
    setTermAppendix(appendix);
    setEditingTerm(term);
    setTermForm({
      code: term.termCode,
      title: term.termTitle,
      titleEn: term.termTitleEn ?? "",
      content: term.termContent ?? "",
      contentEn: term.termContentEn ?? "",
      bilingualContentMode: term.bilingualContentMode,
    });
    setIsTermDialogOpen(true);
  };

  const saveTerm = async () => {
    if (!termAppendix || !termForm.code.trim() || !termForm.title.trim()) {
      toast.error("Vui lòng nhập mã và tiêu đề điều khoản.");
      return;
    }
    if (!termForm.content.trim()) {
      toast.error("Vui lòng nhập nội dung điều khoản.");
      return;
    }
    await runAction("save-term", async () => {
      const common = {
        termCode: termForm.code.trim(),
        termTitle: termForm.title.trim(),
        termTitleEn: termForm.titleEn.trim() || null,
        termContent: termForm.content.trim() || null,
        termContentEn: termForm.contentEn.trim() || null,
        bilingualContentMode: termForm.bilingualContentMode,
        displayOrder:
          editingTerm?.displayOrder ?? nextOrder(termAppendix.terms),
        versionRowVersion: version.rowVersion,
        appendixRowVersion: termAppendix.rowVersion,
      };
      if (editingTerm) {
        await contractTemplateApi.updateAppendixTerm(
          version.templateVersionId,
          termAppendix.templateAppendixId,
          editingTerm.templateAppendixTermId,
          { ...common, rowVersion: editingTerm.rowVersion },
        );
        toast.success("Đã cập nhật điều khoản phụ lục.");
      } else {
        await contractTemplateApi.addAppendixTerm(
          version.templateVersionId,
          termAppendix.templateAppendixId,
          common,
        );
        toast.success("Đã thêm điều khoản phụ lục.");
      }
      setIsTermDialogOpen(false);
      await onRefresh();
    });
  };

  const moveTerm = async (
    appendix: ContractTemplateAppendixResponse,
    index: number,
    direction: -1 | 1,
  ) => {
    const terms = [...appendix.terms].sort(
      (left, right) => left.displayOrder - right.displayOrder,
    );
    const targetIndex = index + direction;
    if (!isDraft || targetIndex < 0 || targetIndex >= terms.length) return;
    [terms[index], terms[targetIndex]] = [terms[targetIndex], terms[index]];
    await runAction("move-term", async () => {
      await contractTemplateApi.reorderAppendixTerms(
        version.templateVersionId,
        appendix.templateAppendixId,
        version.rowVersion,
        appendix.rowVersion,
        terms.map((term, order) => ({
          templateAppendixTermId: term.templateAppendixTermId,
          rowVersion: term.rowVersion,
          displayOrder: order + 1,
        })),
      );
      toast.success("Đã cập nhật thứ tự điều khoản phụ lục.");
      await onRefresh();
    });
  };

  const confirmDeleteTerm = async () => {
    if (!deletingTerm) return;
    await runAction("delete-term", async () => {
      await contractTemplateApi.deleteAppendixTerm(
        version.templateVersionId,
        deletingTerm.appendix.templateAppendixId,
        deletingTerm.term.templateAppendixTermId,
        deletingTerm.term.rowVersion,
        deletingTerm.appendix.rowVersion,
        version.rowVersion,
      );
      toast.success("Đã xóa điều khoản phụ lục.");
      setDeletingTerm(null);
      await onRefresh();
    });
  };

  const isBusy = busyAction !== null;

  return (
    <div className="space-y-4">
      {appendices.length > 0 && (
        <Alert>
          <FileStack />
          <AlertTitle>Vị trí gắn phụ lục trong DOCX</AlertTitle>
          <AlertDescription className="space-y-3">
            <p>
              DOCX nguồn phải có đúng một marker{" "}
              <code>{"{{CONTRACT_APPENDICES}}"}</code> sau toàn bộ nội dung hợp
              đồng chính. Nếu DOCX đã được upload trước khi tạo phụ lục, hãy
              chèn marker, upload lại tài liệu và tạo preview mới.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={copyAppendixMarker}
            >
              Sao chép marker
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {publishError && (
        <Alert variant="destructive">
          <FileStack />
          <AlertTitle>Chưa thể phát hành phụ lục</AlertTitle>
          <AlertDescription>{publishError}</AlertDescription>
        </Alert>
      )}
      {actionError && (
        <Alert variant="destructive">
          <AlertTitle>Không thể lưu thay đổi</AlertTitle>
          <AlertDescription>{actionError}</AlertDescription>
        </Alert>
      )}

      <div className="flex flex-col justify-between gap-3 rounded-xl border bg-muted/20 p-4 sm:flex-row sm:items-center">
        <div>
          <p className="font-semibold">
            Phụ lục đính kèm ({appendices.length})
          </p>
          <p className="mt-1 text-sm text-muted-foreground">
            Phụ lục nằm sau hợp đồng chính, không có khối chữ ký riêng. Căn cứ
            chỉ ghi “Căn cứ Hợp đồng số ...”, không dùng ngày ký.
          </p>
        </div>
        {isDraft && (
          <Button onClick={openCreateAppendix} disabled={isBusy}>
            <Plus className="size-4" /> Thêm phụ lục
          </Button>
        )}
      </div>

      {!isDraft && (
        <Alert>
          <AlertTitle>Chế độ chỉ xem</AlertTitle>
          <AlertDescription>
            Version đã phát hành hoặc ngừng sử dụng nên metadata và điều khoản
            phụ lục đã được khóa.
          </AlertDescription>
        </Alert>
      )}

      {appendices.map((appendix, appendixIndex) => {
        const terms = [...appendix.terms].sort(
          (left, right) => left.displayOrder - right.displayOrder,
        );
        return (
          <AppendixCard
            key={appendix.templateAppendixId}
            index={appendixIndex}
            code={appendix.appendixCode}
            name={appendix.appendixName}
            nameEn={isBilingual ? appendix.appendixNameEn : undefined}
            description={appendix.appendixDescription}
            required={appendix.isRequired}
            selectedByDefault={appendix.isSelectedByDefault}
            termCount={terms.length}
            open={openAppendixIds?.has(appendix.templateAppendixId)}
            onOpenChange={
              onAppendixOpenChange
                ? (open) =>
                    onAppendixOpenChange(appendix.templateAppendixId, open)
                : undefined
            }
            defaultOpen={openAppendixIds ? undefined : appendixIndex === 0}
            actions={
              isDraft && (
                <>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    disabled={isBusy || appendixIndex === 0}
                    aria-label="Chuyển phụ lục lên"
                    onClick={() => moveAppendix(appendixIndex, -1)}
                  >
                    <ArrowUp className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    disabled={isBusy || appendixIndex === appendices.length - 1}
                    aria-label="Chuyển phụ lục xuống"
                    onClick={() => moveAppendix(appendixIndex, 1)}
                  >
                    <ArrowDown className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    disabled={isBusy}
                    aria-label="Sửa phụ lục"
                    onClick={() => openEditAppendix(appendix)}
                  >
                    <Pencil className="size-4" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    disabled={isBusy}
                    aria-label="Xóa phụ lục"
                    onClick={() => setDeletingAppendix(appendix)}
                  >
                    <Trash2 className="size-4 text-destructive" />
                  </Button>
                </>
              )
            }
          >
            <div className="flex items-center justify-between gap-3 border-t pt-4">
              <p className="text-sm font-medium">Điều khoản ({terms.length})</p>
              {isDraft && (
                <Button
                  variant="outline"
                  size="sm"
                  disabled={isBusy}
                  onClick={() => openCreateTerm(appendix)}
                >
                  <Plus className="size-4" /> Thêm điều khoản
                </Button>
              )}
            </div>
            <TermsListEditor
              terms={terms.map((term) => ({ ...term, isNegotiable: false }))}
              emptyText="Phụ lục chưa có điều khoản. Cần ít nhất một điều khoản trước khi phát hành."
              getKey={(term) => term.templateAppendixTermId}
              getCardProps={(term, index) => ({
                inputId: `template-appendix-${term.templateAppendixTermId}`,
                isBilingual,
                showNegotiation: false,
                actionsEnabled: isDraft && !isBusy,
                onEdit: () => openEditTerm(appendix, term),
                notifyMoveSuccess: false,
                onMove: (direction) => moveTerm(appendix, index, direction),
                onRemove: () => setDeletingTerm({ appendix, term }),
              })}
            />
          </AppendixCard>
        );
      })}

      {appendices.length === 0 && (
        <div className="rounded-xl border border-dashed py-12 text-center text-sm text-muted-foreground">
          Template chưa cấu hình phụ lục. Phần này là tùy chọn.
        </div>
      )}

      <Dialog
        open={isAppendixDialogOpen}
        onOpenChange={setIsAppendixDialogOpen}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>
              {editingAppendix ? "Sửa phụ lục" : "Thêm phụ lục"}
            </DialogTitle>
            <DialogDescription>
              Cấu hình cách phụ lục được chọn khi tạo hợp đồng.
            </DialogDescription>
          </DialogHeader>
          <div className="grid gap-4 py-2 sm:grid-cols-2">
            <div className="space-y-2">
              <Label htmlFor="appendix-code">
                Mã phụ lục <span className="text-red-500">*</span>
              </Label>
              <CodeInput
                id="appendix-code"
                value={appendixForm.code}
                onValueChange={(code) =>
                  setAppendixForm((form) => ({ ...form, code }))
                }
              />
            </div>
            <div className="space-y-2">
              <Label htmlFor="appendix-name">
                Tên phụ lục <span className="text-red-500">*</span>
              </Label>
              <Input
                id="appendix-name"
                value={appendixForm.name}
                onChange={(event) =>
                  setAppendixForm((form) => ({
                    ...form,
                    name: event.target.value,
                  }))
                }
              />
            </div>
            {isBilingual && (
              <div className="space-y-2 sm:col-span-2">
                <Label htmlFor="appendix-name-en">
                  Tên tiếng Anh <span className="text-destructive">*</span>
                </Label>
                <Input
                  id="appendix-name-en"
                  required
                  maxLength={500}
                  value={appendixForm.nameEn}
                  onChange={(event) =>
                    setAppendixForm((form) => ({
                      ...form,
                      nameEn: event.target.value,
                    }))
                  }
                />
              </div>
            )}
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="appendix-description">Mô tả</Label>
              <Textarea
                id="appendix-description"
                value={appendixForm.description}
                onChange={(event) =>
                  setAppendixForm((form) => ({
                    ...form,
                    description: event.target.value,
                  }))
                }
              />
            </div>
            <div className="flex items-center justify-between gap-4 rounded-xl border p-4">
              <div>
                <Label htmlFor="appendix-required">Bắt buộc</Label>
                <p className="text-xs text-muted-foreground">
                  Luôn được thêm vào hợp đồng.
                </p>
              </div>
              <Switch
                id="appendix-required"
                checked={appendixForm.isRequired}
                onCheckedChange={(checked) =>
                  setAppendixForm((form) => ({
                    ...form,
                    isRequired: checked,
                    isSelectedByDefault: checked || form.isSelectedByDefault,
                  }))
                }
              />
            </div>
            <div className="flex items-center justify-between gap-4 rounded-xl border p-4">
              <div>
                <Label htmlFor="appendix-default">Chọn mặc định</Label>
                <p className="text-xs text-muted-foreground">
                  Áp dụng cho phụ lục tùy chọn.
                </p>
              </div>
              <Switch
                id="appendix-default"
                checked={appendixForm.isSelectedByDefault}
                disabled={appendixForm.isRequired}
                onCheckedChange={(checked) =>
                  setAppendixForm((form) => ({
                    ...form,
                    isSelectedByDefault: checked,
                  }))
                }
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsAppendixDialogOpen(false)}
              disabled={busyAction === "save-appendix"}
            >
              Hủy
            </Button>
            <Button
              onClick={saveAppendix}
              disabled={busyAction === "save-appendix"}
            >
              {busyAction === "save-appendix" && (
                <Loader2 className="size-4 animate-spin" />
              )}
              Lưu phụ lục
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <Dialog open={isTermDialogOpen} onOpenChange={setIsTermDialogOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>
              {editingTerm
                ? "Sửa điều khoản phụ lục"
                : "Thêm điều khoản phụ lục"}
            </DialogTitle>
            <DialogDescription>{termAppendix?.appendixName}</DialogDescription>
          </DialogHeader>
          <TermFields
            id="appendix-term"
            value={termForm}
            onChange={setTermForm}
            isBilingual={isBilingual}
            disabled={busyAction === "save-term"}
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsTermDialogOpen(false)}
              disabled={busyAction === "save-term"}
            >
              Hủy
            </Button>
            <Button onClick={saveTerm} disabled={busyAction === "save-term"}>
              {busyAction === "save-term" && (
                <Loader2 className="size-4 animate-spin" />
              )}
              Lưu điều khoản
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={Boolean(deletingAppendix)}
        onClose={() => setDeletingAppendix(null)}
        onConfirm={confirmDeleteAppendix}
        title="Xóa phụ lục?"
        description={`Phụ lục ${deletingAppendix?.appendixCode ?? ""} và toàn bộ điều khoản bên trong sẽ bị xóa khỏi bản nháp.`}
        confirmText="Xóa phụ lục"
        variant="destructive"
        isLoading={busyAction === "delete-appendix"}
      />
      <ConfirmDialog
        isOpen={Boolean(deletingTerm)}
        onClose={() => setDeletingTerm(null)}
        onConfirm={confirmDeleteTerm}
        title="Xóa điều khoản phụ lục?"
        description={`Điều khoản ${deletingTerm?.term.termCode ?? ""} sẽ bị xóa khỏi bản nháp.`}
        confirmText="Xóa điều khoản"
        variant="destructive"
        isLoading={busyAction === "delete-term"}
      />
    </div>
  );
}
