"use client";

import { useState, type ReactNode } from "react";
import { Loader2, Trash2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { toast } from "@/components/ui/sonner";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  contractTemplateApi,
  TemplateVersionStatus,
  type ContractTemplateDetailResponse,
} from "@/services/contract-template-api";

export function DeleteContractTemplateButton({
  templateId,
  onDeleted,
  renderTrigger,
}: {
  templateId: number;
  onDeleted: () => void | Promise<void>;
  renderTrigger?: (props: {
    onClick: () => void;
    disabled: boolean;
    isLoading: boolean;
  }) => ReactNode;
}) {
  const [snapshot, setSnapshot] = useState<ContractTemplateDetailResponse | null>(null);
  const [busy, setBusy] = useState(false);

  const requestDelete = async () => {
    if (busy) return;
    setBusy(true);
    try {
      const detail = await contractTemplateApi.getById(templateId);
      if (detail.currentPublishedVersionId != null || detail.versions.some(
        (version) => version.status !== TemplateVersionStatus.Draft,
      )) {
        toast.error("Không thể xóa mẫu đã từng phát hành. Chỉ mẫu chưa phát hành, chưa được sử dụng mới được xóa.");
        return;
      }
      setSnapshot(detail);
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể kiểm tra mẫu trước khi xóa."));
    } finally {
      setBusy(false);
    }
  };

  const confirmDelete = async () => {
    if (!snapshot || busy) return;
    setBusy(true);
    try {
      await contractTemplateApi.delete(templateId, {
        rowVersion: snapshot.rowVersion,
        versions: snapshot.versions.map((version) => ({
          templateVersionId: version.templateVersionId,
          rowVersion: version.rowVersion,
        })),
      });
      setSnapshot(null);
      toast.success("Đã xóa mẫu tài liệu.");
    } catch (error) {
      setSnapshot(null);
      toast.error(getApiErrorMessage(error, "Không thể xóa mẫu tài liệu."));
      setBusy(false);
      return;
    }
    try {
      await onDeleted();
    } catch {
      toast.error("Đã xóa mẫu nhưng chưa tải lại được danh sách. Vui lòng làm mới.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      {renderTrigger ? renderTrigger({
        onClick: () => void requestDelete(),
        disabled: busy,
        isLoading: busy,
      }) : (
        <Button size="sm" variant="destructive" disabled={busy} onClick={() => void requestDelete()}>
          {busy ? <Loader2 className="size-4 animate-spin" /> : <Trash2 className="size-4" />}
          Xóa mẫu
        </Button>
      )}
      <ConfirmDialog
        isOpen={snapshot !== null}
        onClose={() => setSnapshot(null)}
        onConfirm={() => void confirmDelete()}
        title="Xóa mẫu tài liệu?"
        description={
          <div className="space-y-2">
            <p>Bạn có chắc muốn xóa mẫu <strong>{snapshot?.templateName}</strong> ({snapshot?.templateCode})?</p>
            <p>Toàn bộ bản nháp, nội dung và tệp của mẫu sẽ bị xóa vĩnh viễn, không thể khôi phục. Lịch sử thao tác vẫn được giữ lại.</p>
            <p>Mẫu đã phát hành hoặc được hợp đồng/biên bản sử dụng sẽ không được xóa.</p>
          </div>
        }
        icon={<Trash2 className="size-5 text-destructive" />}
        confirmText="Xóa vĩnh viễn"
        variant="destructive"
        isLoading={busy}
      />
    </>
  );
}
