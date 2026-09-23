"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { format } from "date-fns";
import {
  BookmarkCheck,
  Building2,
  CheckCircle2,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Download,
  Eye,
  FileCheck2,
  FileSignature,
  FileText,
  Layers,
  Loader2,
  Plus,
  Save,
  ScrollText,
  XCircle,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { cn } from "@/lib/utils";
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
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { DateFilter } from "@/components/ui/custom/date-filter";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import {
  ContractTermCard,
  type ContractTermEditableField,
} from "@/components/contracts/contract-term-card";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  ContractStatus,
  type ContractDetailResponse,
} from "@/services/contract-api";
import type { ContractPaymentMilestoneResponse } from "@/services/contract-completion-api";
import {
  AcceptanceKind,
  AcceptancePartyRole,
  AcceptanceRecordStatus,
  AcceptanceReferenceType,
  contractAcceptanceApi,
  type ContractAcceptanceParty,
  type ContractAcceptanceRecord,
  type ContractAcceptanceRecordSummary,
  type ContractAcceptanceTemplateOption,
} from "@/services/contract-acceptance-api";

interface Props {
  contract: ContractDetailResponse;
  paymentMilestones: ContractPaymentMilestoneResponse[];
  signedActive: boolean;
  canManage: boolean;
  onChanged: () => void | Promise<void>;
}

const STATUS: Record<AcceptanceRecordStatus, string> = {
  [AcceptanceRecordStatus.Draft]: "Bản nháp",
  [AcceptanceRecordStatus.Finalized]: "Đã chốt, chờ ký",
  [AcceptanceRecordStatus.Signed]: "Đã ký",
  [AcceptanceRecordStatus.Cancelled]: "Đã hủy",
};

const emptyCreate = () => ({
  templateVersionId: 0,
  acceptanceCode: "",
  acceptanceDate: format(new Date(), "yyyy-MM-dd"),
  location: "",
  acceptanceKind: AcceptanceKind.Final,
  appendixIds: [] as number[],
  paymentMilestoneIds: [] as number[],
});

const parseDateString = (str?: string) => {
  if (!str) return undefined;
  const [y, m, d] = str.slice(0, 10).split("-").map(Number);
  if (!y || !m || !d) return undefined;
  return new Date(y, m - 1, d);
};

const partyInput = (party: ContractAcceptanceParty) => ({
  legalName: party.legalName,
  address: party.address,
  phone: party.phone,
  fax: party.fax,
  taxCode: party.taxCode,
  representativeName: party.representativeName,
  representativeTitle: party.representativeTitle,
});

export function ContractAcceptanceRecords({
  contract,
  paymentMilestones,
  signedActive,
  canManage,
  onChanged,
}: Props) {
  const [records, setRecords] = useState<ContractAcceptanceRecordSummary[]>([]);
  const [templates, setTemplates] = useState<
    ContractAcceptanceTemplateOption[]
  >([]);
  const [selected, setSelected] = useState<ContractAcceptanceRecord>();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [createOpen, setCreateOpen] = useState(false);
  const [createStep, setCreateStep] = useState(1);
  const [createForm, setCreateForm] = useState(emptyCreate);
  const [finalizeOpen, setFinalizeOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [signedFile, setSignedFile] = useState<File>();
  const [recordPage, setRecordPage] = useState(1);
  const [recordPageSize, setRecordPageSize] = useState(3);

  const totalRecordPages = Math.ceil(records.length / recordPageSize) || 1;

  const paginatedRecords = useMemo(() => {
    const start = (recordPage - 1) * recordPageSize;
    return records.slice(start, start + recordPageSize);
  }, [records, recordPage, recordPageSize]);

  useEffect(() => {
    if (recordPage > totalRecordPages) {
      setRecordPage(totalRecordPages);
    }
  }, [recordPage, totalRecordPages]);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      const [nextRecords, nextTemplates] = await Promise.all([
        contractAcceptanceApi.list(contract.contractId),
        contractAcceptanceApi.templates(contract.contractId),
      ]);
      setRecords(nextRecords);
      setTemplates(nextTemplates);
    } catch (error) {
      toast.error(
        getApiErrorMessage(error, "Không thể tải biên bản nghiệm thu."),
      );
    } finally {
      setLoading(false);
    }
  }, [contract.contractId]);

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeout);
  }, [load]);

  const appendices = useMemo(
    () =>
      [...(contract.currentVersion.appendices ?? [])].sort(
        (a, b) => a.displayOrder - b.displayOrder,
      ),
    [contract.currentVersion.appendices],
  );

  const commit = async (
    action: () => Promise<ContractAcceptanceRecord>,
    message: string,
    refreshClosing = false,
  ) => {
    try {
      setBusy(true);
      const next = await action();
      setSelected(next);
      setRecordPage(1);
      setRecords((current) => {
        const summary: ContractAcceptanceRecordSummary = next;
        const remaining = current.filter(
          (item) => item.acceptanceRecordId !== next.acceptanceRecordId,
        );
        return [summary, ...remaining];
      });
      toast.success(message);
      if (refreshClosing) await onChanged();
      return next;
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Thao tác không thành công."));
    } finally {
      setBusy(false);
    }
  };

  const openRecord = async (recordId: number) => {
    try {
      setBusy(true);
      setSelected(
        await contractAcceptanceApi.get(contract.contractId, recordId),
      );
    } catch (error) {
      toast.error(getApiErrorMessage(error, "Không thể mở biên bản."));
    } finally {
      setBusy(false);
    }
  };

  const startCreate = () => {
    const next = emptyCreate();
    if (templates.length === 1)
      next.templateVersionId = templates[0].templateVersionId;
    setCreateForm(next);
    setCreateStep(1);
    setCreateOpen(true);
  };

  const createRecord = async () => {
    const result = await commit(
      () =>
        contractAcceptanceApi.create(contract.contractId, {
          contractVersionId: contract.currentVersion.versionId,
          templateVersionId: createForm.templateVersionId,
          acceptanceCode: createForm.acceptanceCode.trim(),
          acceptanceDate: createForm.acceptanceDate,
          location: createForm.location.trim(),
          acceptanceKind: createForm.acceptanceKind,
          appendixIds: createForm.appendixIds,
          paymentMilestoneIds:
            createForm.acceptanceKind === AcceptanceKind.Partial
              ? createForm.paymentMilestoneIds
              : [],
          contractRowVersion: contract.rowVersion,
          versionRowVersion: contract.currentVersion.rowVersion,
        }),
      "Đã tạo bản nháp biên bản nghiệm thu.",
    );
    if (result) setCreateOpen(false);
  };

  const updateParty = (
    role: AcceptancePartyRole,
    key: keyof ContractAcceptanceParty,
    value: string,
  ) => {
    setSelected((current) =>
      current
        ? {
            ...current,
            parties: current.parties.map((party) =>
              party.partyRole === role ? { ...party, [key]: value } : party,
            ),
          }
        : current,
    );
  };

  const saveAllDraftChanges = async (
    quiet = false,
  ): Promise<ContractAcceptanceRecord | null> => {
    if (!selected || selected.status !== AcceptanceRecordStatus.Draft)
      return null;
    const draft = selected;
    const customer = draft.parties.find(
      (party) => party.partyRole === AcceptancePartyRole.Customer,
    );
    const tenant = draft.parties.find(
      (party) => party.partyRole === AcceptancePartyRole.Tenant,
    );
    if (!customer || !tenant) return null;
    let latest = draft;
    try {
      setBusy(true);
      latest = await contractAcceptanceApi.update(
        contract.contractId,
        draft.acceptanceRecordId,
        {
          acceptanceCode: draft.acceptanceCode,
          acceptanceDate: draft.acceptanceDate.slice(0, 10),
          location: draft.location,
          acceptanceKind: draft.acceptanceKind,
          customerParty: partyInput(customer),
          tenantParty: partyInput(tenant),
          rowVersion: latest.rowVersion,
        },
      );
      latest = await contractAcceptanceApi.replaceReferences(
        contract.contractId,
        draft.acceptanceRecordId,
        draft.references
          .filter(
            (item) => item.referenceType === AcceptanceReferenceType.Appendix,
          )
          .flatMap((item) => (item.appendixId ? [item.appendixId] : [])),
        latest.rowVersion,
      );
      latest = await contractAcceptanceApi.replaceMilestones(
        contract.contractId,
        draft.acceptanceRecordId,
        draft.acceptanceKind === AcceptanceKind.Partial
          ? draft.milestones.map((item) => item.paymentMilestoneId)
          : [],
        latest.rowVersion,
      );
      latest = await contractAcceptanceApi.replaceSections(
        contract.contractId,
        draft.acceptanceRecordId,
        draft.sections.map((section) => ({
          sectionCode: section.sectionCode,
          titleVi: section.titleVi,
          titleEn: section.titleEn,
          contentVi: section.contentVi,
          contentEn: section.contentEn,
          displayOrder: section.displayOrder,
        })),
        latest.rowVersion,
      );
      setSelected(latest);
      setRecords((current) => {
        const summary: ContractAcceptanceRecordSummary = latest;
        return [
          summary,
          ...current.filter(
            (item) => item.acceptanceRecordId !== latest.acceptanceRecordId,
          ),
        ];
      });
      if (!quiet) {
        toast.success("Đã lưu toàn bộ thay đổi của biên bản.");
      }
      return latest;
    } catch (error) {
      setSelected((current) =>
        current ? { ...current, rowVersion: latest.rowVersion } : current,
      );
      toast.error(
        getApiErrorMessage(error, "Không thể lưu toàn bộ thay đổi biên bản."),
      );
      return null;
    } finally {
      setBusy(false);
    }
  };

  const toggleReference = (appendixId: number, checked: boolean) => {
    if (!selected) return;
    const existing = selected.references.find(
      (item) => item.appendixId === appendixId,
    );
    setSelected({
      ...selected,
      references: checked
        ? existing
          ? selected.references
          : [
              ...selected.references,
              {
                acceptanceReferenceId: -appendixId,
                referenceType: AcceptanceReferenceType.Appendix,
                appendixId,
                textVi: "Sẽ được backend sinh lại sau khi lưu.",
                displayOrder: selected.references.length,
              },
            ]
        : selected.references.filter((item) => item.appendixId !== appendixId),
    });
  };

  const toggleMilestone = (milestoneId: number, checked: boolean) => {
    if (!selected) return;
    const source = paymentMilestones.find(
      (item) => item.paymentMilestoneId === milestoneId,
    );
    if (!source) return;
    setSelected({
      ...selected,
      milestones: checked
        ? selected.milestones.some(
            (item) => item.paymentMilestoneId === milestoneId,
          )
          ? selected.milestones
          : [
              ...selected.milestones,
              {
                acceptanceMilestoneId: -milestoneId,
                paymentMilestoneId: milestoneId,
                milestoneCode: source.milestoneCode,
                titleVi: source.titleVi,
                titleEn: source.titleEn,
              },
            ]
        : selected.milestones.filter(
            (item) => item.paymentMilestoneId !== milestoneId,
          ),
    });
  };

  const openDocument = async (format: "docx" | "pdf", official: boolean) => {
    if (!selected) return;
    const previewWindow =
      format === "pdf" ? window.open("about:blank", "_blank") : null;
    try {
      setBusy(true);
      const blob = official
        ? await contractAcceptanceApi.artifact(
            contract.contractId,
            selected.acceptanceRecordId,
            format,
          )
        : await contractAcceptanceApi.preview(
            contract.contractId,
            selected.acceptanceRecordId,
            format,
          );
      const source = blob instanceof Blob ? blob : new Blob([blob]);
      const objectUrl = URL.createObjectURL(source);
      if (previewWindow) previewWindow.location.replace(objectUrl);
      else {
        const anchor = document.createElement("a");
        anchor.href = objectUrl;
        anchor.download = `${selected.acceptanceCode}.${format}`;
        anchor.click();
      }
      window.setTimeout(() => URL.revokeObjectURL(objectUrl), 60_000);
    } catch (error) {
      previewWindow?.close();
      toast.error(getApiErrorMessage(error, "Không thể mở tài liệu."));
    } finally {
      setBusy(false);
    }
  };

  if (loading)
    return (
      <Card>
        <CardContent className="flex min-h-40 items-center justify-center">
          <Loader2 className="size-6 animate-spin" />
        </CardContent>
      </Card>
    );

  const isDraft = selected?.status === AcceptanceRecordStatus.Draft;
  const currentVersionRecords = records.filter(
    (record) => record.contractVersionId === contract.currentVersion.versionId,
  );
  const activeRecord = currentVersionRecords.find(
    (record) =>
      record.status === AcceptanceRecordStatus.Draft ||
      record.status === AcceptanceRecordStatus.Finalized,
  );
  const signedFinalRecord = currentVersionRecords.find(
    (record) =>
      record.status === AcceptanceRecordStatus.Signed &&
      record.acceptanceKind === AcceptanceKind.Final,
  );
  const unavailableMilestoneIds = new Set(
    currentVersionRecords
      .filter(
        (record) =>
          record.status === AcceptanceRecordStatus.Finalized ||
          record.status === AcceptanceRecordStatus.Signed,
      )
      .flatMap((record) => record.paymentMilestoneIds),
  );
  const availablePaymentMilestones = paymentMilestones.filter(
    (milestone) => !unavailableMilestoneIds.has(milestone.paymentMilestoneId),
  );
  const canCreate =
    canManage &&
    contract.status === ContractStatus.Signed &&
    signedActive &&
    !activeRecord &&
    !signedFinalRecord;
  const customer = selected?.parties.find(
    (party) => party.partyRole === AcceptancePartyRole.Customer,
  );
  const tenant = selected?.parties.find(
    (party) => party.partyRole === AcceptancePartyRole.Tenant,
  );

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-3">
        <CardTitle className="flex items-center gap-2">
          <FileCheck2 className="size-5" />
          Biên bản nghiệm thu
        </CardTitle>
        {canCreate && (
          <Button
            size="sm"
            onClick={startCreate}
            disabled={busy || templates.length === 0}
          >
            <Plus className="mr-2 size-4" /> Tạo biên bản
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-5">
        {canManage &&
          contract.status === ContractStatus.Signed &&
          !signedActive && (
            <Alert>
              <AlertDescription>
                Hợp đồng chưa có bản ký active nên chưa thể tạo biên bản nghiệm
                thu.
              </AlertDescription>
            </Alert>
          )}
        {canManage && signedActive && activeRecord && (
          <Alert>
            <AlertDescription className="flex flex-wrap items-center justify-between gap-2">
              <span>
                Đang có biên bản {STATUS[activeRecord.status].toLowerCase()}.
                Hãy hoàn tất hoặc hủy biên bản này trước khi tạo mới.
              </span>
              <Button
                type="button"
                size="sm"
                variant="outline"
                onClick={() => void openRecord(activeRecord.acceptanceRecordId)}
                disabled={busy}
              >
                Mở biên bản
              </Button>
            </AlertDescription>
          </Alert>
        )}
        {canManage && signedActive && !activeRecord && signedFinalRecord && (
          <Alert>
            <AlertDescription>
              Hợp đồng đã có biên bản nghiệm thu toàn bộ đã ký, không thể tạo
              thêm biên bản nghiệm thu.
            </AlertDescription>
          </Alert>
        )}
        {templates.length === 0 && canCreate && (
          <Alert>
            <AlertDescription>
              Chưa có mẫu biên bản nghiệm thu đã phát hành. Hãy phát hành mẫu
              trước khi tạo biên bản.
            </AlertDescription>
          </Alert>
        )}
        {records.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Chưa tạo biên bản nghiệm thu.
          </p>
        ) : (
          <div className="space-y-3">
            <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
              {paginatedRecords.map((record) => (
                <button
                  type="button"
                  key={record.acceptanceRecordId}
                  onClick={() => void openRecord(record.acceptanceRecordId)}
                  className={`rounded-lg border p-3 text-left transition-colors hover:bg-muted ${
                    selected?.acceptanceRecordId === record.acceptanceRecordId
                      ? "border-primary bg-muted"
                      : ""
                  }`}
                >
                  <div className="flex items-start justify-between gap-2">
                    <b>{record.acceptanceCode}</b>
                    <Badge
                      variant={
                        record.status === AcceptanceRecordStatus.Cancelled
                          ? "destructive"
                          : "secondary"
                      }
                    >
                      {STATUS[record.status]}
                    </Badge>
                  </div>
                  <p className="mt-1 text-xs text-muted-foreground">
                    {new Date(record.acceptanceDate).toLocaleDateString(
                      "vi-VN",
                    )}{" "}
                    · Phiên bản {record.versionNo}
                  </p>
                </button>
              ))}
            </div>

            {records.length > 0 && (
              <div className="flex flex-col gap-2 border-t pt-3 sm:flex-row sm:items-center sm:justify-between">
                <p className="text-xs text-muted-foreground">
                  {records.length} biên bản · Trang {recordPage}/
                  {totalRecordPages}
                </p>
                <div className="flex items-center gap-2">
                  <Select
                    value={String(recordPageSize)}
                    onValueChange={(val) => {
                      setRecordPageSize(Number(val));
                      setRecordPage(1);
                    }}
                  >
                    <SelectTrigger className="h-8 w-[100px] text-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent showSearch={false}>
                      {[3, 6, 9, 12].map((size) => (
                        <SelectItem key={size} value={String(size)}>
                          {size} / trang
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                  <div className="flex items-center gap-1">
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-8 px-2.5 text-xs gap-1"
                      disabled={recordPage <= 1}
                      onClick={() => setRecordPage((p) => Math.max(p - 1, 1))}
                    >
                      <ChevronLeft className="size-3.5" />
                      <span>Trước</span>
                    </Button>
                    <Button
                      type="button"
                      variant="outline"
                      size="sm"
                      className="h-8 px-2.5 text-xs gap-1"
                      disabled={recordPage >= totalRecordPages}
                      onClick={() =>
                        setRecordPage((p) => Math.min(p + 1, totalRecordPages))
                      }
                    >
                      <span>Sau</span>
                      <ChevronRight className="size-3.5" />
                    </Button>
                  </div>
                </div>
              </div>
            )}
          </div>
        )}

        {selected && (
          <div className="space-y-6 rounded-lg border p-4">
            <div className="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 className="font-semibold">{selected.acceptanceCode}</h3>
                <p className="text-sm text-muted-foreground">
                  {selected.templateName} · {STATUS[selected.status]}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={busy}
                  onClick={() => void openDocument("pdf", !isDraft)}
                >
                  <Eye className="mr-2 size-4" /> PDF
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={busy}
                  onClick={() => void openDocument("docx", !isDraft)}
                >
                  <Download className="mr-2 size-4" /> DOCX
                </Button>
              </div>
            </div>

            {isDraft ? (
              <div className="space-y-6">
                <div className="sticky top-2 z-20 flex flex-col gap-3 rounded-xl border border-primary/30 bg-background/95 p-3 shadow-md backdrop-blur sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold">
                      Chỉnh sửa bản nháp nghiệm thu
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Một lần lưu áp dụng cho thông tin, căn cứ, phạm vi và toàn
                      bộ nội dung biên bản.
                    </p>
                  </div>
                  <Button
                    type="button"
                    onClick={() => void saveAllDraftChanges()}
                    disabled={busy}
                    className="shrink-0 gap-2"
                  >
                    {busy ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Save className="size-4" />
                    )}
                    Lưu tất cả thay đổi
                  </Button>
                </div>

                {/* 1. Thông tin và người tham gia */}
                <Card className="border-border/80 shadow-sm overflow-hidden gap-0 py-2">
                  <CardHeader className="flex flex-row items-center justify-between border-b px-4 py-3 sm:px-5">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <FileText className="size-4" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Thông tin & Người tham gia
                        </CardTitle>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Mã biên bản, ngày nghiệm thu, địa điểm và thông tin
                          đại diện hai bên
                        </p>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="p-4 sm:p-5 space-y-4">
                    <div className="grid gap-3.5 sm:grid-cols-2 p-3.5 rounded-lg border border-border/60">
                      <Field
                        label="Mã biên bản"
                        value={selected.acceptanceCode}
                        onChange={(value) =>
                          setSelected({ ...selected, acceptanceCode: value })
                        }
                      />
                      <div className="space-y-2">
                        <Label htmlFor="acceptance-date-edit">
                          Ngày nghiệm thu
                        </Label>
                        <DateFilter
                          id="acceptance-date-edit"
                          date={parseDateString(selected.acceptanceDate)}
                          onChange={(date) =>
                            setSelected({
                              ...selected,
                              acceptanceDate: date
                                ? format(date, "yyyy-MM-dd")
                                : "",
                            })
                          }
                          placeholder="Chọn ngày nghiệm thu"
                          className="flex-1 shadow-none"
                        />
                      </div>
                      <div className="col-span-2">
                        <Field
                          label="Địa điểm"
                          value={selected.location}
                          onChange={(value) =>
                            setSelected({ ...selected, location: value })
                          }
                        />
                      </div>
                    </div>

                    <div className="grid gap-4 md:grid-cols-2 pt-1">
                      {customer && (
                        <PartyEditor
                          title="Bên A — Khách hàng"
                          party={customer}
                          representativeLabel="Người liên lạc"
                          onChange={(key, value) =>
                            updateParty(
                              AcceptancePartyRole.Customer,
                              key,
                              value,
                            )
                          }
                        />
                      )}
                      {tenant && (
                        <PartyEditor
                          title="Bên B — Doanh nghiệp"
                          party={tenant}
                          onChange={(key, value) =>
                            updateParty(AcceptancePartyRole.Tenant, key, value)
                          }
                        />
                      )}
                    </div>
                  </CardContent>
                </Card>

                {/* 2. Căn cứ tự sinh */}
                <Card className="border-border/80 shadow-sm overflow-hidden gap-0 py-2">
                  <CardHeader className="flex flex-row items-center justify-between border-b px-4 py-3 sm:px-5">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <ScrollText className="size-4" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Căn cứ nghiệm thu
                        </CardTitle>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Hợp đồng gốc và các phụ lục hợp đồng được sử dụng làm
                          căn cứ pháp lý
                        </p>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="p-4 sm:p-5 space-y-2.5">
                    {/* Hợp đồng gốc */}
                    <div className="flex items-start gap-3 rounded-lg border border-primary/20 bg-primary/5 p-3 text-sm">
                      <Checkbox checked disabled className="mt-0.5" />
                      <div className="min-w-0 flex-1 flex flex-wrap items-center justify-between gap-2">
                        <span className="font-medium text-foreground">
                          {selected.references.find(
                            (item) =>
                              item.referenceType ===
                              AcceptanceReferenceType.Contract,
                          )?.textVi ?? `Hợp đồng ${contract.contractCode}`}
                        </span>
                        <Badge
                          variant="secondary"
                          className="text-xs font-normal shrink-0"
                        >
                          Bắt buộc
                        </Badge>
                      </div>
                    </div>

                    {/* Phụ lục */}
                    {appendices.length > 0 && (
                      <div className="space-y-2 pt-1">
                        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                          Phụ lục hợp đồng
                        </p>
                        <div className="space-y-2">
                          {appendices.map((appendix) => {
                            const reference = selected.references.find(
                              (item) => item.appendixId === appendix.appendixId,
                            );
                            const isChecked = Boolean(reference);
                            return (
                              <label
                                key={appendix.appendixId}
                                className={cn(
                                  "flex items-start gap-3 rounded-lg border p-3 text-sm transition-colors cursor-pointer select-none",
                                  isChecked
                                    ? "border-primary/40 bg-card shadow-xs"
                                    : "border-border/60 bg-muted/20 hover:bg-muted/40 text-muted-foreground",
                                )}
                              >
                                <Checkbox
                                  checked={isChecked}
                                  onCheckedChange={(value) =>
                                    toggleReference(
                                      appendix.appendixId,
                                      value === true,
                                    )
                                  }
                                  className="mt-0.5"
                                />
                                <div className="min-w-0 flex-1 flex flex-wrap items-center gap-2">
                                  <span className="font-mono text-xs font-medium bg-muted px-1.5 py-0.5 rounded border border-border/50 text-foreground">
                                    {appendix.appendixCode}
                                  </span>
                                  <span className="text-foreground font-medium">
                                    {appendix.appendixName}
                                  </span>
                                  {reference?.textVi && (
                                    <span className="text-xs text-muted-foreground italic w-full block mt-0.5">
                                      {reference.textVi}
                                    </span>
                                  )}
                                </div>
                              </label>
                            );
                          })}
                        </div>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* 3. Phạm vi nghiệm thu */}
                <Card className="border-border/80 shadow-sm overflow-hidden gap-0 py-2">
                  <CardHeader className="flex flex-row items-center justify-between border-b px-4 py-3 sm:px-5">
                    <div className="flex items-center gap-2.5">
                      <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <Layers className="size-4" />
                      </div>
                      <div>
                        <CardTitle className="text-base font-semibold">
                          Phạm vi nghiệm thu
                        </CardTitle>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Nghiệm thu toàn bộ hoặc chọn từng đợt thanh toán cụ
                          thể
                        </p>
                      </div>
                    </div>
                  </CardHeader>
                  <CardContent className="p-4 sm:p-5 space-y-4">
                    <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 bg-muted/30 p-3.5 rounded-lg border border-border/60">
                      <div>
                        <span className="text-sm font-medium text-foreground block">
                          Chế độ nghiệm thu
                        </span>
                        <span className="text-xs text-muted-foreground">
                          Chọn phạm vi hoàn thành của hợp đồng
                        </span>
                      </div>
                      <Select
                        value={String(selected.acceptanceKind)}
                        onValueChange={(val) =>
                          setSelected({
                            ...selected,
                            acceptanceKind: Number(val) as AcceptanceKind,
                            milestones: [],
                          })
                        }
                      >
                        <SelectTrigger className="w-full sm:w-[220px] bg-background">
                          <SelectValue />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value={String(AcceptanceKind.Final)}>
                            Nghiệm thu toàn bộ
                          </SelectItem>
                          <SelectItem value={String(AcceptanceKind.Partial)}>
                            Nghiệm thu một phần
                          </SelectItem>
                        </SelectContent>
                      </Select>
                    </div>

                    {selected.acceptanceKind === AcceptanceKind.Final ? (
                      <div className="flex items-center gap-2.5 rounded-lg border border-emerald-500/20 bg-emerald-500/5 p-3.5 text-sm text-emerald-800 dark:text-emerald-300">
                        <CheckCircle2 className="size-4 shrink-0 text-emerald-600 dark:text-emerald-400" />
                        <span>
                          Biên bản này áp dụng nghiệm thu toàn bộ cho tất cả các
                          hạng mục và đợt thanh toán của hợp đồng.
                        </span>
                      </div>
                    ) : (
                      <div className="space-y-2 pt-1">
                        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
                          Chọn các đợt thanh toán nghiệm thu
                        </p>
                        {paymentMilestones.length === 0 ? (
                          <p className="text-sm text-muted-foreground italic py-2">
                            Không có đợt thanh toán nào trong hợp đồng này.
                          </p>
                        ) : (
                          <div className="space-y-2">
                            {paymentMilestones.map((milestone) => {
                              const isChecked = selected.milestones.some(
                                (item) =>
                                  item.paymentMilestoneId ===
                                  milestone.paymentMilestoneId,
                              );
                              return (
                                <label
                                  key={milestone.paymentMilestoneId}
                                  className={cn(
                                    "flex items-center justify-between gap-3 rounded-lg border p-3 text-sm transition-colors cursor-pointer select-none",
                                    isChecked
                                      ? "border-primary/40 bg-card shadow-xs"
                                      : "border-border/60 bg-muted/20 hover:bg-muted/40 text-muted-foreground",
                                  )}
                                >
                                  <div className="flex items-center gap-3 min-w-0">
                                    <Checkbox
                                      checked={isChecked}
                                      onCheckedChange={(value) =>
                                        toggleMilestone(
                                          milestone.paymentMilestoneId,
                                          value === true,
                                        )
                                      }
                                    />
                                    <span className="font-mono text-xs font-medium bg-muted px-1.5 py-0.5 rounded border border-border/50 text-foreground">
                                      {milestone.milestoneCode}
                                    </span>
                                    <span className="font-medium text-foreground truncate">
                                      {milestone.titleVi}
                                    </span>
                                  </div>
                                  <Badge
                                    variant="outline"
                                    className="shrink-0 text-xs"
                                  >
                                    {milestone.paymentPercent}%
                                  </Badge>
                                </label>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* 4. Nội dung biên bản */}
                <Collapsible
                  defaultOpen
                  className="rounded-xl border border-border/80 bg-card/60 shadow-sm overflow-hidden"
                >
                  <div className="flex items-center justify-between border-b px-4 py-3 sm:px-5">
                    <div className="flex items-center gap-2.5 py-2">
                      <div className="flex size-8 items-center justify-center rounded-lg bg-primary/10 text-primary">
                        <BookmarkCheck className="size-4" />
                      </div>
                      <div>
                        <div className="flex items-center gap-2">
                          <h4 className="text-base font-semibold text-foreground">
                            Nội dung biên bản
                          </h4>
                          <Badge variant="secondary" className="text-xs">
                            {selected.sections.length} mục nghiệm thu
                          </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground mt-0.5">
                          Từng điều khoản và nội dung chi tiết của biên bản
                          nghiệm thu
                        </p>
                      </div>
                    </div>
                    <div className="flex items-center gap-2">
                      <CollapsibleTrigger asChild>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="size-8 group"
                          aria-label="Thu gọn hoặc mở rộng nội dung biên bản"
                        >
                          <ChevronDown className="size-4 transition-transform group-data-[state=open]:rotate-180" />
                        </Button>
                      </CollapsibleTrigger>
                    </div>
                  </div>
                  <CollapsibleContent className="p-4 sm:p-5 space-y-4">
                    {selected.sections.map((section, index) => {
                      const updateSection = (
                        field: ContractTermEditableField,
                        value: string | boolean,
                      ) => {
                        if (typeof value !== "string") return;
                        const sectionField =
                          field === "termTitle"
                            ? "titleVi"
                            : field === "termTitleEn"
                              ? "titleEn"
                              : field === "termContent"
                                ? "contentVi"
                                : field === "termContentEn"
                                  ? "contentEn"
                                  : null;
                        if (!sectionField) return;
                        setSelected({
                          ...selected,
                          sections: selected.sections.map((item, itemIndex) =>
                            itemIndex === index
                              ? { ...item, [sectionField]: value }
                              : item,
                          ),
                        });
                      };

                      return (
                        <ContractTermCard
                          key={section.acceptanceSectionId}
                          term={{
                            termCode: section.sectionCode,
                            termTitle: section.titleVi,
                            termTitleEn: section.titleEn,
                            termContent: section.contentVi,
                            termContentEn: section.contentEn,
                            displayOrder: section.displayOrder,
                            isNegotiable: false,
                          }}
                          inputId={`acceptance-${selected.acceptanceRecordId}-${section.acceptanceSectionId}`}
                          editable
                          isBilingual={
                            section.titleEn != null || section.contentEn != null
                          }
                          orderLabel="Mục nghiệm thu"
                          titleLabel="Tiêu đề mục"
                          contentLabel="Nội dung mục"
                          showNegotiation={false}
                          onChange={updateSection}
                        />
                      );
                    })}
                  </CollapsibleContent>
                </Collapsible>

                {/* 5. Khối hành động: Hoàn tất & Chốt biên bản */}
                {canManage && (
                  <Card className="border-emerald-200 bg-gradient-to-r from-emerald-50/70 via-background to-emerald-50/30 p-5 shadow-sm dark:border-emerald-900/50 dark:from-emerald-950/20 dark:via-background dark:to-emerald-950/10">
                    <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                      <div className="flex items-start gap-3.5">
                        <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                          <CheckCircle2 className="size-5" />
                        </div>
                        <div className="space-y-1">
                          <div className="flex items-center gap-2">
                            <h4 className="font-semibold text-foreground">
                              Hoàn tất & Chốt biên bản nghiệm thu
                            </h4>
                          </div>
                          <p className="text-xs text-muted-foreground leading-relaxed max-w-xl">
                            Vui lòng kiểm tra kỹ thông tin các bên, căn cứ và
                            các mục nghiệm thu. Khi chốt, hệ thống sẽ tự động
                            lưu lại toàn bộ thay đổi mới nhất, xuất file chính
                            thức và khóa nội dung để chuẩn bị ký kết.
                          </p>
                        </div>
                      </div>

                      <div className="flex items-center gap-2.5 sm:shrink-0">
                        <Button
                          type="button"
                          disabled={busy}
                          onClick={() => setFinalizeOpen(true)}
                          className="bg-emerald-600 hover:bg-emerald-700 text-white font-medium shadow-sm gap-2"
                        >
                          {busy ? (
                            <Loader2 className="size-4 animate-spin" />
                          ) : (
                            <FileSignature className="size-4" />
                          )}
                          Chốt biên bản
                        </Button>
                      </div>
                    </div>
                  </Card>
                )}
              </div>
            ) : (
              <div className="space-y-4 text-sm">
                <div className="grid gap-3 md:grid-cols-2">
                  <div>
                    <span className="text-muted-foreground">
                      Ngày nghiệm thu:
                    </span>{" "}
                    {new Date(selected.acceptanceDate).toLocaleDateString(
                      "vi-VN",
                    )}
                  </div>
                  <div>
                    <span className="text-muted-foreground">Địa điểm:</span>{" "}
                    {selected.location}
                  </div>
                  {selected.finalizedAt && (
                    <>
                      <div>
                        <span className="text-muted-foreground">
                          Ngày chốt:
                        </span>{" "}
                        {new Date(selected.finalizedAt).toLocaleString("vi-VN")}
                      </div>
                      <div>
                        <span className="text-muted-foreground">Chốt bởi:</span>{" "}
                        {selected.finalizedByEmployeeName ?? "—"}
                      </div>
                    </>
                  )}
                </div>
                <div className="space-y-2">
                  {selected.references.map((reference) => (
                    <p key={reference.acceptanceReferenceId} className="italic">
                      {reference.textVi}
                    </p>
                  ))}
                </div>
                <Collapsible className="space-y-3">
                  <Card>
                    <div className="flex flex-row items-center justify-between gap-3 px-4">
                      <div className="flex flex-col gap-2">
                        <p className="font-medium">Nội dung biên bản</p>
                        <p className="text-xs text-muted-foreground">
                          {selected.sections.length} mục nghiệm thu
                        </p>
                      </div>
                      <CollapsibleTrigger asChild>
                        <Button
                          type="button"
                          variant="ghost"
                          size="icon"
                          className="group"
                          aria-label="Thu gọn hoặc mở rộng nội dung biên bản"
                        >
                          <ChevronDown className="size-4 transition-transform group-data-[state=open]:rotate-180" />
                        </Button>
                      </CollapsibleTrigger>
                    </div>
                    <CollapsibleContent className="space-y-3 px-3">
                      {selected.sections.map((section) => (
                        <ContractTermCard
                          key={section.acceptanceSectionId}
                          term={{
                            termCode: section.sectionCode,
                            termTitle: section.titleVi,
                            termTitleEn: section.titleEn,
                            termContent: section.contentVi,
                            termContentEn: section.contentEn,
                            displayOrder: section.displayOrder,
                            isNegotiable: false,
                          }}
                          inputId={`acceptance-readonly-${selected.acceptanceRecordId}-${section.acceptanceSectionId}`}
                          isBilingual={Boolean(
                            section.titleEn || section.contentEn,
                          )}
                          orderLabel="Mục nghiệm thu"
                          showNegotiation={false}
                        />
                      ))}
                    </CollapsibleContent>
                  </Card>
                </Collapsible>
              </div>
            )}

            {selected.status === AcceptanceRecordStatus.Finalized &&
              canManage && (
                <div className="space-y-3 rounded-md border border-amber-300 bg-amber-50 p-4 dark:bg-amber-950/20">
                  <b>Biên bản đã chốt và đang chờ bản ký</b>
                  <div className="flex flex-col gap-2 sm:flex-row">
                    <Input
                      type="file"
                      accept=".pdf,.jpg,.jpeg,.png"
                      onChange={(event) =>
                        setSignedFile(event.target.files?.[0])
                      }
                      className="bg-background"
                    />
                    <Button
                      disabled={!signedFile || busy}
                      onClick={() =>
                        signedFile &&
                        void (async () => {
                          try {
                            setBusy(true);
                            await contractAcceptanceApi.uploadSignedEvidence(
                              contract.contractId,
                              selected.acceptanceRecordId,
                              signedFile,
                              selected.rowVersion,
                            );
                            toast.success("Đã lưu bản ký biên bản nghiệm thu.");
                            setSignedFile(undefined);
                            setSelected(
                              await contractAcceptanceApi.get(
                                contract.contractId,
                                selected.acceptanceRecordId,
                              ),
                            );
                            setRecords(
                              await contractAcceptanceApi.list(
                                contract.contractId,
                              ),
                            );
                            await onChanged();
                          } catch (error) {
                            toast.error(
                              getApiErrorMessage(
                                error,
                                "Không thể tải bản ký.",
                              ),
                            );
                          } finally {
                            setBusy(false);
                          }
                        })()
                      }
                    >
                      <FileSignature className="mr-2 size-4" /> Tải bản ký
                    </Button>
                  </div>
                </div>
              )}
            {selected.status === AcceptanceRecordStatus.Signed &&
              selected.signedEvidence && (
                <Alert>
                  <CheckCircle2 className="size-4" />
                  <AlertDescription>
                    Đã ký: {selected.signedEvidence.fileName} ·{" "}
                    {new Date(
                      selected.signedEvidence.uploadedAt,
                    ).toLocaleString("vi-VN")}
                  </AlertDescription>
                </Alert>
              )}
            {selected.status === AcceptanceRecordStatus.Cancelled && (
              <Alert variant="destructive">
                <XCircle className="size-4" />
                <AlertDescription>
                  Lý do: {selected.cancellationReason}
                </AlertDescription>
              </Alert>
            )}
            {canManage &&
              (selected.status === AcceptanceRecordStatus.Draft ||
                selected.status === AcceptanceRecordStatus.Finalized) && (
                <Button
                  variant="destructive"
                  onClick={() => setCancelOpen(true)}
                  disabled={busy}
                >
                  Hủy biên bản
                </Button>
              )}
          </div>
        )}
      </CardContent>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>
              Tạo biên bản nghiệm thu · Bước {createStep}/3
            </DialogTitle>
            <DialogDescription>
              Hợp đồng gốc luôn là căn cứ bắt buộc; nội dung căn cứ do backend
              tự sinh.
            </DialogDescription>
          </DialogHeader>
          {createStep === 1 && (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label>Mẫu biên bản nghiệm thu</Label>
                <Select
                  value={
                    createForm.templateVersionId
                      ? String(createForm.templateVersionId)
                      : ""
                  }
                  onValueChange={(value) =>
                    setCreateForm({
                      ...createForm,
                      templateVersionId: Number(value) || 0,
                    })
                  }
                >
                  <SelectTrigger className="w-full">
                    <SelectValue placeholder="Chọn mẫu" />
                  </SelectTrigger>
                  <SelectContent>
                    {templates.map((template) => (
                      <SelectItem
                        key={template.templateVersionId}
                        value={String(template.templateVersionId)}
                      >
                        {template.templateName} · Phiên bản {template.versionNo}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <Field
                label="Mã biên bản"
                value={createForm.acceptanceCode}
                onChange={(value) =>
                  setCreateForm({ ...createForm, acceptanceCode: value })
                }
              />
              <div className="space-y-2">
                <Label htmlFor="acceptance-date">Ngày nghiệm thu</Label>
                <DateFilter
                  id="acceptance-date"
                  date={parseDateString(createForm.acceptanceDate)}
                  onChange={(date) =>
                    setCreateForm({
                      ...createForm,
                      acceptanceDate: date ? format(date, "yyyy-MM-dd") : "",
                    })
                  }
                  placeholder="Chọn ngày nghiệm thu"
                  className="flex-1"
                />
              </div>
              <div className="md:col-span-2">
                <Field
                  label="Địa điểm"
                  value={createForm.location}
                  onChange={(value) =>
                    setCreateForm({ ...createForm, location: value })
                  }
                />
              </div>
            </div>
          )}
          {createStep === 2 && (
            <div className="space-y-4">
              <label className="flex items-center gap-3 rounded-md border bg-muted/50 p-3 text-sm">
                <Checkbox checked disabled /> Hợp đồng {contract.contractCode} —
                căn cứ bắt buộc
              </label>
              {appendices.map((appendix) => (
                <label
                  key={appendix.appendixId}
                  className="flex items-center gap-3 rounded-md border p-3 text-sm"
                >
                  <Checkbox
                    checked={createForm.appendixIds.includes(
                      appendix.appendixId,
                    )}
                    onCheckedChange={(value) =>
                      setCreateForm({
                        ...createForm,
                        appendixIds:
                          value === true
                            ? [...createForm.appendixIds, appendix.appendixId]
                            : createForm.appendixIds.filter(
                                (id) => id !== appendix.appendixId,
                              ),
                      })
                    }
                  />{" "}
                  {appendix.appendixCode} — {appendix.appendixName}
                </label>
              ))}
            </div>
          )}
          {createStep === 3 && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Loại nghiệm thu</Label>
                <Select
                  value={String(createForm.acceptanceKind)}
                  onValueChange={(value) =>
                    setCreateForm({
                      ...createForm,
                      acceptanceKind: Number(value) as AcceptanceKind,
                      paymentMilestoneIds: [],
                    })
                  }
                >
                  <SelectTrigger className="w-full bg-background">
                    <SelectValue placeholder="Chọn loại nghiệm thu" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={String(AcceptanceKind.Final)}>
                      Nghiệm thu toàn bộ
                    </SelectItem>
                    <SelectItem value={String(AcceptanceKind.Partial)}>
                      Nghiệm thu một phần
                    </SelectItem>
                  </SelectContent>
                </Select>
              </div>
              {createForm.acceptanceKind === AcceptanceKind.Partial &&
                availablePaymentMilestones.map((milestone) => (
                  <label
                    key={milestone.paymentMilestoneId}
                    className="flex items-center gap-3 rounded-md border p-3 text-sm"
                  >
                    <Checkbox
                      checked={createForm.paymentMilestoneIds.includes(
                        milestone.paymentMilestoneId,
                      )}
                      onCheckedChange={(value) =>
                        setCreateForm({
                          ...createForm,
                          paymentMilestoneIds:
                            value === true
                              ? [
                                  ...createForm.paymentMilestoneIds,
                                  milestone.paymentMilestoneId,
                                ]
                              : createForm.paymentMilestoneIds.filter(
                                  (id) => id !== milestone.paymentMilestoneId,
                                ),
                        })
                      }
                    />{" "}
                    {milestone.milestoneCode} — {milestone.titleVi}
                  </label>
                ))}
              {createForm.acceptanceKind === AcceptanceKind.Partial &&
                availablePaymentMilestones.length === 0 && (
                  <p className="text-sm text-muted-foreground">
                    Không còn đợt thanh toán nào chưa thuộc biên bản đã chốt
                    hoặc đã ký.
                  </p>
                )}
              <Alert>
                <AlertDescription>
                  Sau khi tạo bản nháp, hệ thống lưu thông tin hai bên và các
                  mục nghiệm thu từ mẫu. Bạn có thể chỉnh người tham gia và nội
                  dung trước khi chốt.
                </AlertDescription>
              </Alert>
            </div>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() =>
                createStep === 1
                  ? setCreateOpen(false)
                  : setCreateStep((step) => step - 1)
              }
            >
              {" "}
              {createStep === 1 ? "Hủy" : "Quay lại"}{" "}
            </Button>
            {createStep < 3 ? (
              <Button
                disabled={
                  createStep === 1 &&
                  (!createForm.templateVersionId ||
                    !createForm.acceptanceCode.trim() ||
                    !createForm.location.trim())
                }
                onClick={() => setCreateStep((step) => step + 1)}
              >
                Tiếp tục
              </Button>
            ) : (
              <Button
                disabled={
                  busy ||
                  (createForm.acceptanceKind === AcceptanceKind.Partial &&
                    createForm.paymentMilestoneIds.length === 0)
                }
                onClick={() => void createRecord()}
              >
                {busy && <Loader2 className="mr-2 size-4 animate-spin" />} Tạo
                Bản nháp
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={finalizeOpen}
        onClose={() => setFinalizeOpen(false)}
        onConfirm={async () => {
          if (!selected) return;
          const saved = await saveAllDraftChanges(true);
          if (!saved) return;

          const finalized = await commit(
            () =>
              contractAcceptanceApi.finalize(
                contract.contractId,
                saved.acceptanceRecordId,
                saved.rowVersion,
              ),
            "Đã chốt biên bản nghiệm thu.",
            true,
          );
          if (finalized) setFinalizeOpen(false);
        }}
        title="Chốt biên bản nghiệm thu?"
        description="Hệ thống sẽ lưu lại toàn bộ các sửa đổi hiện tại, sau đó tạo và khóa file DOCX/PDF chính thức. Sau bước này không thể sửa đổi nội dung biên bản."
        confirmText="Chốt biên bản"
        isLoading={busy}
      />
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Hủy biên bản nghiệm thu</DialogTitle>
            <DialogDescription>
              Record được giữ lại để audit và không thể dùng để hoàn tất hợp
              đồng.
            </DialogDescription>
          </DialogHeader>
          <Textarea
            value={cancelReason}
            onChange={(event) => setCancelReason(event.target.value)}
            placeholder="Lý do hủy bắt buộc"
          />
          <DialogFooter>
            <Button variant="outline" onClick={() => setCancelOpen(false)}>
              Đóng
            </Button>
            <Button
              variant="destructive"
              disabled={!cancelReason.trim() || busy}
              onClick={() =>
                selected &&
                void commit(
                  () =>
                    contractAcceptanceApi.cancel(
                      contract.contractId,
                      selected.acceptanceRecordId,
                      cancelReason.trim(),
                      selected.rowVersion,
                    ),
                  "Đã hủy biên bản.",
                ).then((result) => {
                  if (result) {
                    setCancelOpen(false);
                    setCancelReason("");
                  }
                })
              }
            >
              Hủy biên bản
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function Field({
  label,
  value,
  onChange,
  type = "text",
  disabled = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  disabled?: boolean;
}) {
  return (
    <div className="space-y-2">
      <Label>{label}</Label>
      <Input
        type={type}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.target.value)}
        className="bg-background"
      />
    </div>
  );
}

function PartyEditor({
  title,
  party,
  representativeLabel = "Người đại diện",
  onChange,
}: {
  title: string;
  party: ContractAcceptanceParty;
  representativeLabel?: string;
  onChange: (key: keyof ContractAcceptanceParty, value: string) => void;
}) {
  return (
    <div className="space-y-3.5 rounded-xl border border-border/70 bg-card p-4 shadow-xs">
      <div className="flex items-center gap-2 border-b border-border/60 pb-2.5">
        <Building2 className="size-4 text-primary shrink-0" />
        <span className="text-sm font-semibold text-foreground">{title}</span>
      </div>
      <Field
        label="Tên pháp nhân"
        value={party.legalName}
        disabled
        onChange={(value) => onChange("legalName", value)}
      />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field
          label={representativeLabel}
          value={party.representativeName}
          onChange={(value) => onChange("representativeName", value)}
        />
        <Field
          label="Chức vụ"
          value={party.representativeTitle ?? ""}
          onChange={(value) => onChange("representativeTitle", value)}
        />
      </div>
      <Field
        label="Địa chỉ"
        value={party.address ?? ""}
        onChange={(value) => onChange("address", value)}
      />
      <div className="grid gap-3 sm:grid-cols-3">
        <Field
          label="Điện thoại"
          value={party.phone ?? ""}
          onChange={(value) => onChange("phone", value)}
        />
        <Field
          label="Fax"
          value={party.fax ?? ""}
          onChange={(value) => onChange("fax", value)}
        />
        <Field
          label="Mã số thuế"
          value={party.taxCode ?? ""}
          onChange={(value) => onChange("taxCode", value)}
        />
      </div>
    </div>
  );
}
