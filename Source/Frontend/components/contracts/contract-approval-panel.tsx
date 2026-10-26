"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  CheckCircle2,
  Download,
  Eye,
  FileClock,
  FileText,
  Loader2,
  RotateCcw,
  ShieldX,
  Undo2,
} from "lucide-react";

import { downloadBlob } from "@/components/contract-templates/contract-template-utils";
import {
  ContractApprovalDecisionDialog,
  type ApprovalDecision,
} from "@/components/contracts/contract-approval-decision-dialog";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "@/components/ui/sonner";
import { useAuthStore } from "@/hooks/use-auth-store";
import {
  getApiErrorMessage,
  getBlobApiErrorMessage,
  isStaleRowVersion,
} from "@/lib/api-error";
import { createNegotiationRoundAfterApproval } from "@/lib/create-negotiation-round-after-approval";
import { formatDateTime } from "@/lib/format-date-time";
import { hasPermission, RBAC_PERMISSIONS } from "@/lib/rbac";
import { cn } from "@/lib/utils";
import {
  contractApprovalApi,
  type ContractApprovalDetailResponse,
  type ContractApprovalRequestResponse,
} from "@/services/contract-approval-api";
import {
  ApprovalRequestStatus,
  ContractStatus,
  getApprovalRequestStatusLabel,
  type ContractDetailResponse,
} from "@/services/contract-api";

const statusStyles: Record<ApprovalRequestStatus, string> = {
  [ApprovalRequestStatus.PendingAdministrativeReview]:
    "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-300",
  [ApprovalRequestStatus.Pending]:
    "border-amber-500/30 bg-amber-500/10 text-amber-700 dark:text-amber-300",
  [ApprovalRequestStatus.Approved]:
    "border-emerald-500/30 bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  [ApprovalRequestStatus.Returned]:
    "border-orange-500/30 bg-orange-500/10 text-orange-700 dark:text-orange-300",
  [ApprovalRequestStatus.Rejected]:
    "border-rose-500/30 bg-rose-500/10 text-rose-700 dark:text-rose-300",
  [ApprovalRequestStatus.Withdrawn]:
    "border-slate-500/30 bg-slate-500/10 text-slate-700 dark:text-slate-300",
};

const statusIcon = (status: ApprovalRequestStatus) => {
  if (status === ApprovalRequestStatus.Approved) return CheckCircle2;
  if (status === ApprovalRequestStatus.Returned) return RotateCcw;
  if (status === ApprovalRequestStatus.Rejected) return ShieldX;
  if (status === ApprovalRequestStatus.Withdrawn) return Undo2;
  return FileClock;
};

interface ContractApprovalPanelProps {
  contract: ContractDetailResponse;
  canManage: boolean;
  onContractRefetch: () => void | Promise<void>;
}

export function ContractApprovalPanel({
  contract,
  canManage,
  onContractRefetch,
}: ContractApprovalPanelProps) {
  const employeeId = useAuthStore((state) => state.user?.employeeId);
  const permissions = useAuthStore((state) => state.user?.permissions);
  const [history, setHistory] = useState<ContractApprovalRequestResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [withdrawReason, setWithdrawReason] = useState("");
  const [isWithdrawing, setIsWithdrawing] = useState(false);
  const [isCreatingRound, setIsCreatingRound] = useState(false);
  const [artifactDetail, setArtifactDetail] =
    useState<ContractApprovalDetailResponse | null>(null);
  const [isLoadingArtifacts, setIsLoadingArtifacts] = useState(false);
  const [downloadingFileId, setDownloadingFileId] = useState<number | null>(
    null,
  );
  const [decisionRequest, setDecisionRequest] =
    useState<ContractApprovalRequestResponse | null>(null);
  const [decisionDetail, setDecisionDetail] =
    useState<ContractApprovalDetailResponse | null>(null);
  const [isLoadingDecision, setIsLoadingDecision] = useState(false);
  const [isSubmittingDecision, setIsSubmittingDecision] = useState(false);
  const decisionLoadId = useRef(0);

  const loadHistory = useCallback(async () => {
    try {
      setIsLoading(true);
      setError(null);
      setHistory(
        await contractApprovalApi.getContractHistory(contract.contractId),
      );
    } catch (loadError) {
      setError(
        getApiErrorMessage(loadError, "Không thể tải lịch sử phê duyệt."),
      );
    } finally {
      setIsLoading(false);
    }
  }, [contract.contractId]);

  useEffect(() => {
    void loadHistory();
  }, [loadHistory, contract.status]);

  const pendingRequest = history.find(
    (request) => request.status === ApprovalRequestStatus.Pending
      || request.status === ApprovalRequestStatus.PendingAdministrativeReview,
  );
  const canWithdraw =
    canManage &&
    [ContractStatus.PendingApproval, ContractStatus.PendingAdministrativeReview].includes(contract.status) &&
    pendingRequest?.submittedByEmployeeId === employeeId;
  const canDecide =
    hasPermission(permissions, RBAC_PERMISSIONS.contractApprovalDecide) &&
    contract.status === ContractStatus.PendingApproval &&
    pendingRequest?.versionId === contract.currentVersion.versionId &&
    pendingRequest.submittedByEmployeeId !== employeeId;

  const canReview =
    hasPermission(permissions, RBAC_PERMISSIONS.contractApprovalReview) &&
    contract.status === ContractStatus.PendingAdministrativeReview &&
    pendingRequest?.status === ApprovalRequestStatus.PendingAdministrativeReview &&
    pendingRequest.versionId === contract.currentVersion.versionId &&
    pendingRequest.submittedByEmployeeId !== employeeId;

  const openDecision = async (request: ContractApprovalRequestResponse) => {
    const loadId = ++decisionLoadId.current;
    setDecisionRequest(request);
    setDecisionDetail(null);
    setIsLoadingDecision(true);
    try {
      const detail = await contractApprovalApi.getDetail(
        request.approvalRequestId,
      );
      if (decisionLoadId.current === loadId) setDecisionDetail(detail);
    } catch (loadError) {
      if (decisionLoadId.current === loadId) {
        setDecisionRequest(null);
        toast.error(
          getApiErrorMessage(loadError, "Không thể tải yêu cầu duyệt."),
        );
      }
    } finally {
      if (decisionLoadId.current === loadId) setIsLoadingDecision(false);
    }
  };

  const closeDecision = () => {
    decisionLoadId.current++;
    setDecisionRequest(null);
    setDecisionDetail(null);
    setIsLoadingDecision(false);
  };

  const submitDecision = async (
    decision: ApprovalDecision,
    comment: string,
  ) => {
    if (!decisionDetail || isSubmittingDecision) return;
    setIsSubmittingDecision(true);
    try {
      const payload = {
        rowVersion: decisionDetail.rowVersion,
        comment: comment || null,
      };
      if (decisionDetail.status === ApprovalRequestStatus.PendingAdministrativeReview) {
        await (decision === "approve" ? contractApprovalApi.reviewApprove : contractApprovalApi.reviewReturn)(
          decisionDetail.approvalRequestId, payload);
      } else if (decision === "approve") {
        await contractApprovalApi.approve(
          decisionDetail.approvalRequestId,
          payload,
        );
      } else {
        await contractApprovalApi.returnForRevision(
          decisionDetail.approvalRequestId,
          payload,
        );
      }
      toast.success(
        decision === "approve"
          ? decisionDetail.status === ApprovalRequestStatus.PendingAdministrativeReview
            ? "Đã kiểm tra và trình quản lý phê duyệt." : "Đã duyệt hợp đồng."
          : "Đã yêu cầu sửa lại hợp đồng.",
      );
      closeDecision();
      await Promise.allSettled([
        loadHistory(),
        Promise.resolve(onContractRefetch()),
      ]);
    } catch (decisionError) {
      if (isStaleRowVersion(decisionError)) {
        toast.error(
          "Yêu cầu đã được người khác xử lý. Dữ liệu mới nhất đã được tải lại.",
        );
        closeDecision();
        await Promise.allSettled([
          loadHistory(),
          Promise.resolve(onContractRefetch()),
        ]);
      } else {
        toast.error(
          getApiErrorMessage(decisionError, "Không thể xử lý yêu cầu duyệt."),
        );
      }
    } finally {
      setIsSubmittingDecision(false);
    }
  };

  const withdraw = async () => {
    const reason = withdrawReason.trim();
    if (!pendingRequest || !reason) {
      toast.error("Vui lòng nhập lý do rút yêu cầu duyệt.");
      return;
    }

    let withdrawCompleted = false;
    try {
      setIsWithdrawing(true);
      await contractApprovalApi.withdraw(pendingRequest.approvalRequestId, {
        rowVersion: pendingRequest.rowVersion,
        reason,
      });
      withdrawCompleted = true;
      setWithdrawReason("");
      setIsCreatingRound(true);
      const round = await createNegotiationRoundAfterApproval({
        contractId: contract.contractId,
        sourceVersionId: pendingRequest.versionId,
        changeNote: reason,
      });
      toast.success(
        `Đã rút hồ sơ và tạo phiên bản ${round.versionNo} để tiếp tục chỉnh sửa.`,
      );
    } catch (withdrawError) {
      if (withdrawCompleted) {
        const cause = getApiErrorMessage(
          withdrawError,
          "Không thể tạo phiên bản chỉnh sửa.",
        );
        toast.error(
          `Đã rút hồ sơ nhưng chưa thể tạo phiên bản chỉnh sửa: ${cause} Hãy thử lại tại tab Đàm phán.`,
        );
      } else if (isStaleRowVersion(withdrawError)) {
        toast.error("Yêu cầu đã thay đổi. Dữ liệu mới nhất đã được tải lại.");
      } else {
        toast.error(
          getApiErrorMessage(withdrawError, "Không thể rút yêu cầu duyệt."),
        );
      }
    } finally {
      setIsCreatingRound(false);
      setIsWithdrawing(false);
      await Promise.allSettled([
        loadHistory(),
        Promise.resolve(onContractRefetch()),
      ]);
    }
  };

  const openArtifacts = async (approvalRequestId: number) => {
    try {
      setIsLoadingArtifacts(true);
      setArtifactDetail(await contractApprovalApi.getDetail(approvalRequestId));
    } catch (loadError) {
      toast.error(getApiErrorMessage(loadError, "Không thể tải tài liệu đã gửi."));
    } finally {
      setIsLoadingArtifacts(false);
    }
  };

  const downloadArtifact = async (
    fileId: number,
    fileName: string,
    openPdf: boolean,
  ) => {
    const previewWindow = openPdf ? window.open("about:blank", "_blank") : null;
    try {
      setDownloadingFileId(fileId);
      const blob = await contractApprovalApi.downloadArtifact(fileId);
      if (openPdf && previewWindow) {
        const url = URL.createObjectURL(
          blob.type === "application/pdf"
            ? blob
            : new Blob([blob], { type: "application/pdf" }),
        );
        previewWindow.opener = null;
        previewWindow.location.replace(url);
        window.setTimeout(() => URL.revokeObjectURL(url), 60_000);
      } else {
        previewWindow?.close();
        downloadBlob(blob, fileName);
      }
    } catch (downloadError) {
      previewWindow?.close();
      toast.error(
        await getBlobApiErrorMessage(downloadError, "Không thể tải tài liệu đã gửi."),
      );
    } finally {
      setDownloadingFileId(null);
    }
  };

  return (
    <div className="space-y-4">
      {(canDecide || canReview) && pendingRequest && (
        <Alert className="border-emerald-500/30 bg-emerald-500/5">
          <CheckCircle2 className="size-4 text-emerald-600" />
          <AlertTitle>{canReview ? "Hợp đồng chờ kiểm tra hành chính" : "Hợp đồng đang chờ quyết định phê duyệt"}</AlertTitle>
          <AlertDescription className="mt-3 space-y-3">
            <p>
              Kiểm tra bộ tài liệu đã gửi trước khi duyệt hoặc yêu cầu sửa lại.
            </p>
            <Button onClick={() => void openDecision(pendingRequest)}>
              <Eye className="size-4" />
              {canReview ? "Kiểm tra hồ sơ" : "Xem hồ sơ và duyệt"}
            </Button>
          </AlertDescription>
        </Alert>
      )}
      {canWithdraw && pendingRequest && (
        <Alert className="border-amber-500/30 bg-amber-500/5">
          <Undo2 className="size-4 text-amber-600" />
          <AlertTitle>Hợp đồng đang chờ duyệt</AlertTitle>
          <AlertDescription className="mt-3 space-y-3">
            <p>
              Bạn có thể rút hồ sơ trước khi người quản lý xử lý. Hệ thống sẽ tự tạo
              phiên bản mới từ bản đã gửi để bạn tiếp tục chỉnh sửa.
            </p>
            <div className="space-y-2">
              <Label htmlFor="withdraw-reason">Lý do rút hồ sơ *</Label>
              <Textarea
                className="bg-white"
                id="withdraw-reason"
                value={withdrawReason}
                maxLength={1000}
                onChange={(event) => setWithdrawReason(event.target.value)}
                placeholder="Nhập lý do cần chỉnh sửa lại hợp đồng..."
              />
            </div>
            <Button
              variant="outline"
              disabled={isWithdrawing || isCreatingRound}
              onClick={() => void withdraw()}
            >
              {isWithdrawing || isCreatingRound ? (
                <Loader2 className="size-4 animate-spin" />
              ) : (
                <Undo2 className="size-4" />
              )}
              {isCreatingRound
                ? "Đang tạo phiên bản chỉnh sửa..."
                : isWithdrawing
                  ? "Đang rút hồ sơ..."
                  : "Rút yêu cầu duyệt"}
            </Button>
          </AlertDescription>
        </Alert>
      )}

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <FileClock className="size-5 text-primary" />
            Lịch sử phê duyệt
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="flex min-h-32 items-center justify-center">
              <Loader2 className="size-6 animate-spin text-primary" />
            </div>
          ) : error ? (
            <Alert variant="destructive">
              <AlertTitle>Không thể tải dữ liệu</AlertTitle>
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : history.length === 0 ? (
            <p className="py-8 text-center text-sm text-muted-foreground">
              Hợp đồng chưa có lần gửi duyệt nào.
            </p>
          ) : (
            <div className="space-y-3">
              {history.map((request) => {
                const Icon = statusIcon(request.status);
                return (
                  <div
                    key={request.approvalRequestId}
                    className="rounded-xl border p-4"
                  >
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                      <div className="flex items-start gap-3">
                        <div className="rounded-lg bg-muted p-2">
                          <Icon className="size-4" />
                        </div>
                        <div>
                          <div className="flex flex-wrap items-center gap-2">
                            <p className="font-semibold">
                              Hồ sơ gửi duyệt · Phiên bản{" "}
                              {request.versionNo}
                            </p>
                            <Badge
                              variant="outline"
                              className={cn(statusStyles[request.status])}
                            >
                              {getApprovalRequestStatusLabel(request.status)}
                            </Badge>
                          </div>
                          <p className="mt-1 text-xs text-muted-foreground">
                            {request.submittedByEmployeeName || "Nhân viên"} gửi
                            lúc {formatDateTime(request.submittedDate)}
                          </p>
                          {request.administrativeReviewedAt && (
                            <p className="mt-1 text-xs text-muted-foreground">
                              {request.administrativeReviewerEmployeeName || "Nhân viên quản trị"} kiểm tra lúc{" "}
                              {formatDateTime(request.administrativeReviewedAt)}
                              {request.administrativeReviewComment ? ` · ${request.administrativeReviewComment}` : ""}
                            </p>
                          )}
                          {request.resolvedDate && (
                            <p className="mt-1 text-xs text-muted-foreground">
                              {request.resolvedByEmployeeName || "Nhân viên"} xử
                              lý lúc {formatDateTime(request.resolvedDate)}
                            </p>
                          )}
                        </div>
                      </div>
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={isLoadingArtifacts}
                        onClick={() =>
                          void openArtifacts(request.approvalRequestId)
                        }
                      >
                        {isLoadingArtifacts ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <FileText className="size-4" />
                        )}
                        Tài liệu đã gửi
                      </Button>
                    </div>
                    {request.decisionComment && (
                      <div className="mt-3 rounded-lg bg-muted/50 p-3 text-sm">
                        <span className="font-medium">Ghi chú/lý do: </span>
                        {request.decisionComment}
                      </div>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={artifactDetail !== null}
        onOpenChange={(open) => !open && setArtifactDetail(null)}
      >
        <DialogContent className="sm:max-w-2xl">
          {artifactDetail && (
            <>
              <DialogHeader>
                <DialogTitle>
                  Tài liệu phiên bản {artifactDetail.versionNo}
                </DialogTitle>
                <DialogDescription>
                  Đây là bản bất biến được tạo tại thời điểm gửi duyệt.
                </DialogDescription>
              </DialogHeader>
              <div className="rounded-xl border bg-muted/20 p-3 text-sm">
                <p className="font-medium">
                  Phụ lục trong bộ tài liệu ({artifactDetail.appendices?.length ?? 0})
                </p>
                {(artifactDetail.appendices?.length ?? 0) > 0 ? (
                  <ul className="mt-2 list-disc space-y-1 pl-5 text-muted-foreground">
                    {artifactDetail.appendices.map((appendix) => (
                      <li key={appendix.appendixId}>
                        {appendix.appendixCode} — {appendix.appendixName}
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p className="mt-1 text-muted-foreground">Không có phụ lục.</p>
                )}
              </div>
              <div className="grid gap-3 sm:grid-cols-2">
                {artifactDetail.artifacts.map((artifact) => {
                  const isPdf = artifact.fileType.toLowerCase() === "pdf";
                  return (
                    <Button
                      key={artifact.fileId}
                      variant="outline"
                      className="h-auto justify-start p-4"
                      disabled={downloadingFileId === artifact.fileId}
                      onClick={() =>
                        void downloadArtifact(
                          artifact.fileId,
                          artifact.fileName,
                          isPdf,
                        )
                      }
                    >
                      {downloadingFileId === artifact.fileId ? (
                        <Loader2 className="size-5 animate-spin" />
                      ) : isPdf ? (
                        <Eye className="size-5" />
                      ) : (
                        <Download className="size-5" />
                      )}
                      <span className="min-w-0 text-left">
                        <span className="block truncate font-medium">
                          {artifact.fileName}
                        </span>
                        <span className="block text-xs text-muted-foreground">
                          {isPdf ? "Xem PDF" : "Tải DOCX"}
                        </span>
                      </span>
                    </Button>
                  );
                })}
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
      {decisionRequest && (
        <ContractApprovalDecisionDialog
          open
          mode="single"
          administrativeReview={decisionRequest.status === ApprovalRequestStatus.PendingAdministrativeReview}
          requests={[decisionRequest]}
          detail={decisionDetail}
          isLoadingDetail={isLoadingDecision}
          isSubmitting={isSubmittingDecision}
          downloadingFileId={downloadingFileId}
          onOpenChange={(open) => {
            if (!open && !isSubmittingDecision) closeDecision();
          }}
          onSubmit={submitDecision}
          onDownloadArtifact={(artifact, openPdf) =>
            downloadArtifact(artifact.fileId, artifact.fileName, openPdf)
          }
        />
      )}
    </div>
  );
}
