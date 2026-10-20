"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { getContractDirectionLabel } from "@/services/contract-api";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Copy,
  FileClock,
  Languages,
  Loader2,
  Pencil,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { ContractTemplateFormDialog } from "@/components/contract-templates/contract-template-form-dialog";
import { DeleteContractTemplateButton } from "@/components/contract-templates/delete-contract-template-button";
import {
  TemplateValidationStatusBadge,
  TemplateVersionStatusBadge,
} from "@/components/contract-templates/contract-template-status";
import { getContractTemplateErrorMessage } from "@/components/contract-templates/contract-template-utils";
import {
  PermissionGuard,
  ResourceAccessErrorState,
} from "@/components/auth/permission-guard";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Header } from "@/components/ui/custom/header";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { usePermission } from "@/hooks/use-permission";
import { RBAC_PERMISSIONS } from "@/lib/rbac";
import {
  getResourceAccessErrorKind,
  type ResourceAccessErrorKind,
} from "@/lib/api-error";
import { formatDateTime } from "@/lib/format-date-time";
import { ContractLanguageMode } from "@/services/contract-api";
import {
  contractTemplateApi,
  getTemplateDocumentTypeLabel,
  TemplateVersionStatus,
  type ContractTemplateDetailResponse,
  type ContractTemplateVersionSummaryResponse,
} from "@/services/contract-template-api";

export default function ContractTemplateDetailPage() {
  const params = useParams<{ templateId: string }>();
  const router = useRouter();
  const { can } = usePermission();
  const canManage = can(RBAC_PERMISSIONS.templateManage);
  const templateId = Number(params.templateId);
  const [template, setTemplate] =
    useState<ContractTemplateDetailResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<{
    kind: ResourceAccessErrorKind;
    message: string;
  } | null>(null);
  const [isEditOpen, setIsEditOpen] = useState(false);
  const [copySource, setCopySource] =
    useState<ContractTemplateVersionSummaryResponse | null>(null);
  const [changeNote, setChangeNote] = useState("");
  const [isCopying, setIsCopying] = useState(false);

  const fetchTemplate = useCallback(async () => {
    if (!canManage || !Number.isInteger(templateId) || templateId <= 0) {
      if (canManage) {
        setLoadError({
          kind: "not-found",
          message: "Đường dẫn mẫu hợp đồng không hợp lệ.",
        });
      }
      setIsLoading(false);
      return;
    }
    try {
      setIsLoading(true);
      setLoadError(null);
      setTemplate(await contractTemplateApi.getById(templateId));
    } catch (error) {
      setLoadError({
        kind: getResourceAccessErrorKind(error),
        message: getContractTemplateErrorMessage(error),
      });
      toast.error(getContractTemplateErrorMessage(error));
    } finally {
      setIsLoading(false);
    }
  }, [canManage, templateId]);

  useEffect(() => {
    void fetchTemplate();
  }, [fetchTemplate]);

  const versions = useMemo(
    () =>
      [...(template?.versions ?? [])].sort((a, b) => b.versionNo - a.versionNo),
    [template?.versions],
  );
  const draftVersion = versions.find(
    (version) => version.status === TemplateVersionStatus.Draft,
  );
  const latestRetiredVersionId = versions.find(
    (version) => version.status === TemplateVersionStatus.Retired,
  )?.templateVersionId;
  const currentPublishedVersion = versions.find(
    (version) =>
      version.templateVersionId === template?.currentPublishedVersionId,
  );

  const handleCopy = async () => {
    if (!copySource) return;
    try {
      setIsCopying(true);
      const result = await contractTemplateApi.copyVersion(
        copySource.templateVersionId,
        {
          rowVersion: copySource.rowVersion,
          changeNote: changeNote.trim() || null,
        },
      );
      toast.success(
        `Đã tạo phiên bản ${result.versionNo} ở trạng thái bản nháp.`,
      );
      setCopySource(null);
      setChangeNote("");
      router.push(
        `/admin/contract-templates/${templateId}/versions/${result.templateVersionId}`,
      );
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await fetchTemplate();
    } finally {
      setIsCopying(false);
    }
  };

  return (
    <>
      <Header title="Chi tiết mẫu hợp đồng" />
      <div className="grow overflow-y-auto p-3 sm:p-6 lg:p-10">
        <div className="mx-auto space-y-6">
          <Button asChild variant="ghost" className="-ml-3">
            <Link href="/admin/contract-templates">
              <ArrowLeft className="size-4" /> Danh sách mẫu
            </Link>
          </Button>

          {!canManage ? (
            <PermissionGuard permission={RBAC_PERMISSIONS.templateManage}>
              {null}
            </PermissionGuard>
          ) : isLoading ? (
            <div className="space-y-4">
              <Skeleton className="h-44 w-full" />
              <Skeleton className="h-72 w-full" />
            </div>
          ) : loadError || !template ? (
            <ResourceAccessErrorState
              kind={loadError?.kind ?? "not-found"}
              title={
                loadError?.kind === "error"
                  ? "Không thể tải mẫu hợp đồng"
                  : "Không thể truy cập mẫu hợp đồng"
              }
              description={
                loadError?.message ?? "Mẫu hợp đồng không tồn tại hoặc đã bị xóa."
              }
              backHref="/admin/contract-templates"
              backLabel="Quay lại danh sách mẫu"
              onRetry={() => void fetchTemplate()}
            />
          ) : (
            <>
              <Card>
                <CardHeader className="flex flex-row items-start justify-between gap-4">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <CardTitle className="text-xl">
                        {template.templateName}
                      </CardTitle>
                      <Badge variant="outline" className="font-mono">
                        {template.templateCode}
                      </Badge>
                    </div>
                    {template.templateNameEn && (
                      <p className="mt-2 text-sm text-muted-foreground">
                        {template.templateNameEn}
                      </p>
                    )}
                  </div>
                  <div className="flex flex-wrap justify-end gap-2">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => setIsEditOpen(true)}
                    >
                      <Pencil className="size-4" /> Chỉnh sửa
                    </Button>
                    <DeleteContractTemplateButton
                      templateId={templateId}
                      onDeleted={() => router.replace("/admin/contract-templates")}
                    />
                  </div>
                </CardHeader>
                <CardContent className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
                  <div>
                    <p className="text-xs text-muted-foreground">
                      Loại tài liệu
                    </p>
                    <p className="mt-1 font-medium">
                      {getTemplateDocumentTypeLabel(template.documentType)}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Ngôn ngữ</p>
                    <p className="mt-1 inline-flex items-center gap-1.5 font-medium">
                      <Languages className="size-4" />
                      {template.languageMode === ContractLanguageMode.Bilingual
                        ? "Song ngữ"
                        : "Tiếng Việt"}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">
                      Phiên bản phát hành
                    </p>
                    <p className="mt-1 font-medium">
                      {currentPublishedVersion
                        ? `Phiên bản ${currentPublishedVersion.versionNo}`
                        : "Chưa có"}
                    </p>
                  </div>
                  <div>
                    <p className="text-xs text-muted-foreground">Cập nhật</p>
                    <p className="mt-1 font-medium">
                      {formatDateTime(
                        template.updatedDate || template.createdDate,
                      )}
                    </p>
                  </div>
                  {template.description && (
                    <div className="sm:col-span-2 lg:col-span-4">
                      <p className="text-xs text-muted-foreground">Mô tả</p>
                      <p className="mt-1 whitespace-pre-wrap text-sm">
                        {template.description}
                      </p>
                    </div>
                  )}
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    <FileClock className="size-5 text-primary" /> Lịch sử phiên
                    bản
                  </CardTitle>
                </CardHeader>
                <CardContent className="space-y-3">
                  {versions.map((version) => {
                    const isCurrentPublished =
                      template.currentPublishedVersionId ===
                      version.templateVersionId;
                    const canUseAsDraftSource =
                      (version.status === TemplateVersionStatus.Published &&
                        isCurrentPublished) ||
                      (version.status === TemplateVersionStatus.Retired &&
                        !template.currentPublishedVersionId &&
                        version.templateVersionId === latestRetiredVersionId);
                    return (
                      <div
                        key={version.templateVersionId}
                        className="flex flex-col justify-between gap-4 rounded-xl border p-4 md:flex-row md:items-center"
                      >
                        <div className="min-w-0">
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-semibold">
                              Phiên bản {version.versionNo} · {getContractDirectionLabel(version.contractDirection)}
                            </p>
                            <TemplateVersionStatusBadge
                              status={version.status}
                            />
                            <TemplateValidationStatusBadge
                              status={version.validationStatus}
                            />
                            {isCurrentPublished && <Badge>Hiện hành</Badge>}
                          </div>
                          <p className="mt-2 text-sm text-muted-foreground">
                            {version.changeNote || "Không có ghi chú thay đổi"}
                          </p>
                          <p className="mt-1 text-xs text-muted-foreground">
                            Tạo lúc {formatDateTime(version.createdDate)}
                          </p>
                        </div>
                        <div className="flex shrink-0 flex-wrap gap-2">
                          <Button asChild variant="outline" size="sm">
                            <Link
                              href={`/admin/contract-templates/${templateId}/versions/${version.templateVersionId}`}
                            >
                              Mở khu vực biên soạn
                            </Link>
                          </Button>
                          {canUseAsDraftSource && !draftVersion && (
                            <Button
                              variant="outline"
                              size="sm"
                              onClick={() => {
                                setCopySource(version);
                                setChangeNote("");
                              }}
                            >
                              <Copy className="size-4" /> Tạo bản nháp mới
                            </Button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                  {versions.length === 0 && (
                    <p className="py-10 text-center text-sm text-muted-foreground">
                      Chưa có phiên bản.
                    </p>
                  )}
                </CardContent>
              </Card>
            </>
          )}
        </div>
      </div>

      <ContractTemplateFormDialog
        isOpen={isEditOpen}
        onClose={() => setIsEditOpen(false)}
        onSuccess={(updated) => setTemplate(updated)}
        template={template}
      />

      <Dialog
        open={Boolean(copySource)}
        onOpenChange={(open) => !open && setCopySource(null)}
      >
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Tạo bản nháp mới</DialogTitle>
            <DialogDescription>
              Phiên bản nguồn vẫn giữ nguyên và chỉ ở chế độ đọc. Điều khoản sẽ
              được sao chép; DOCX, việc kiểm tra tính hợp lệ và bản xem trước
              phải thực hiện lại.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={changeNote}
            onChange={(event) => setChangeNote(event.target.value)}
            placeholder="Ghi chú thay đổi cho phiên bản mới"
          />
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setCopySource(null)}
              disabled={isCopying}
            >
              Hủy
            </Button>
            <Button onClick={handleCopy} disabled={isCopying}>
              {isCopying && <Loader2 className="size-4 animate-spin" />} Tạo bản
              nháp
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
