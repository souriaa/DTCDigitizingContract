"use client";

import { useEffect, useState } from "react";
import { Loader2 } from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Button } from "@/components/ui/button";
import {
  CodeInput,
  isValidCodeValue,
  normalizeCodeValue,
} from "@/components/ui/custom/code-input";
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
import { Textarea } from "@/components/ui/textarea";
import { ContractDirection, ContractLanguageMode } from "@/services/contract-api";
import {
  contractTemplateApi,
  getTemplateDocumentTypeLabel,
  TemplateDocumentType,
  type ContractTemplateDetailResponse,
} from "@/services/contract-template-api";
import { getContractTemplateErrorMessage } from "./contract-template-utils";

interface ContractTemplateFormDialogProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: (template: ContractTemplateDetailResponse) => void;
  template?: ContractTemplateDetailResponse | null;
}

export function ContractTemplateFormDialog({
  isOpen,
  onClose,
  onSuccess,
  template,
}: ContractTemplateFormDialogProps) {
  const isEdit = Boolean(template);
  const [templateCode, setTemplateCode] = useState("");
  const [templateName, setTemplateName] = useState("");
  const [templateNameEn, setTemplateNameEn] = useState("");
  const [documentType, setDocumentType] = useState<TemplateDocumentType>(
    TemplateDocumentType.SoftwareSupplyContract,
  );
  const [languageMode, setLanguageMode] = useState<ContractLanguageMode>(
    ContractLanguageMode.Vietnamese,
  );
  const [contractDirection, setContractDirection] = useState<ContractDirection | null>(null);
  const [description, setDescription] = useState("");
  const [initialChangeNote, setInitialChangeNote] = useState("");
  const [isSaving, setIsSaving] = useState(false);

  useEffect(() => {
    if (!isOpen) return;
    setTemplateCode(template?.templateCode ?? "");
    setTemplateName(template?.templateName ?? "");
    setTemplateNameEn(template?.templateNameEn ?? "");
    setDocumentType(
      template?.documentType ?? TemplateDocumentType.SoftwareSupplyContract,
    );
    setLanguageMode(template?.languageMode ?? ContractLanguageMode.Vietnamese);
    setDescription(template?.description ?? "");
    setInitialChangeNote("");
    setContractDirection(template?.versions[0]?.contractDirection ?? null);
  }, [isOpen, template]);

  const handleSubmit = async () => {
    const normalizedTemplateCode = normalizeCodeValue(templateCode, true);
    if (!templateName.trim()) {
      toast.error("Vui lòng nhập tên mẫu hợp đồng.");
      return;
    }
    if (!isEdit && !normalizedTemplateCode) {
      toast.error("Vui lòng nhập mã mẫu hợp đồng.");
      return;
    }
    if (!isEdit && !isValidCodeValue(normalizedTemplateCode)) {
      toast.error(
        "Mã mẫu chỉ được gồm chữ cái không dấu, chữ số, dấu gạch ngang hoặc gạch dưới.",
      );
      return;
    }
    if (
      languageMode === ContractLanguageMode.Bilingual &&
      !templateNameEn.trim()
    ) {
      toast.error("Mẫu song ngữ cần có tên tiếng Anh.");
      return;
    }

    if (!isEdit && contractDirection === null) {
      toast.error("Vui lòng chọn chiều giao dịch Bán hoặc Mua.");
      return;
    }
    try {
      setIsSaving(true);
      const result = template
        ? await contractTemplateApi.update(template.templateId, {
            templateName: templateName.trim(),
            templateNameEn: templateNameEn.trim() || null,
            description: description.trim() || null,
            rowVersion: template.rowVersion,
          })
        : await contractTemplateApi.create({
            templateCode: normalizedTemplateCode,
            templateName: templateName.trim(),
            templateNameEn: templateNameEn.trim() || null,
            documentType,
            contractDirection: contractDirection!,
            languageMode,
            description: description.trim() || null,
            initialChangeNote: initialChangeNote.trim() || null,
          });

      toast.success(
        isEdit ? "Đã cập nhật mẫu hợp đồng." : "Đã tạo mẫu hợp đồng.",
      );
      onSuccess(result);
      onClose();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {isEdit ? "Chỉnh sửa mẫu hợp đồng" : "Tạo mẫu hợp đồng"}
          </DialogTitle>
          <DialogDescription>
            {isEdit
              ? "Chỉ tên và mô tả có thể thay đổi sau khi tạo."
              : "Hệ thống sẽ đồng thời tạo phiên bản đầu tiên ở trạng thái bản nháp."}
          </DialogDescription>
        </DialogHeader>

        <div className="grid gap-4 py-2 sm:grid-cols-2">
          {!isEdit && (
            <div className="space-y-2 col-span-2">
              <Label>
                Chiều giao dịch <span className="text-red-500">*</span>
              </Label>
              <Select
                value={contractDirection === null ? "" : String(contractDirection)}
                onValueChange={(value) =>
                  setContractDirection(Number(value) as ContractDirection)
                }
              >
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Chọn Bán hoặc Mua" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={String(ContractDirection.Sale)}>Bán</SelectItem>
                  <SelectItem value={String(ContractDirection.Purchase)}>Mua</SelectItem>
                </SelectContent>
              </Select>
            </div>
          )}
          <div className="space-y-2 col-span-2">
            <Label htmlFor="template-code">
              Mã mẫu <span className="text-destructive">*</span>
            </Label>
            <CodeInput
              id="template-code"
              value={templateCode}
              onValueChange={setTemplateCode}
              disabled={isEdit}
              placeholder="VD: SOFTWARE-SUPPLY-VI"
              maxLength={50}
              aria-describedby="template-code-help"
            />
            <p
              id="template-code-help"
              className="text-xs leading-relaxed text-muted-foreground"
            ></p>
          </div>
          <div className="space-y-2">
            <Label>
              Loại tài liệu <span className="text-destructive">*</span>
            </Label>
            <Select
              value={String(documentType)}
              onValueChange={(value) =>
                setDocumentType(Number(value) as TemplateDocumentType)
              }
              disabled={isEdit}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {[
                  TemplateDocumentType.SoftwareSupplyContract,
                  TemplateDocumentType.SoftwareMaintenanceContract,
                  TemplateDocumentType.SoftwareUpkeepContract,
                  TemplateDocumentType.Quotation,
                  TemplateDocumentType.PaymentRequest,
                  TemplateDocumentType.HandoverRecord,
                  TemplateDocumentType.AcceptanceRecord,
                  TemplateDocumentType.LiquidationRecord,
                  TemplateDocumentType.Other,
                ].map((type) => (
                  <SelectItem key={type} value={String(type)}>
                    {getTemplateDocumentTypeLabel(type)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2">
            <Label>
              Ngôn ngữ <span className="text-destructive">*</span>
            </Label>
            <Select
              value={String(languageMode)}
              onValueChange={(value) =>
                setLanguageMode(Number(value) as ContractLanguageMode)
              }
              disabled={isEdit}
            >
              <SelectTrigger className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={String(ContractLanguageMode.Vietnamese)}>
                  Tiếng Việt
                </SelectItem>
                <SelectItem value={String(ContractLanguageMode.Bilingual)}>
                  Song ngữ
                </SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="template-name">
              Tên mẫu <span className="text-destructive">*</span>
            </Label>
            <Input
              id="template-name"
              value={templateName}
              onChange={(event) => setTemplateName(event.target.value)}
              placeholder="Tên mẫu hợp đồng"
            />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="template-name-en">
              Tên tiếng Anh
              {languageMode === ContractLanguageMode.Bilingual && (
                <span className="text-destructive"> *</span>
              )}
            </Label>
            <Input
              id="template-name-en"
              value={templateNameEn}
              onChange={(event) => setTemplateNameEn(event.target.value)}
              placeholder="English template name"
            />
          </div>
          <div className="space-y-2 sm:col-span-2">
            <Label htmlFor="template-description">Mô tả</Label>
            <Textarea
              id="template-description"
              value={description}
              onChange={(event) => setDescription(event.target.value)}
              className="min-h-24"
            />
          </div>
          {!isEdit && (
            <div className="space-y-2 sm:col-span-2">
              <Label htmlFor="initial-change-note">Ghi chú phiên bản 1</Label>
              <Textarea
                id="initial-change-note"
                value={initialChangeNote}
                onChange={(event) => setInitialChangeNote(event.target.value)}
                placeholder="Nội dung khởi tạo bản nháp đầu tiên"
              />
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={isSaving}>
            Hủy
          </Button>
          <Button onClick={handleSubmit} disabled={isSaving}>
            {isSaving && <Loader2 className="size-4 animate-spin" />}
            {isEdit ? "Lưu thay đổi" : "Tạo mẫu"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
