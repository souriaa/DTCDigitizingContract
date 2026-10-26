"use client";

import {
  useCallback,
  useState,
  useEffect,
  useRef,
  type Dispatch,
  type SetStateAction,
} from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import {
  ArrowLeft,
  Clock,
  ClipboardCheck,
  Download,
  Eye,
  FileSignature,
  FileText,
  Loader2,
  LockKeyhole,
  MessageSquareText,
  Save,
  Send,
  Users,
  WalletCards,
} from "lucide-react";

import { Header } from "@/components/ui/custom/header";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";
import { SplitActionMenu } from "@/components/ui/custom/split-action-menu";
import { ResourceAccessErrorState } from "@/components/auth/permission-guard";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { formatCurrency } from "@/lib/format-currency";
import { getPaymentDraftError } from "@/lib/contract-payment-draft";
import { ContractLegalBases } from "@/components/contracts/contract-legal-bases";
import { downloadBlob } from "@/components/contract-templates/contract-template-utils";

import {
  contractApi,
  CurrentContractCustomerAccessLinkResponse,
  ContractAppendixTermResponse,
  ContractDetailResponse,
  ContractItemDiscountMode,
  ContractLanguageMode,
  ContractStatus,
  ContractType,
  getContractTypeLabel,
  getContractDirectionLabel,
  getContractCounterpartyLabel,
  UpdateContractDraftRequest,
  ContractTermKind,
} from "@/services/contract-api";
import { roundContractMoney } from "@/lib/contract-finance";
import { useAuthStore } from "@/hooks/use-auth-store";
import { hasPermission, RBAC_PERMISSIONS } from "@/lib/rbac";
import {
  getApiErrorMessage,
  getBlobApiErrorMessage,
  getResourceAccessErrorKind,
  type ResourceAccessErrorKind,
  isResourceNotFound,
  isStaleRowVersion,
} from "@/lib/api-error";
import { toast } from "@/components/ui/sonner";

import {
  ContractStatusBadge,
  InfoCard,
} from "@/components/contracts/contract-helpers";
import { ContractOverview } from "@/components/contracts/contract-overview";
import { ContractTerms } from "@/components/contracts/contract-terms";
import { ContractAppendices } from "@/components/contracts/contract-appendices";
import { ContractNegotiation } from "@/components/contracts/contract-negotiation";
import { ContractSignature } from "@/components/contracts/contract-signature";
import { ContractDocuments } from "@/components/contracts/contract-attachments";
import { ContractClosing } from "@/components/contracts/contract-closing";
import { ContractAuditLog } from "@/components/contracts/contract-audit-log";
import { TransferResponsibilityModal } from "@/components/contracts/transfer-responsibility-modal";
import { ContractApprovalPanel } from "@/components/contracts/contract-approval-panel";
import { ContractSigningPanel } from "@/components/contracts/contract-signing-panel";
import { ContractSubmittedArtifactActions } from "@/components/contracts/contract-submitted-artifact-actions";
import { ContractPaymentDueAnchor } from "@/services/contract-completion-api";

const CONTRACT_TABS = [
  "overview",
  "terms",
  "legal-bases",
  "appendices",
  "negotiation",
  "approval",
  "customer-access",
  "signature",
  "documents",
  "closing",
  "activity",
] as const;

type ContractTab = (typeof CONTRACT_TABS)[number];

type KnownCustomerAccessLink = Pick<
  CurrentContractCustomerAccessLinkResponse,
  "linkId" | "state" | "expiresAt"
>;

const isContractTab = (value: string): value is ContractTab =>
  CONTRACT_TABS.some((tab) => tab === value);

const appendixTermMatches = (
  draft: ContractAppendixTermResponse,
  saved: ContractAppendixTermResponse,
) =>
  draft.termCode.trim() === saved.termCode.trim() &&
  draft.termTitle.trim() === saved.termTitle.trim() &&
  (draft.termTitleEn?.trim() || "") === (saved.termTitleEn?.trim() || "") &&
  (draft.termContent?.trim() || "") === (saved.termContent?.trim() || "") &&
  (draft.termContentEn?.trim() || "") === (saved.termContentEn?.trim() || "") &&
  draft.bilingualContentMode === saved.bilingualContentMode;

function ContractSaveShortcut({
  enabled,
  onSave,
}: {
  enabled: boolean;
  onSave: () => void;
}) {
  useEffect(() => {
    const handleKeyDown = (event: KeyboardEvent) => {
      if (
        !enabled ||
        event.defaultPrevented ||
        event.repeat ||
        event.altKey ||
        event.shiftKey ||
        !(event.ctrlKey || event.metaKey) ||
        event.key.toLowerCase() !== "s" ||
        document.querySelector('[role="dialog"], [role="alertdialog"]')
      )
        return;
      event.preventDefault();
      onSave();
    };
    window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [enabled, onSave]);
  return null;
}

export default function ContractDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const user = useAuthStore((state) => state.user);
  const currentEmployeeId = user?.employeeId;
  const currentPermissions = user?.permissions;

  const [contract, setContract] = useState<ContractDetailResponse | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [errorKind, setErrorKind] =
    useState<ResourceAccessErrorKind>("not-found");
  const [isUpdating, setIsUpdating] = useState(false);
  const saveInFlightRef = useRef(false);
  const [isStartingNegotiation, setIsStartingNegotiation] = useState(false);
  const [isSubmittingApproval, setIsSubmittingApproval] = useState(false);
  const [isOpeningPdf, setIsOpeningPdf] = useState(false);
  const [isDownloadingPdf, setIsDownloadingPdf] = useState(false);
  const [isDownloadingDocx, setIsDownloadingDocx] = useState(false);
  const [isTransferModalOpen, setIsTransferModalOpen] = useState(false);
  const [activeTab, setActiveTab] = useState<ContractTab>("overview");
  const [hasUnsavedChanges, setHasUnsavedChanges] = useState(false);
  const [appendixDrafts, setAppendixDrafts] = useState<
    Record<number, ContractAppendixTermResponse>
  >({});
  const [hasOpenAppendixForm, setHasOpenAppendixForm] = useState(false);
  const [appendixOperationBusy, setAppendixOperationBusy] = useState(false);
  const dirtyAppendixTerms =
    contract?.currentVersion.appendices.flatMap((appendix) =>
      appendix.terms
        .filter((term) => {
          const draft = appendixDrafts[term.appendixTermId];
          return draft && !appendixTermMatches(draft, term);
        })
        .map((term) => ({
          appendix,
          term,
          draft: appendixDrafts[term.appendixTermId],
        })),
    ) ?? [];
  const hasUnsavedAppendixChanges = dirtyAppendixTerms.length > 0;
  const hasAnyUnsavedChanges = hasUnsavedChanges || hasUnsavedAppendixChanges;
  const [knownCustomerAccessLink, setKnownCustomerAccessLink] =
    useState<KnownCustomerAccessLink | null>(null);

  useEffect(() => {
    let isMounted = true;

    const syncTabFromHash = () => {
      if (!isMounted) return;
      const tabFromHash = window.location.hash.slice(1);
      const searchParams = new URLSearchParams(window.location.search);
      const tabFromQuery = searchParams.get("tab");
      const targetTab = tabFromHash || tabFromQuery || "";

      if (isContractTab(targetTab)) {
        setActiveTab((current) =>
          current === targetTab ? current : targetTab,
        );
      }
    };

    syncTabFromHash();
    const timer = setTimeout(syncTabFromHash, 50);

    window.addEventListener("hashchange", syncTabFromHash);
    window.addEventListener("popstate", syncTabFromHash);

    const originalPushState = window.history.pushState;
    const originalReplaceState = window.history.replaceState;

    window.history.pushState = function (...args) {
      const result = originalPushState.apply(this, args);
      setTimeout(syncTabFromHash, 0);
      return result;
    };

    window.history.replaceState = function (...args) {
      const result = originalReplaceState.apply(this, args);
      setTimeout(syncTabFromHash, 0);
      return result;
    };

    return () => {
      isMounted = false;
      clearTimeout(timer);
      window.removeEventListener("hashchange", syncTabFromHash);
      window.removeEventListener("popstate", syncTabFromHash);
      window.history.pushState = originalPushState;
      window.history.replaceState = originalReplaceState;
    };
  }, []);

  const handleTabChange = (value: string) => {
    if (!isContractTab(value)) return;

    setActiveTab(value);
    window.history.replaceState(window.history.state, "", `#${value}`);
  };

  const handleContractChange: Dispatch<
    SetStateAction<ContractDetailResponse | null>
  > = (value) => {
    setContract(value);
    setHasUnsavedChanges(true);
  };

  useEffect(() => {
    if (!hasAnyUnsavedChanges) return;

    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };

    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [hasAnyUnsavedChanges]);

  const fetchContractDetail = useCallback(
    async (showLoading = true) => {
      if (!params.id) return;

      try {
        if (showLoading) setIsLoading(true);
        const contractId = Number(params.id);
        const res = await contractApi.getDetail(contractId);
        const canReadCustomerAccess =
          res.responsibleEmployee?.employeeId === currentEmployeeId ||
          hasPermission(currentPermissions, RBAC_PERMISSIONS.contractSupport);
        const currentLink = canReadCustomerAccess
          ? await contractApi.getCurrentCustomerAccessLink(contractId)
          : null;
        setContract(res);
        setKnownCustomerAccessLink(currentLink);
        setHasUnsavedChanges(false);
        setError(null);
      } catch (err: any) {
        console.error("Lỗi lấy chi tiết hợp đồng:", err);
        if (showLoading) {
          setErrorKind(getResourceAccessErrorKind(err));
          setError(
            isResourceNotFound(err)
              ? "Không tìm thấy hợp đồng hoặc bạn không có quyền truy cập."
              : getApiErrorMessage(
                  err,
                  "Không thể lấy dữ liệu hợp đồng. Vui lòng thử lại sau.",
                ),
          );
        }
        throw err;
      } finally {
        if (showLoading) setIsLoading(false);
      }
    },
    [currentEmployeeId, currentPermissions, params.id],
  );

  // Gọi API lấy dữ liệu hợp đồng
  useEffect(() => {
    void fetchContractDetail().catch(() => undefined);
  }, [fetchContractDetail]);

  const handleCustomerAccessLinkChange = useCallback(
    (link: KnownCustomerAccessLink | null) => {
      setKnownCustomerAccessLink(
        link
          ? {
              linkId: link.linkId,
              state: link.state,
              expiresAt: link.expiresAt,
            }
          : null,
      );
    },
    [],
  );

  if (isLoading) {
    return (
      <>
        <Header />
        <div className="flex h-[50vh] items-center justify-center">
          <div className="flex flex-col items-center gap-2 text-muted-foreground">
            <Loader2 className="size-8 animate-spin text-primary" />
            <p>Đang tải chi tiết hợp đồng...</p>
          </div>
        </div>
      </>
    );
  }

  if (error || !contract) {
    return (
      <>
        <Header />
        <ResourceAccessErrorState
          kind={errorKind}
          title={
            errorKind === "error"
              ? "Không thể tải hợp đồng"
              : "Không thể truy cập hợp đồng"
          }
          description={error || "Hợp đồng này không tồn tại hoặc đã bị xóa."}
          backHref="/contracts"
          backLabel="Quay lại danh sách"
          onRetry={() => void fetchContractDetail().catch(() => undefined)}
        />
      </>
    );
  }

  const handleUpdateDraft = async () => {
    if (
      !contract ||
      !canUpdateDraft ||
      appendixOperationBusy ||
      saveInFlightRef.current ||
      !hasAnyUnsavedChanges
    )
      return;
    if (hasOpenAppendixForm) {
      toast.error(
        "Hãy hoàn tất hoặc hủy form thêm phụ lục/điều khoản trước khi lưu chung.",
      );
      return;
    }

    for (const { draft } of dirtyAppendixTerms) {
      if (!draft.termCode.trim() || !draft.termTitle.trim()) {
        toast.error("Mã và tiêu đề điều khoản phụ lục là bắt buộc.");
        return;
      }
      if (
        contract.languageMode === ContractLanguageMode.Bilingual &&
        (!draft.termTitleEn?.trim() ||
          (!!draft.termContent?.trim() &&
            draft.bilingualContentMode === 1 &&
            !draft.termContentEn?.trim()))
      ) {
        toast.error(
          `Điều khoản phụ lục “${draft.termCode}” chưa đủ nội dung tiếng Anh.`,
        );
        return;
      }
    }

    const paymentDraftError = getPaymentDraftError(
      contract.currentVersion.terms.map((term) => ({
        termKind: term.termKind,
        paymentMilestones:
          term.termKind === ContractTermKind.Payment
            ? contract.currentVersion.paymentMilestones
            : [],
      })),
      contract.languageMode === ContractLanguageMode.Bilingual,
    );
    if (hasUnsavedChanges && paymentDraftError) {
      toast.error(paymentDraftError);
      return;
    }

    const manualPaymentMilestones =
      contract.currentVersion.paymentMilestones.filter(
        (item) => item.dueAnchor === ContractPaymentDueAnchor.ManualDate,
      );

    if (hasUnsavedChanges) {
      for (const basis of contract.currentVersion.legalBases) {
        if (
          !basis.contentVi.trim() ||
          (contract.languageMode === ContractLanguageMode.Bilingual &&
            !basis.contentEn?.trim())
        ) {
          toast.error(
            `Căn cứ “${basis.basisCode}” chưa đủ nội dung${contract.languageMode === ContractLanguageMode.Bilingual ? " tiếng Việt và tiếng Anh" : " tiếng Việt"}.`,
          );
          handleTabChange("legal-bases");
          return;
        }
      }
      if (!contract.contractName.trim()) {
        toast.error("Tên hợp đồng không được để trống.");
        return;
      }

      if (contract.currentVersion.items.length === 0) {
        toast.error("Hợp đồng phải có ít nhất một sản phẩm hoặc dịch vụ.");
        return;
      }

      if (contract.currentVersion.terms.length === 0) {
        toast.error("Hợp đồng phải có ít nhất một điều khoản.");
        return;
      }

      if (
        contract.effectiveDate &&
        contract.expireDate &&
        new Date(contract.expireDate) < new Date(contract.effectiveDate)
      ) {
        toast.error("Ngày hết hạn không được trước ngày hiệu lực.");
        return;
      }

      if (
        contract.languageMode === ContractLanguageMode.Bilingual &&
        !contract.contractNameEn?.trim()
      ) {
        toast.error("Hợp đồng song ngữ phải có tên hợp đồng tiếng Anh.");
        return;
      }

      for (const item of contract.currentVersion.items) {
        if (
          !item.unitName?.trim() ||
          (contract.languageMode === ContractLanguageMode.Bilingual &&
            !item.unitNameEn?.trim())
        ) {
          toast.error(
            `“${item.itemName}” thiếu đơn vị${item.unitName?.trim() ? " tiếng Anh" : ""}. Hãy cập nhật danh mục và thêm lại hạng mục trước khi lưu.`,
          );
          return;
        }
        if (
          contract.languageMode === ContractLanguageMode.Bilingual &&
          !item.itemNameEn?.trim()
        ) {
          toast.error(`Vui lòng nhập tên tiếng Anh cho “${item.itemName}”.`);
          return;
        }
        if (!Number.isFinite(item.quantity) || item.quantity <= 0) {
          toast.error(
            "Vui lòng nhập số lượng hợp lệ cho tất cả sản phẩm/dịch vụ.",
          );
          return;
        }

        if (!Number.isFinite(item.unitPrice) || item.unitPrice < 0) {
          toast.error(`Đơn giá của “${item.itemName}” không hợp lệ.`);
          return;
        }

        if (
          item.discountMode === ContractItemDiscountMode.Percentage &&
          (item.discountPercent < 0 || item.discountPercent > 100)
        ) {
          toast.error(`Chiết khấu của “${item.itemName}” phải từ 0% đến 100%.`);
          return;
        }

        const lineSubtotal = roundContractMoney(
          item.quantity * item.unitPrice,
          contract.currencyCode,
        );
        if (
          item.discountMode === ContractItemDiscountMode.FixedAmount &&
          (item.fixedDiscountAmount < 0 ||
            item.fixedDiscountAmount > lineSubtotal)
        ) {
          toast.error(
            `Số tiền giảm của “${item.itemName}” không được vượt quá tạm tính.`,
          );
          return;
        }

        if (item.isTaxable && (item.vatPercent < 0 || item.vatPercent > 100)) {
          toast.error(`Thuế VAT của “${item.itemName}” phải từ 0% đến 100%.`);
          return;
        }
      }

      for (const term of contract.currentVersion.terms) {
        if (!term.termTitle.trim()) {
          toast.error("Tiêu đề điều khoản không được để trống.");
          return;
        }

        if (
          contract.languageMode === ContractLanguageMode.Bilingual &&
          (!term.termTitleEn?.trim() ||
            (!!term.termContent?.trim() &&
              term.bilingualContentMode === 1 &&
              !term.termContentEn?.trim()))
        ) {
          toast.error(
            `Điều khoản “${term.termCode}” chưa đủ nội dung tiếng Anh.`,
          );
          return;
        }
      }

      if (manualPaymentMilestones.some((item) => !item.anchorDate)) {
        toast.error(
          "Vui lòng nhập ngày bắt đầu tính hạn cho các đợt dùng Lịch thủ công.",
        );
        return;
      }
    }

    saveInFlightRef.current = true;
    setIsUpdating(true);
    let mainDraftSaved = false;
    try {
      if (hasUnsavedChanges) {
        // Map payload chuẩn hóa theo UpdateContractDraftRequest
        const payload: UpdateContractDraftRequest = {
          rowVersion: contract.rowVersion,
          currentVersionId: contract.currentVersion.versionId,
          currentVersionRowVersion: contract.currentVersion.rowVersion,
          customerId: contract.customer.customerId,
          contractName: contract.contractName,
          contractNameEn: contract.contractNameEn,
          effectiveDate: contract.effectiveDate,
          expireDate: contract.expireDate,
          currencyCode: contract.currencyCode,

          // Map Items (Bao gồm Id và rowVersion)
          items: contract.currentVersion.items.map((item) => ({
            contractItemId:
              item.contractItemId < 0 ? null : item.contractItemId,
            rowVersion: item.contractItemId < 0 ? null : item.rowVersion,
            itemType: item.itemType,
            sourceProductId: item.sourceProductId,
            sourceServiceId: item.sourceServiceId,
            itemCode: item.itemCode,
            itemName: item.itemName,
            itemNameEn: item.itemNameEn,
            itemDescription: item.itemDescription,
            itemDescriptionEn: item.itemDescriptionEn,
            unitName: item.unitName,
            unitNameEn: item.unitNameEn,
            quantity: item.quantity,
            unitPrice: item.unitPrice,
            discountMode: item.discountMode,
            discountPercent: item.discountPercent,
            fixedDiscountAmount: item.fixedDiscountAmount,
            isTaxable: item.isTaxable,
            vatPercent: item.vatPercent,
            displayOrder: item.displayOrder,
          })),

          // Map Terms (Bao gồm Id và rowVersion)
          legalBases: contract.currentVersion.legalBases.map((basis) => ({
            legalBasisId: basis.legalBasisId,
            rowVersion: basis.rowVersion,
            contentVi: basis.contentVi,
            contentEn: basis.contentEn,
          })),
          terms: contract.currentVersion.terms.map((term) => ({
            termId: term.termId < 0 ? null : term.termId,
            rowVersion: term.termId < 0 ? null : term.rowVersion,
            termKind: term.termKind,
            paymentMilestones:
              term.termKind === ContractTermKind.Payment
                ? contract.currentVersion.paymentMilestones.map((row) => ({
                    ...row,
                    paymentMilestoneId:
                      row.paymentMilestoneId > 0
                        ? row.paymentMilestoneId
                        : null,
                    rowVersion:
                      row.paymentMilestoneId > 0 ? row.rowVersion : null,
                  }))
                : [],
            termCode: term.termCode,
            termTitle: term.termTitle,
            termTitleEn: term.termTitleEn,
            termContent: term.termContent,
            termContentEn: term.termContentEn,
            bilingualContentMode: term.bilingualContentMode,
            isNegotiable: term.isNegotiable,
            displayOrder: term.displayOrder,
          })),
        };

        const res: any = await contractApi.updateDraft(
          contract.contractId,
          payload,
        );
        const updatedData = res.data ? res.data : res;

        setContract(updatedData); // Cập nhật lại UI với data mới
        setHasUnsavedChanges(false);
        mainDraftSaved = true;
      }

      for (const { appendix, term, draft } of dirtyAppendixTerms) {
        const latest = await contractApi.getDetail(contract.contractId);
        if (
          latest.currentVersion.versionId !== contract.currentVersion.versionId
        ) {
          throw new Error(
            "Phiên bản hợp đồng đã thay đổi. Vui lòng tải lại trang.",
          );
        }
        const latestAppendix = latest.currentVersion.appendices.find(
          (item) => item.appendixId === appendix.appendixId,
        );
        const latestTerm = latestAppendix?.terms.find(
          (item) => item.appendixTermId === term.appendixTermId,
        );
        if (!latestAppendix || !latestTerm) {
          throw new Error(
            "Điều khoản phụ lục đã thay đổi. Vui lòng tải lại trang.",
          );
        }
        if (appendixTermMatches(draft, latestTerm)) continue;
        if (draft.rowVersion !== latestTerm.rowVersion) {
          throw new Error(
            `Điều khoản phụ lục “${draft.termCode}” đã được người khác sửa. Bản nháp của bạn vẫn được giữ lại.`,
          );
        }
        const saved = await contractApi.updateAppendixTerm(
          contract.contractId,
          contract.currentVersion.versionId,
          appendix.appendixId,
          term.appendixTermId,
          {
            termCode: draft.termCode.trim(),
            termTitle: draft.termTitle.trim(),
            termTitleEn: draft.termTitleEn?.trim() || null,
            termContent: draft.termContent?.trim() || null,
            termContentEn: draft.termContentEn?.trim() || null,
            bilingualContentMode: draft.bilingualContentMode,
            displayOrder: latestTerm.displayOrder,
            rowVersion: latestTerm.rowVersion,
            appendixRowVersion: latestAppendix.rowVersion,
            versionRowVersion: latest.currentVersion.rowVersion,
          },
        );
        setAppendixDrafts((current) => ({
          ...current,
          [term.appendixTermId]: saved,
        }));
      }

      if (hasUnsavedAppendixChanges) {
        setContract(await contractApi.getDetail(contract.contractId));
        setAppendixDrafts({});
      }
      toast.success(
        hasUnsavedChanges && hasUnsavedAppendixChanges
          ? "Đã lưu các thay đổi hợp đồng và phụ lục."
          : hasUnsavedAppendixChanges
            ? "Đã lưu các điều khoản phụ lục."
            : "Cập nhật bản nháp thành công!",
      );
    } catch (err: any) {
      console.error("Lỗi cập nhật bản nháp:", err);
      if (mainDraftSaved || !hasUnsavedChanges) {
        try {
          setContract(await contractApi.getDetail(contract.contractId));
        } catch {
          // Giữ bản nháp để người dùng thử lại khi tải dữ liệu thất bại.
        }
      }
      if (isStaleRowVersion(err)) {
        toast.error(
          "Hợp đồng đã được thay đổi ở nơi khác. Bản nháp của bạn vẫn được giữ lại; hãy kiểm tra xung đột trước khi thử lại.",
        );
      } else {
        toast.error(
          err instanceof Error && err.name === "Error"
            ? err.message
            : getApiErrorMessage(
                err,
                "Chưa lưu được toàn bộ thay đổi. Phần chưa lưu vẫn được giữ lại để thử lại.",
              ),
        );
      }
    } finally {
      saveInFlightRef.current = false;
      setIsUpdating(false);
    }
  };

  const handleStartNegotiation = async () => {
    if (!contract || !canManageContract) return;
    if (isUpdating || appendixOperationBusy) return;
    if (hasAnyUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi trước khi bắt đầu đàm phán.");
      return;
    }

    setIsStartingNegotiation(true);
    try {
      const res: any = await contractApi.startNegotiation(contract.contractId, {
        rowVersion: contract.rowVersion,
      });
      const updatedData = res.data ? res.data : res;
      setContract(updatedData);
      handleTabChange("negotiation");
      toast.success("Hợp đồng đã chuyển sang trạng thái Đàm phán!");
    } catch (err: any) {
      console.error("Lỗi bắt đầu đàm phán:", err);
      if (isStaleRowVersion(err)) {
        await fetchContractDetail(false).catch(() => undefined);
        toast.error("Hợp đồng đã thay đổi. Dữ liệu mới nhất đã được tải lại.");
      } else {
        toast.error(
          getApiErrorMessage(
            err,
            "Không thể bắt đầu đàm phán. Vui lòng thử lại.",
          ),
        );
      }
    } finally {
      setIsStartingNegotiation(false);
    }
  };

  const handleSubmitApproval = () => {
    if (!contract || !contract.currentVersion || !canManageContract) return;
    if (isUpdating || appendixOperationBusy) return;
    if (hasAnyUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi trước khi gửi duyệt.");
      return;
    }
    if (!canSubmitApproval) {
      toast.error(
        effectiveApprovalBlockers[0]?.message ||
          "Hợp đồng chưa đủ điều kiện gửi duyệt.",
      );
      return;
    }

    showConfirmToast({
      title: "Gửi hợp đồng để duyệt?",
      description:
        "Sau khi gửi, hợp đồng sẽ chuyển sang trạng thái Chờ duyệt và không thể chỉnh sửa cho đến khi được xử lý.",
      confirmLabel: "Gửi duyệt",
      cancelLabel: "Hủy",
      onConfirm: async () => {
        setIsSubmittingApproval(true);
        try {
          const payload = {
            rowVersion: contract.rowVersion,
            currentVersionId: contract.currentVersion.versionId,
            currentVersionRowVersion: contract.currentVersion.rowVersion,
            workflowId: null,
          };
          await contractApi.submitApproval(contract.contractId, payload);

          const detailRes: ContractDetailResponse = await contractApi.getDetail(
            contract.contractId,
          );
          handleCustomerAccessLinkChange(null);
          setContract(detailRes);

          toast.success("Hợp đồng đã được gửi duyệt!");
        } catch (err: any) {
          console.error("Lỗi gửi duyệt hợp đồng:", err);
          if (isStaleRowVersion(err)) {
            await fetchContractDetail(false).catch(() => undefined);
            toast.error(
              "Hợp đồng đã thay đổi. Dữ liệu mới nhất đã được tải lại.",
            );
          } else {
            toast.error(
              getApiErrorMessage(err, "Không thể gửi duyệt. Vui lòng thử lại."),
            );
          }
        } finally {
          setIsSubmittingApproval(false);
        }
      },
    });
  };

  const isResponsibleEmployee =
    user?.employeeId === contract.responsibleEmployee?.employeeId;
  const canManageContract =
    isResponsibleEmployee &&
    hasPermission(user?.permissions, RBAC_PERMISSIONS.contractManageOwn);
  const canViewCustomerAccess =
    isResponsibleEmployee ||
    hasPermission(user?.permissions, RBAC_PERMISSIONS.contractSupport);
  const canManageSigning = hasPermission(
    user?.permissions,
    RBAC_PERMISSIONS.contractSigningManage,
  );
  const canManageAcceptance = hasPermission(user?.permissions, RBAC_PERMISSIONS.contractAcceptanceManage)
    || (isResponsibleEmployee && contract.status === ContractStatus.Signed
      && hasPermission(user?.permissions, RBAC_PERMISSIONS.contractAcceptanceManageOwn));
  const canManagePayment = hasPermission(user?.permissions, RBAC_PERMISSIONS.contractPaymentManage)
    || (isResponsibleEmployee && contract.status === ContractStatus.Signed
      && hasPermission(user?.permissions, RBAC_PERMISSIONS.contractPaymentManageOwn));
  const canCompleteContract = hasPermission(
    user?.permissions,
    RBAC_PERMISSIONS.contractComplete,
  );
  const visibleActiveTab =
    activeTab === "customer-access" && !canViewCustomerAccess
      ? canManageSigning
        ? "signature"
        : canManageAcceptance || canManagePayment || canCompleteContract
          ? "closing"
          : "overview"
      : activeTab;
  const effectiveApprovalBlockers = contract.approvalReadiness.blockers;
  const canSubmitApproval = contract.approvalReadiness.canSubmit;
  const isCurrentVersionShared = contract.approvalReadiness.hasEverBeenShared;
  const hasActiveCurrentVersionLink =
    contract.approvalReadiness.hasActiveCurrentVersionLink;
  const canUpdateDraft =
    canManageContract &&
    (contract.status === ContractStatus.Draft ||
      contract.status === ContractStatus.Negotiating) &&
    !contract.currentVersion.isLocked;
  const canCommentOnCurrentVersion =
    canManageContract &&
    contract.status === ContractStatus.Negotiating &&
    !contract.currentVersion.isLocked;
  const canPreviewContractDocument =
    canManageContract &&
    (contract.status === ContractStatus.Draft ||
      contract.status === ContractStatus.Negotiating) &&
    [ContractType.SoftwareSupply, ContractType.SoftwareMaintenance, ContractType.SoftwareUpkeep].includes(contract.contractType);

  const viewContractPdf = async () => {
    if (isOpeningPdf) return;

    const previewWindow = window.open("about:blank", "_blank");
    if (!previewWindow) {
      toast.error(
        "Trình duyệt đã chặn tab xem trước. Vui lòng cho phép cửa sổ bật lên.",
      );
      return;
    }

    previewWindow.opener = null;
    previewWindow.document.title = "Đang tải hợp đồng...";
    previewWindow.document.body.textContent = "Đang tải bản PDF hợp đồng...";

    try {
      setIsOpeningPdf(true);
      const blob = await contractApi.downloadPreviewPdf(contract.contractId);
      const pdfUrl = URL.createObjectURL(
        blob.type === "application/pdf"
          ? blob
          : new Blob([blob], { type: "application/pdf" }),
      );
      previewWindow.location.replace(pdfUrl);
      window.setTimeout(() => URL.revokeObjectURL(pdfUrl), 60_000);
    } catch (error) {
      previewWindow.close();
      toast.error(
        await getBlobApiErrorMessage(error, "Không thể mở bản PDF hợp đồng."),
      );
    } finally {
      setIsOpeningPdf(false);
    }
  };

  const downloadContractPdf = async () => {
    if (isDownloadingPdf) return;

    try {
      setIsDownloadingPdf(true);
      const blob = await contractApi.downloadPreviewPdf(contract.contractId);
      downloadBlob(
        blob,
        `${contract.contractCode || `contract-${contract.contractId}`}-preview.pdf`,
      );
    } catch (error) {
      toast.error(
        await getBlobApiErrorMessage(error, "Không thể tải bản PDF hợp đồng."),
      );
    } finally {
      setIsDownloadingPdf(false);
    }
  };

  const downloadContractDocx = async () => {
    if (isDownloadingDocx) return;

    try {
      setIsDownloadingDocx(true);
      const blob = await contractApi.downloadPreviewDocx(contract.contractId);
      downloadBlob(
        blob,
        `${contract.contractCode || `contract-${contract.contractId}`}-preview.docx`,
      );
    } catch (error) {
      toast.error(
        await getBlobApiErrorMessage(
          error,
          "Không thể tải bản DOCX xem trước.",
        ),
      );
    } finally {
      setIsDownloadingDocx(false);
    }
  };

  return (
    <>
      <Header />

      <div className="grow overflow-y-auto p-2 lg:p-10 space-y-6">
        <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
          <div>
            <Button
              variant="ghost"
              className="-ml-3 mb-2 text-muted-foreground"
              onClick={() => router.push("/contracts")}
            >
              <ArrowLeft className="size-4 mr-2" />
              Quay lại danh sách
            </Button>
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-bold tracking-tight text-foreground">
                {contract.contractCode}
              </h1>
              <ContractStatusBadge status={contract.status} />
            </div>
            <p className="mt-2 max-w-3xl text-sm text-muted-foreground">
              {contract.contractName}
            </p>
          </div>

          <div className="flex flex-wrap gap-2">
            {canPreviewContractDocument && (
              <SplitActionMenu
                primaryLabel="Xem bản xem trước PDF"
                primaryIcon={<Eye className="size-4" />}
                onPrimaryClick={viewContractPdf}
                isLoading={isOpeningPdf}
                buttonClassName="h-9"
                menuItems={[
                  {
                    label: "Tải bản xem trước DOCX",
                    icon: isDownloadingDocx ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <FileText className="size-4" />
                    ),
                    onClick: downloadContractDocx,
                    disabled: isDownloadingDocx,
                  },
                  {
                    label: "Tải bản xem trước PDF",
                    icon: isDownloadingPdf ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Download className="size-4" />
                    ),
                    onClick: downloadContractPdf,
                    disabled: isDownloadingPdf,
                  },
                ]}
              />
            )}
            <ContractSubmittedArtifactActions
              contractId={contract.contractId}
              versionId={contract.currentVersion.versionId}
              enabled={
                contract.status !== ContractStatus.Draft &&
                contract.status !== ContractStatus.Negotiating
              }
            />
            {canUpdateDraft && (
              <Button
                variant="outline"
                onClick={handleUpdateDraft}
                disabled={isUpdating || appendixOperationBusy}
                className="bg-blue-50 text-blue-700 hover:bg-blue-100 hover:text-blue-800 border-blue-200"
              >
                {isUpdating ? (
                  <Loader2 className="size-4 mr-2 animate-spin" />
                ) : (
                  <Save className="size-4 mr-2" />
                )}
                Lưu thay đổi
              </Button>
            )}
            {canManageContract && contract.status === ContractStatus.Draft && (
              <Button
                onClick={handleStartNegotiation}
                disabled={
                  isStartingNegotiation ||
                  isUpdating ||
                  appendixOperationBusy ||
                  hasAnyUnsavedChanges
                }
                className="bg-emerald-600 hover:bg-emerald-700 text-white"
              >
                {isStartingNegotiation ? (
                  <Loader2 className="size-4 mr-2 animate-spin" />
                ) : (
                  <MessageSquareText className="size-4 mr-2" />
                )}
                Bắt đầu đàm phán
              </Button>
            )}
            {canManageContract &&
              contract.status === ContractStatus.Negotiating && (
                <Button
                  onClick={handleSubmitApproval}
                  disabled={
                    isSubmittingApproval ||
                    isUpdating ||
                    appendixOperationBusy ||
                    hasAnyUnsavedChanges ||
                    !canSubmitApproval
                  }
                  title={effectiveApprovalBlockers[0]?.message}
                  className="bg-amber-600 hover:bg-amber-700 text-white"
                >
                  {isSubmittingApproval ? (
                    <Loader2 className="size-4 mr-2 animate-spin" />
                  ) : (
                    <Send className="size-4 mr-2" />
                  )}
                  Gửi duyệt
                </Button>
              )}
            {/* <Button variant="outline">
              <DatabaseZap className="size-4 mr-2" />
              Tạo từ báo giá
            </Button>
            <Button>
              <FileSignature className="size-4 mr-2" />
              Gửi ký OTP
            </Button> */}
          </div>
        </div>

        <div className="grid gap-4 md:grid-cols-5">
          <InfoCard
            icon={<Users className="size-4" />}
            label={getContractCounterpartyLabel(contract.contractDirection)}
            value={
              contract.customer?.customerCompany ||
              contract.customer?.customerFullName
            }
          />
          <InfoCard
            icon={<FileSignature className="size-4" />}
            label="Loại hợp đồng"
            value={`${getContractDirectionLabel(contract.contractDirection)} · ${getContractTypeLabel(contract.contractType)}`}
          />
          <InfoCard
            icon={<FileText className="size-4" />}
            label="Báo giá liên quan"
            value={"Chưa liên kết"}
          />
          <InfoCard
            icon={<WalletCards className="size-4" />}
            label="Tổng thanh toán"
            value={
              <span className="text-primary break-all">
                {formatCurrency(
                  contract.totalPayment ?? contract.totalAmount,
                  contract.currencyCode,
                )}
              </span>
            }
          />
          <div className="rounded-xl border bg-muted/20 p-4 flex flex-col justify-between">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-2 text-sm text-muted-foreground">
                <Clock className="size-4" />
                Người phụ trách
              </div>
              {canManageContract && (
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-xs text-primary hover:bg-primary/10"
                  onClick={() => setIsTransferModalOpen(true)}
                >
                  Chuyển giao
                </Button>
              )}
            </div>
            <div className="mt-2 font-semibold text-foreground">
              {contract.responsibleEmployee?.employeeFullName || "Chưa gán"}
            </div>
          </div>
        </div>

        {isCurrentVersionShared &&
          (contract.status === ContractStatus.Negotiating ||
            contract.status === ContractStatus.Draft) && (
            <Alert className="border-amber-300 bg-amber-50/60 dark:bg-amber-950/20">
              <Users className="size-4 text-amber-700" />
              <AlertTitle>
                {hasActiveCurrentVersionLink
                  ? "Phiên bản đang được chia sẻ với khách hàng"
                  : "Phiên bản đã từng được chia sẻ với khách hàng"}
              </AlertTitle>
              <AlertDescription>
                {hasActiveCurrentVersionLink
                  ? "Mọi thay đổi sau khi lưu sẽ được khách hàng nhìn thấy trên cùng liên kết truy cập."
                  : "Liên kết cũ hiện không còn hoạt động. Bạn vẫn có thể chỉnh sửa và lưu; chỉ cần tạo liên kết mới nếu muốn chia sẻ lại."}
              </AlertDescription>
            </Alert>
          )}

        {canManageContract &&
          contract.status === ContractStatus.Negotiating &&
          !canSubmitApproval && (
            <Alert className="border-amber-300 bg-amber-50/60 dark:bg-amber-950/20">
              <Send className="size-4 text-amber-700" />
              <AlertTitle>Chưa đủ điều kiện gửi duyệt</AlertTitle>
              <AlertDescription>
                <ul className="list-disc space-y-1 pl-5">
                  {effectiveApprovalBlockers.map((blocker) => (
                    <li key={`${blocker.code}:${blocker.message}`}>
                      {blocker.message}
                      {blocker.code === "EnglishSourceMissingCustomer" &&
                        hasPermission(
                          user?.permissions,
                          RBAC_PERMISSIONS.customerManage,
                        ) && (
                          <Link
                            href={`/customers/${contract.customer.customerId}`}
                            className="ml-2 font-medium underline"
                          >
                            Bổ sung thông tin khách hàng
                          </Link>
                        )}
                      {blocker.code === "EnglishSourceMissingProvider" &&
                        hasPermission(
                          user?.permissions,
                          RBAC_PERMISSIONS.tenantLegalProfileManage,
                        ) && (
                          <Link
                            href="/admin/legal-profile"
                            className="ml-2 font-medium underline"
                          >
                            Bổ sung hồ sơ pháp lý
                          </Link>
                        )}
                    </li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}

        {canManageContract &&
          contract.status === ContractStatus.Draft &&
          effectiveApprovalBlockers.some((item) =>
            item.code.startsWith("EnglishSourceMissing"),
          ) && (
            <Alert>
              <AlertTitle>
                Cần bổ sung thông tin tiếng Anh cho tài liệu
              </AlertTitle>
              <AlertDescription className="space-y-2">
                <ul className="list-disc pl-5">
                  {effectiveApprovalBlockers
                    .filter((item) =>
                      item.code.startsWith("EnglishSourceMissing"),
                    )
                    .map((item) => (
                      <li key={`${item.code}:${item.message}`}>
                        {item.message}
                      </li>
                    ))}
                </ul>
                <div className="flex flex-wrap gap-3">
                  {hasPermission(
                    currentPermissions,
                    RBAC_PERMISSIONS.customerManage,
                  ) && (
                    <Link
                      className="font-medium underline"
                      href={`/customers/${contract.customer.customerId}`}
                    >
                      Thông tin khách hàng
                    </Link>
                  )}
                  {hasPermission(
                    currentPermissions,
                    RBAC_PERMISSIONS.tenantLegalProfileManage,
                  ) && (
                    <Link
                      className="font-medium underline"
                      href="/admin/legal-profile"
                    >
                      Hồ sơ pháp lý
                    </Link>
                  )}
                  <span>
                    Nếu không có quyền sửa, hãy nhờ người phụ trách bổ sung rồi
                    tải lại hợp đồng.
                  </span>
                </div>
              </AlertDescription>
            </Alert>
          )}

        {!canManageContract && (
          <Alert>
            <LockKeyhole className="size-4" />
            <AlertTitle>Chế độ chỉ xem</AlertTitle>
            <AlertDescription>
              Nội dung hợp đồng chỉ người phụ trách mới được chỉnh sửa. Các thao
              tác ký, nghiệm thu, thanh toán và hoàn tất phụ thuộc vào vai trò
              của bạn.
            </AlertDescription>
          </Alert>
        )}

        <ContractSaveShortcut
          enabled={
            canUpdateDraft &&
            hasAnyUnsavedChanges &&
            !isUpdating &&
            !appendixOperationBusy &&
            !hasOpenAppendixForm
          }
          onSave={() => void handleUpdateDraft()}
        />

        <Tabs
          value={visibleActiveTab}
          onValueChange={handleTabChange}
          className="space-y-4"
        >
          <TabsList className="flex h-auto w-full flex-wrap justify-start">
            <TabsTrigger value="overview">Tổng quan</TabsTrigger>
            <TabsTrigger value="documents">Chứng từ</TabsTrigger>
            <TabsTrigger value="negotiation">Vòng đàm phán</TabsTrigger>
            <TabsTrigger value="terms">Điều khoản - Trao đổi</TabsTrigger>
            <TabsTrigger value="legal-bases">Căn cứ</TabsTrigger>
            <TabsTrigger value="appendices">
              Phụ lục ({contract.currentVersion.appendices?.length ?? 0})
            </TabsTrigger>
            {canViewCustomerAccess && (
              <TabsTrigger value="customer-access">
                Truy cập khách hàng
              </TabsTrigger>
            )}
            <TabsTrigger value="approval">Phê duyệt</TabsTrigger>
            <TabsTrigger value="signature">Ký hợp đồng</TabsTrigger>
            <TabsTrigger value="closing">Đóng hợp đồng</TabsTrigger>
            <TabsTrigger value="activity">Lịch sử hoạt động</TabsTrigger>
          </TabsList>

          <TabsContent value="overview">
            <ContractOverview
              contract={contract}
              setContract={handleContractChange}
              canEdit={canUpdateDraft && !appendixOperationBusy}
              onOpenTransferModal={() => setIsTransferModalOpen(true)}
            />
          </TabsContent>

          <TabsContent value="terms">
            <ContractTerms
              contract={contract}
              setContract={setContract}
              canEdit={canUpdateDraft && !appendixOperationBusy}
              canComment={canCommentOnCurrentVersion}
              onDraftChange={() => setHasUnsavedChanges(true)}
            />
          </TabsContent>

          <TabsContent value="legal-bases">
            <ContractLegalBases
              bases={contract.currentVersion.legalBases}
              isBilingual={
                contract.languageMode === ContractLanguageMode.Bilingual
              }
              canEdit={canUpdateDraft && !isUpdating && !appendixOperationBusy}
              onChange={(legalBasisId, field, value) =>
                handleContractChange((current) =>
                  !current
                    ? current
                    : {
                        ...current,
                        currentVersion: {
                          ...current.currentVersion,
                          legalBases: current.currentVersion.legalBases.map(
                            (basis) =>
                              basis.legalBasisId === legalBasisId
                                ? { ...basis, [field]: value }
                                : basis,
                          ),
                        },
                      },
                )
              }
            />
          </TabsContent>

          <TabsContent value="appendices">
            <ContractAppendices
              contract={contract}
              canEdit={canUpdateDraft}
              onRefetch={() => fetchContractDetail(false)}
              drafts={appendixDrafts}
              onTermDraftChange={(term) =>
                setAppendixDrafts((current) => ({
                  ...current,
                  [term.appendixTermId]: term,
                }))
              }
              hasUnsavedChanges={hasAnyUnsavedChanges}
              isSaving={isUpdating}
              onPendingFormChange={setHasOpenAppendixForm}
              onOperationChange={setAppendixOperationBusy}
            />
          </TabsContent>

          <TabsContent value="negotiation">
            <ContractNegotiation
              contract={contract}
              setContract={setContract}
              canManage={canManageContract}
              hasUnsavedChanges={hasAnyUnsavedChanges}
              onNegotiationRoundCreated={() =>
                handleCustomerAccessLinkChange(null)
              }
            />
          </TabsContent>

          <TabsContent value="approval">
            <ContractApprovalPanel
              contract={contract}
              canManage={canManageContract}
              onContractRefetch={() => fetchContractDetail(false)}
            />
          </TabsContent>

          {canViewCustomerAccess && (
            <TabsContent value="customer-access">
              <ContractSignature
                contract={contract}
                onContractRefetch={() => fetchContractDetail(false)}
                knownLink={knownCustomerAccessLink}
                hasUnsavedChanges={hasAnyUnsavedChanges}
                canManage={canManageContract}
                onCustomerAccessLinkChange={handleCustomerAccessLinkChange}
              />
            </TabsContent>
          )}

          <TabsContent value="signature">
            <ContractSigningPanel
              contract={contract}
              canManageSigning={canManageSigning}
              onContractRefetch={() => fetchContractDetail(false)}
            />
          </TabsContent>

          <TabsContent value="documents">
            <ContractDocuments
              contract={contract}
              canManage={canManageContract}
            />
          </TabsContent>

          <TabsContent value="closing">
            <ContractClosing
              contract={contract}
              canManageAcceptance={canManageAcceptance}
              canManagePayment={canManagePayment}
              canComplete={canCompleteContract}
              onContractRefetch={() => fetchContractDetail(false)}
            />
          </TabsContent>

          <TabsContent value="activity">
            <ContractAuditLog contractId={contract.contractId} />
          </TabsContent>
        </Tabs>

        {canManageContract && (
          <TransferResponsibilityModal
            isOpen={isTransferModalOpen}
            onClose={() => setIsTransferModalOpen(false)}
            contractId={contract.contractId}
            rowVersion={contract.rowVersion}
            currentEmployeeId={contract.responsibleEmployee?.employeeId}
            currentEmployeeName={contract.responsibleEmployee?.employeeFullName}
          />
        )}

        {hasAnyUnsavedChanges && canUpdateDraft && (
          <Alert
            className="fixed bottom-6 right-6 z-50 w-[calc(100%-3rem)] max-w-md animate-in fade-in-0 slide-in-from-bottom-8 bg-background shadow-lg duration-300"
            aria-live="polite"
          >
            <AlertTitle>Hợp đồng có thay đổi chưa được lưu</AlertTitle>
            <AlertDescription className="mt-2 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
              <span>Bạn có muốn lưu các thay đổi vừa thực hiện không?</span>
              <Button
                onClick={handleUpdateDraft}
                disabled={isUpdating || appendixOperationBusy}
                size="sm"
                className="shrink-0"
              >
                {isUpdating ? (
                  <Loader2 className="size-4 mr-2 animate-spin" />
                ) : (
                  <Save className="size-4 mr-2" />
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
    </>
  );
}
