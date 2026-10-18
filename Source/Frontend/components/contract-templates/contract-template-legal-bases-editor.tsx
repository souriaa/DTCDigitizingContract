"use client";

import { useMemo, useState } from "react";
import {
  ArrowDown,
  ArrowUp,
  FileText,
  Loader2,
  Pencil,
  Plus,
  Trash2,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Badge } from "@/components/ui/badge";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { LegalBasisFields } from "@/components/contract-documents/legal-basis-fields";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  contractTemplateApi,
  TemplateVersionStatus,
  type ContractTemplateLegalBasisResponse,
  type ContractTemplateVersionDetailResponse,
} from "@/services/contract-template-api";
import { getContractTemplateErrorMessage } from "./contract-template-utils";
import { Card } from "../ui/card";

interface Props {
  version: ContractTemplateVersionDetailResponse;
  isBilingual: boolean;
  publishError?: string | null;
  onRefresh: () => Promise<void>;
}

export function ContractTemplateLegalBasesEditor({
  version,
  isBilingual,
  publishError,
  onRefresh,
}: Props) {
  const isDraft = version.status === TemplateVersionStatus.Draft;
  const bases = useMemo(
    () =>
      [...(version.legalBases ?? [])].sort(
        (a, b) => a.displayOrder - b.displayOrder,
      ),
    [version.legalBases],
  );
  const [editing, setEditing] =
    useState<ContractTemplateLegalBasisResponse | null>(null);
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [basisCode, setBasisCode] = useState("");
  const [contentVi, setContentVi] = useState("");
  const [contentEn, setContentEn] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [isMoving, setIsMoving] = useState(false);
  const [deleting, setDeleting] =
    useState<ContractTemplateLegalBasisResponse | null>(null);
  const [isDeleting, setIsDeleting] = useState(false);

  const copyLegalBasesMarker = async () => {
    try {
      await navigator.clipboard.writeText("{{CONTRACT_LEGAL_BASES}}");
      toast.success("Đã sao chép {{CONTRACT_LEGAL_BASES}}.");
    } catch {
      toast.error("Không thể sao chép marker.");
    }
  };

  const openCreate = () => {
    let index = 1;
    const codes = new Set(bases.map((item) => item.basisCode.toUpperCase()));
    while (codes.has(`LEGAL_BASIS_${index}`)) index += 1;
    setEditing(null);
    setBasisCode(`LEGAL_BASIS_${index}`);
    setContentVi("");
    setContentEn("");
    setIsDialogOpen(true);
  };

  const openEdit = (basis: ContractTemplateLegalBasisResponse) => {
    setEditing(basis);
    setBasisCode(basis.basisCode);
    setContentVi(basis.contentVi);
    setContentEn(basis.contentEn ?? "");
    setIsDialogOpen(true);
  };

  const firstAvailableOrder = () => {
    const used = new Set(bases.map((item) => item.displayOrder));
    let order = 1;
    while (used.has(order)) order += 1;
    return order;
  };

  const save = async () => {
    if (!basisCode.trim() || !contentVi.trim()) {
      toast.error("Vui lòng nhập mã và nội dung căn cứ tiếng Việt.");
      return;
    }
    try {
      setIsSaving(true);
      if (editing) {
        await contractTemplateApi.updateLegalBasis(
          version.templateVersionId,
          editing.templateLegalBasisId,
          {
            basisCode: basisCode.trim(),
            contentVi,
            contentEn: contentEn || null,
            displayOrder: editing.displayOrder,
            rowVersion: editing.rowVersion,
            versionRowVersion: version.rowVersion,
          },
        );
        toast.success("Đã cập nhật căn cứ hợp đồng.");
      } else {
        await contractTemplateApi.addLegalBasis(version.templateVersionId, {
          basisCode: basisCode.trim(),
          contentVi,
          contentEn: contentEn || null,
          displayOrder: firstAvailableOrder(),
          versionRowVersion: version.rowVersion,
        });
        toast.success("Đã thêm căn cứ hợp đồng.");
      }
      setIsDialogOpen(false);
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setIsSaving(false);
    }
  };

  const move = async (index: number, direction: -1 | 1) => {
    const targetIndex = index + direction;
    if (!isDraft || targetIndex < 0 || targetIndex >= bases.length) return;
    const reordered = [...bases];
    [reordered[index], reordered[targetIndex]] = [
      reordered[targetIndex],
      reordered[index],
    ];
    try {
      setIsMoving(true);
      await contractTemplateApi.reorderLegalBases(version.templateVersionId, {
        versionRowVersion: version.rowVersion,
        legalBases: reordered.map((item, order) => ({
          legalBasisId: item.templateLegalBasisId,
          rowVersion: item.rowVersion,
          displayOrder: order + 1,
        })),
      });
      toast.success("Đã cập nhật thứ tự căn cứ.");
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setIsMoving(false);
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    try {
      setIsDeleting(true);
      await contractTemplateApi.deleteLegalBasis(
        version.templateVersionId,
        deleting.templateLegalBasisId,
        deleting.rowVersion,
        version.rowVersion,
      );
      toast.success("Đã xóa căn cứ hợp đồng.");
      setDeleting(null);
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <div className="space-y-4">
      {bases.length > 0 && (
        <Alert>
          <FileText />
          <AlertTitle>Vị trí chèn căn cứ trong DOCX</AlertTitle>
          <AlertDescription className="space-y-3">
            <p>
              DOCX nguồn phải có đúng một marker{" "}
              <code>{"{{CONTRACT_LEGAL_BASES}}"}</code> tại vị trí cần chèn căn
              cứ hợp đồng. Nếu DOCX đã được upload trước khi thêm căn cứ, hãy
              chèn marker, upload lại tài liệu và tạo preview mới.
            </p>
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={copyLegalBasesMarker}
            >
              Sao chép marker
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {publishError && bases.length > 0 && (
        <Alert variant="destructive">
          <FileText />
          <AlertTitle>Chưa thể tạo preview hoặc phát hành</AlertTitle>
          <AlertDescription>{publishError}</AlertDescription>
        </Alert>
      )}
      <div className="flex flex-col justify-between gap-3 rounded-xl border bg-muted/20 p-4 sm:flex-row sm:items-center">
        <div>
          <p className="font-semibold">Căn cứ hợp đồng ({bases.length})</p>
          <p className="mt-1 text-sm text-muted-foreground">
            Nội dung tại đây được chèn vào {"{{CONTRACT_LEGAL_BASES}}"} và được
            chụp riêng khi tạo hợp đồng.
          </p>
        </div>
        {isDraft && (
          <Button onClick={openCreate}>
            <Plus className="size-4" /> Thêm căn cứ
          </Button>
        )}
      </div>

      {bases.map((basis, index) => (
        <Card key={basis.templateLegalBasisId} className="p-4 shadow-xs">
          <div className="mb-3 flex items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <Badge variant="outline">Căn cứ {index + 1}</Badge>
              <span className="font-mono text-xs text-muted-foreground">
                {basis.basisCode}
              </span>
            </div>
            {isDraft && (
              <div className="flex items-center gap-1">
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={isMoving || index === 0}
                  onClick={() => move(index, -1)}
                  aria-label="Chuyển căn cứ lên"
                >
                  <ArrowUp className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  disabled={isMoving || index === bases.length - 1}
                  onClick={() => move(index, 1)}
                  aria-label="Chuyển căn cứ xuống"
                >
                  <ArrowDown className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => openEdit(basis)}
                  aria-label="Sửa căn cứ"
                >
                  <Pencil className="size-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  className="text-destructive hover:text-destructive"
                  onClick={() => setDeleting(basis)}
                  aria-label="Xóa căn cứ"
                >
                  <Trash2 className="size-4" />
                </Button>
              </div>
            )}
          </div>
          <LegalBasisFields
            id={`template-basis-${basis.templateLegalBasisId}`}
            value={basis}
            isBilingual={isBilingual}
            disabled
            showCode={false}
          />
        </Card>
      ))}

      {bases.length === 0 && (
        <div className="rounded-xl border border-dashed py-12 text-center text-sm text-muted-foreground">
          Chưa có căn cứ hợp đồng. Phần này không bắt buộc.
        </div>
      )}

      <Dialog open={isDialogOpen} onOpenChange={setIsDialogOpen}>
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? "Sửa căn cứ" : "Thêm căn cứ"}</DialogTitle>
            <DialogDescription>
              Mỗi căn cứ là một đoạn độc lập trong block căn cứ của hợp đồng.
            </DialogDescription>
          </DialogHeader>
          <LegalBasisFields id="legal-basis" value={{ basisCode, contentVi, contentEn }}
            isBilingual={isBilingual} disabled={isSaving}
            onChange={(field, next) => {
              if (field === "basisCode") setBasisCode(next);
              else if (field === "contentVi") setContentVi(next);
              else setContentEn(next);
            }} />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setIsDialogOpen(false)}
              disabled={isSaving}
            >
              Hủy
            </Button>
            <Button onClick={save} disabled={isSaving}>
              {isSaving && <Loader2 className="size-4 animate-spin" />}
              {editing ? "Lưu thay đổi" : "Thêm căn cứ"}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={Boolean(deleting)}
        onClose={() => setDeleting(null)}
        onConfirm={confirmDelete}
        title="Xóa căn cứ hợp đồng?"
        description={`Căn cứ ${deleting?.basisCode ?? ""} sẽ bị xóa khỏi bản nháp.`}
        confirmText="Xóa căn cứ"
        variant="destructive"
        isLoading={isDeleting}
      />
    </div>
  );
}
