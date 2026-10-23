"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { format } from "date-fns";
import { formatDate } from "@/lib/format-date";
import { formatDateTime } from "@/lib/format-date-time";
import {
  CheckCircle2,
  ChevronDown,
  Download,
  Eye,
  FileSignature,
  FileSliders,
  Loader2,
  Plus,
  Save,
  Truck,
} from "lucide-react";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
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
import { toast } from "@/components/ui/sonner";
import {
  DocumentField,
  DocumentPartyFields,
} from "@/components/contract-documents/document-party-fields";
import { DocumentQuantitySelector } from "@/components/contract-documents/document-quantity-selector";
import { DocumentRecordSwitcher } from "@/components/contract-documents/document-record-switcher";
import { DocumentRecordPagination } from "@/components/contract-documents/document-record-pagination";
import { useDocumentRecordPagination } from "@/hooks/use-document-record-pagination";
import { ContractTermCard } from "@/components/contracts/contract-term-card";
import { getApiErrorMessage } from "@/lib/api-error";
import {
  ContractDirection,
  ContractLanguageMode,
  ContractStatus,
  type ContractDetailResponse,
} from "@/services/contract-api";
import {
  contractHandoverApi as api,
  HandoverStatus,
  HandoverScopeMode,
  type HandoverDetail,
  type HandoverReadiness,
  type HandoverScopeCandidate,
  type HandoverSummary,
  type HandoverTemplate,
  type HandoverLineInput,
  type HandoverMetadata,
} from "@/services/contract-handover-api";

const statusText = {
  [HandoverStatus.Draft]: "Bản nháp",
  [HandoverStatus.Finalized]: "Chờ bản ký",
  [HandoverStatus.Signed]: "Đã ký",
  [HandoverStatus.Cancelled]: "Đã hủy",
};
const day = (value?: string) =>
  value ? new Date(value.slice(0, 10) + "T00:00:00") : undefined;
const emptyForm = (): HandoverMetadata => ({
  handoverCode: "",
  handoverDate: format(new Date(), "yyyy-MM-dd"),
  location: "",
  locationEn: "",
  appendixIds: [],
  lines: [],
});

export function ContractHandoverRecords({
  contract,
  canManage,
  revision,
  onChanged,
}: {
  contract: ContractDetailResponse;
  canManage: boolean;
  revision: number;
  onChanged: () => Promise<void>;
}) {
  const [records, setRecords] = useState<HandoverSummary[]>([]);
  const {
    page: recordPage,
    pageSize: recordPageSize,
    totalPages: totalRecordPages,
    paginatedRecords,
    setPage: setRecordPage,
    changePageSize: changeRecordPageSize,
  } = useDocumentRecordPagination(records, contract.contractId);
  const [readiness, setReadiness] = useState<HandoverReadiness>();
  const [templates, setTemplates] = useState<HandoverTemplate[]>([]);
  const [candidates, setCandidates] = useState<HandoverScopeCandidate[]>([]);
  const [selected, setSelected] = useState<HandoverDetail>();
  const [detailsOpen, setDetailsOpen] = useState(true);
  const [saved, setSaved] = useState("");
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [baselineOpen, setBaselineOpen] = useState(false);
  const [modes, setModes] = useState<Record<number, HandoverScopeMode>>({});
  const [createOpen, setCreateOpen] = useState(false);
  const [step, setStep] = useState(1);
  const [templateId, setTemplateId] = useState(0);
  const [form, setForm] = useState(emptyForm);
  const [confirm, setConfirm] = useState<
    "scope" | "finalize" | "sign" | "cancel"
  >();
  const [reason, setReason] = useState("");
  const [file, setFile] = useState<File>();
  const [signDate, setSignDate] = useState("");
  const [openSections, setOpenSections] = useState<Set<string>>(new Set());
  const sectionRoot = useRef<HTMLDivElement>(null);
  const saveLock = useRef(false);
  const isBilingual = contract.languageMode === ContractLanguageMode.Bilingual;
  const editable =
    canManage &&
    contract.status === ContractStatus.Signed &&
    selected?.record.status === HandoverStatus.Draft;
  const dirty = !!selected && JSON.stringify(selected) !== saved;
  const appendices = contract.currentVersion.appendices ?? [];
  const acceptRecord = (next: HandoverDetail) => {
    if (selected?.record.handoverRecordId !== next.record.handoverRecordId) {
      setFile(undefined);
      setSignDate("");
      setReason("");
      setOpenSections(new Set());
    }
    setSelected(next);
    setDetailsOpen(true);
    setSaved(JSON.stringify(next));
  };
  const openRecord = async (recordId: number) => {
    if (busy) return;
    if (selected?.record.handoverRecordId === recordId) {
      setDetailsOpen(true);
      return;
    }
    if (dirty) {
      toast.error("Vui lòng lưu thay đổi trước khi mở biên bản khác.");
      return;
    }
    setBusy(true);
    try {
      acceptRecord(await api.get(contract.contractId, recordId));
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Không thể tải biên bản bàn giao."));
    } finally {
      setBusy(false);
    }
  };
  const toggleRecord = (recordId: number) => {
    if (busy) return;
    if (selected?.record.handoverRecordId === recordId) {
      setDetailsOpen((open) => !open);
      return;
    }
    void openRecord(recordId);
  };
  const load = useCallback(async () => {
    try {
      const [list, state] = await Promise.all([
        api.list(contract.contractId),
        api.readiness(contract.contractId),
      ]);
      setRecords(list);
      setReadiness(state);
      setError("");
    } catch (e) {
      setError(getApiErrorMessage(e, "Không thể tải hồ sơ bàn giao."));
    } finally {
      setLoading(false);
    }
  }, [contract.contractId, contract.currentVersion.versionId]);
  useEffect(() => {
    void load();
  }, [load, revision]);
  useEffect(() => {
    setSelected(undefined);
    setSaved("");
  }, [contract.contractId]);
  const refresh = async () => {
    await load();
    try {
      await onChanged();
    } catch {
      toast.error("Đã lưu nhưng chưa tải lại được tiến độ. Vui lòng làm mới.");
    }
  };
  const mutate = async (
    action: () => Promise<HandoverDetail>,
    message: string,
  ) => {
    if (saveLock.current) return null;
    saveLock.current = true;
    setBusy(true);
    try {
      const next = await action();
      acceptRecord(next);
      setRecordPage(1);
      toast.success(message);
      await refresh();
      return next;
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Không thể lưu bàn giao."));
      return null;
    } finally {
      setBusy(false);
      saveLock.current = false;
    }
  };
  const metadataError = (value: HandoverMetadata) =>
    !value.handoverCode.trim()
      ? "Vui lòng nhập mã biên bản."
      : !value.handoverDate
        ? "Vui lòng chọn ngày bàn giao."
        : !value.location.trim()
          ? "Vui lòng nhập địa điểm."
          : isBilingual && !value.locationEn?.trim()
            ? "Vui lòng nhập địa điểm tiếng Anh."
            : value.lines.length === 0 ||
                value.lines.some((x) => x.quantity <= 0)
              ? "Chọn phạm vi và số lượng bàn giao lớn hơn 0."
              : null;
  const save = async (quiet = false) => {
    if (!selected || !editable) return null;
    const value = {
      handoverCode: selected.record.handoverCode,
      handoverDate: selected.record.handoverDate.slice(0, 10),
      location: selected.location,
      locationEn: selected.locationEn,
      appendixIds: selected.appendixIds,
      lines: selected.lines.map(
        ({ handoverScopeItemId, quantity, notesVi, notesEn }) => ({
          handoverScopeItemId,
          quantity,
          notesVi,
          notesEn,
        }),
      ),
    };
    const invalid = metadataError(value);
    if (invalid) {
      toast.error(invalid);
      return null;
    }
    if (!dirty) return selected;
    return mutate(
      () =>
        api.update(contract.contractId, selected.record.handoverRecordId, {
          ...value,
          rowVersion: selected.record.rowVersion,
          parties: selected.parties,
          sections: selected.sections,
        }),
      quiet
        ? "Đã lưu bản nháp trước thao tác."
        : "Đã lưu toàn bộ bản nháp bàn giao.",
    );
  };
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (
        !(e.ctrlKey || e.metaKey) ||
        e.altKey ||
        e.shiftKey ||
        e.key.toLowerCase() !== "s" ||
        !editable ||
        !(e.target instanceof Node) ||
        !sectionRoot.current?.contains(e.target)
      )
        return;
      // Only this document handles Ctrl+S while its own fields have focus.
      e.preventDefault();
      e.stopImmediatePropagation();
      if (!busy && !e.repeat && !createOpen && !baselineOpen && !confirm)
        void save();
    };
    window.addEventListener("keydown", handler, true);
    return () => window.removeEventListener("keydown", handler, true);
  });
  const startCreate = async () => {
    setBusy(true);
    try {
      const options = await api.templates(contract.contractId);
      setTemplates(options);
      setTemplateId(options.length === 1 ? options[0].templateVersionId : 0);
      setForm(emptyForm());
      setStep(1);
      setCreateOpen(true);
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Không thể tải mẫu bàn giao."));
    } finally {
      setBusy(false);
    }
  };
  const openBaseline = async () => {
    setBusy(true);
    try {
      setCandidates(await api.candidates(contract.contractId));
      setModes({});
      setBaselineOpen(true);
    } catch (e) {
      toast.error(getApiErrorMessage(e, "Không thể đọc phạm vi đã ký."));
    } finally {
      setBusy(false);
    }
  };
  const quantities = (lines: HandoverLineInput[]) =>
    (readiness?.items ?? []).map((item) => ({
      id: item.handoverScopeItemId,
      name: item.nameVi,
      unit: item.unitVi,
      available: Math.max(
        0,
        item.requiredQuantity - item.delivered - item.reserved,
      ),
      description: `${item.code} · Đã giao ${item.delivered}/${item.requiredQuantity} · Chờ ký ${item.reserved}`,
      quantity: lines.find(
        (x) => x.handoverScopeItemId === item.handoverScopeItemId,
      )?.quantity,
      wholePackage: item.mode === HandoverScopeMode.WholePackage,
    }));
  const changeLines = (
    lines: HandoverLineInput[],
    id: number,
    qty: number | null,
  ) =>
    qty === null
      ? lines.filter((x) => x.handoverScopeItemId !== id)
      : lines.some((x) => x.handoverScopeItemId === id)
        ? lines.map((x) =>
            x.handoverScopeItemId === id ? { ...x, quantity: qty } : x,
          )
        : [...lines, { handoverScopeItemId: id, quantity: qty }];
  const documentFile = async (kind: "docx" | "pdf" | "evidence") => {
    if (!selected || busy) return;
    const previewWindow =
      kind === "pdf" ? window.open("about:blank", "_blank") : null;
    if (kind === "pdf" && !previewWindow) {
      toast.error("Vui lòng cho phép mở cửa sổ bật lên để xem PDF bàn giao.");
      return;
    }
    if (previewWindow) previewWindow.opener = null;
    try {
      const target = editable && dirty ? await save(true) : selected;
      if (!target) {
        previewWindow?.close();
        return;
      }
      setBusy(true);
      const blob =
        kind === "evidence"
          ? await api.evidence(
              contract.contractId,
              target.record.handoverRecordId,
            )
          : await api.document(
              contract.contractId,
              target.record.handoverRecordId,
              kind,
              target.record.status === HandoverStatus.Draft,
            );
      const url = URL.createObjectURL(
        kind === "pdf" && blob.type !== "application/pdf"
          ? new Blob([blob], { type: "application/pdf" })
          : blob,
      );
      if (previewWindow) previewWindow.location.replace(url);
      else {
        const a = document.createElement("a");
        a.href = url;
        a.download =
          kind === "evidence"
            ? (target.signedEvidence?.fileName ?? "ban-ky")
            : `${target.record.handoverCode}.docx`;
        a.click();
      }
      setTimeout(() => URL.revokeObjectURL(url), 60_000);
    } catch (e) {
      previewWindow?.close();
      toast.error(getApiErrorMessage(e, "Không thể mở tệp bàn giao."));
    } finally {
      setBusy(false);
    }
  };
  const confirmAction = async () => {
    if (busy) return;
    if (confirm === "scope") {
      setBusy(true);
      try {
        setReadiness(
          await api.confirmScope(contract.contractId, {
            contractVersionId: contract.currentVersion.versionId,
            contractRowVersion: contract.rowVersion,
            versionRowVersion: contract.currentVersion.rowVersion,
            items: candidates.map((x) => ({
              contractItemId: x.contractItemId,
              mode: modes[x.contractItemId],
            })),
          }),
        );
        setBaselineOpen(false);
        setConfirm(undefined);
        toast.success("Đã xác nhận phạm vi bất biến.");
        await refresh();
      } catch (e) {
        toast.error(getApiErrorMessage(e, "Không thể xác nhận phạm vi."));
      } finally {
        setBusy(false);
      }
      return;
    }
    if (!selected) return;
    if (confirm === "finalize") {
      const next = await save(true);
      if (!next) return;
      const done = await mutate(
        () =>
          api.finalize(
            contract.contractId,
            next.record.handoverRecordId,
            next.record.rowVersion,
          ),
        "Đã chốt, đang chờ bản ký.",
      );
      if (done) setConfirm(undefined);
    } else if (confirm === "cancel") {
      if (!reason.trim()) {
        toast.error("Vui lòng nhập lý do hủy.");
        return;
      }
      const done = await mutate(
        () =>
          api.cancel(
            contract.contractId,
            selected.record.handoverRecordId,
            selected.record.rowVersion,
            reason,
          ),
        "Đã hủy biên bản và giải phóng phần giữ chỗ.",
      );
      if (done) {
        setConfirm(undefined);
        setReason("");
      }
    } else if (confirm === "sign" && file && signDate) {
      const done = await mutate(
        () =>
          api.sign(
            contract.contractId,
            selected.record.handoverRecordId,
            file,
            selected.record.rowVersion,
            signDate,
          ),
        "Đã lưu bản ký và cập nhật phạm vi bàn giao.",
      );
      if (done) {
        setConfirm(undefined);
        setFile(undefined);
        setSignDate("");
      }
    }
  };
  const metadataFields = (
    value: HandoverMetadata,
    change: (patch: Partial<HandoverMetadata>) => void,
  ) => (
    <div className="grid gap-4 sm:grid-cols-2">
      <DocumentField
        disabled={busy}
        label="Mã biên bản"
        required
        value={value.handoverCode}
        onChange={(v) => change({ handoverCode: v })}
      />
      <div className="space-y-2">
        <Label>
          Ngày bàn giao <span className="text-red-500">*</span>
        </Label>
        <DateFilter
          disabled={busy}
          date={day(value.handoverDate)}
          onChange={(v) =>
            change({ handoverDate: v ? format(v, "yyyy-MM-dd") : "" })
          }
          className="flex-1"
          limit={{ to: new Date() }}
        />
      </div>
      <DocumentField
        disabled={busy}
        label="Địa điểm"
        required
        value={value.location}
        onChange={(v) => change({ location: v })}
      />
      {isBilingual && (
        <DocumentField
          disabled={busy}
          label="Địa điểm tiếng Anh"
          required
          value={value.locationEn ?? ""}
          onChange={(v) => change({ locationEn: v })}
        />
      )}
    </div>
  );
  const referenceFields = (ids: number[], change: (next: number[]) => void) => (
    <div className="space-y-3">
      <p className="text-sm text-muted-foreground">
        Hợp đồng đã ký luôn là căn cứ. Chọn thêm phụ lục thuộc cùng hồ sơ:
      </p>
      {appendices.map((x) => (
        <Label key={x.appendixId} className="flex gap-2">
          <Checkbox
            disabled={busy}
            checked={ids.includes(x.appendixId)}
            onCheckedChange={(v) =>
              change(
                v === true
                  ? [...ids, x.appendixId]
                  : ids.filter((id) => id !== x.appendixId),
              )
            }
          />
          {x.appendixCode} · {x.appendixName}
        </Label>
      ))}
    </div>
  );

  return (
    <div
      ref={sectionRoot}
      data-document-save-scope="handover"
      id="contract-handover"
      className="scroll-mt-6"
    >
      <Card>
        <CardHeader className="flex flex-row items-center justify-between gap-3">
          <CardTitle className="flex items-center gap-2">
            <Truck className="size-5" />
            Biên bản bàn giao
          </CardTitle>
          <div className="flex gap-2">
            {canManage &&
              contract.status === ContractStatus.Signed &&
              readiness &&
              !readiness.baselineId && (
                <Button
                  size="sm"
                  disabled={busy}
                  onClick={() => void openBaseline()}
                >
                  <FileSliders className="mr-2 size-4" />
                  Thiết lập phạm vi
                </Button>
              )}
            {canManage && readiness?.canCreate && (
              <Button
                size="sm"
                disabled={busy}
                onClick={() => void startCreate()}
              >
                <Plus className="mr-2 size-4" />
                Tạo biên bản
              </Button>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-5">
          {loading && <p>Đang tải bàn giao…</p>}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>
                {error}{" "}
                <Button variant="link" onClick={() => void load()}>
                  Thử lại
                </Button>
              </AlertDescription>
            </Alert>
          )}
          {readiness?.blockerMessage && (
            <Alert>
              <AlertDescription>{readiness.blockerMessage}</AlertDescription>
            </Alert>
          )}
          {readiness?.completed && <Badge>Đã bàn giao đủ phạm vi</Badge>}
          {readiness?.items.map((x) => (
            <div
              key={x.handoverScopeItemId}
              className="rounded-lg border p-3 text-sm"
            >
              <p className="font-medium">
                {x.nameVi} ·{" "}
                {x.mode === HandoverScopeMode.WholePackage
                  ? `Nguyên gói (lượng gốc ${x.sourceQuantity})`
                  : "Theo số lượng đã ký"}
              </p>
              <p>
                Đã giao {x.delivered}/{x.requiredQuantity} {x.unitVi} · Chờ ký{" "}
                {x.reserved} · Còn {x.remaining} · Đã nghiệm thu {x.accepted}
              </p>
            </div>
          ))}
          {records.length > 0 && (
            <DocumentRecordSwitcher
              title="Chuyển biên bản bàn giao"
              count={records.length}
              selectedCode={
                detailsOpen ? selected?.record.handoverCode : undefined
              }
              onOpenChange={(open) => {
                if (!open) setDetailsOpen(false);
              }}
            >
              <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                {paginatedRecords.map((x) => (
                  <Button
                    type="button"
                    key={x.handoverRecordId}
                    variant="outline"
                    aria-pressed={
                      detailsOpen &&
                      selected?.record.handoverRecordId === x.handoverRecordId
                    }
                    aria-expanded={
                      detailsOpen &&
                      selected?.record.handoverRecordId === x.handoverRecordId
                    }
                    className={`h-auto w-full justify-start rounded-lg p-3 text-left whitespace-normal hover:bg-muted ${
                      detailsOpen &&
                      selected?.record.handoverRecordId === x.handoverRecordId
                        ? "border-primary bg-muted"
                        : ""
                    }`}
                    disabled={busy}
                    onClick={() => toggleRecord(x.handoverRecordId)}
                  >
                    <div className="w-full space-y-1">
                      <div className="flex items-start justify-between gap-2">
                        <span className="min-w-0 break-words font-semibold">
                          {x.handoverCode}
                        </span>
                        <Badge
                          variant={
                            x.status === HandoverStatus.Cancelled
                              ? "destructive"
                              : "secondary"
                          }
                        >
                          {statusText[x.status]}
                        </Badge>
                      </div>
                      <p className="text-xs font-normal text-muted-foreground">
                        {formatDate(x.handoverDate)}
                        {detailsOpen &&
                          selected?.record.handoverRecordId ===
                            x.handoverRecordId &&
                          " · Đang xem"}
                      </p>
                    </div>
                  </Button>
                ))}
              </div>
              <DocumentRecordPagination
                totalCount={records.length}
                page={recordPage}
                pageSize={recordPageSize}
                totalPages={totalRecordPages}
                onPageChange={setRecordPage}
                onPageSizeChange={changeRecordPageSize}
              />
            </DocumentRecordSwitcher>
          )}
          {readiness?.activeRecordId && !selected && (
            <p className="text-sm text-muted-foreground">
              Mở danh sách biên bản phía trên và chọn bản nháp/chờ ký để tiếp
              tục, hoặc hủy trước khi tạo lần mới.
            </p>
          )}
          {!loading && records.length === 0 && (
            <p className="text-sm text-muted-foreground">
              Chưa có biên bản bàn giao.
            </p>
          )}
          {selected && (
            <div
              hidden={!detailsOpen}
              className="space-y-6 rounded-lg border p-4"
            >
              <div className="flex flex-wrap items-center justify-between gap-3">
                <div>
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="font-semibold">
                      {selected.record.handoverCode}
                    </h3>
                    <Badge variant="outline">
                      {statusText[selected.record.status]}
                    </Badge>
                  </div>
                  <p className="mt-1 text-sm text-muted-foreground">
                    {selected.templateName} · Phiên bản mẫu{" "}
                    {selected.templateVersionNo} · Phiên bản hợp đồng{" "}
                    {selected.contractVersionNo}
                  </p>
                </div>
                <div className="ml-auto flex flex-wrap gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={busy}
                    onClick={() => void documentFile("pdf")}
                  >
                    <Eye className="size-4" /> PDF
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    disabled={busy}
                    onClick={() => void documentFile("docx")}
                  >
                    <Download className="size-4" /> DOCX
                  </Button>
                </div>
              </div>
              {editable && (
                <div className="sticky top-2 z-20 flex flex-col gap-3 rounded-xl border border-primary/30 bg-background/95 p-3 shadow-md backdrop-blur sm:flex-row sm:items-center sm:justify-between">
                  <div>
                    <p className="text-sm font-semibold">
                      Chỉnh sửa bản nháp bàn giao
                    </p>
                    <p className="text-xs text-muted-foreground">
                      Một lần lưu áp dụng cho thông tin, căn cứ, phạm vi và toàn
                      bộ nội dung biên bản. Có thể dùng Ctrl+S (hoặc ⌘+S).
                    </p>
                    <p
                      className="mt-1 text-xs text-muted-foreground"
                      role="status"
                    >
                      {dirty
                        ? "Có thay đổi chưa được lưu."
                        : "Bản nháp đã được lưu."}
                    </p>
                  </div>
                  <Button
                    type="button"
                    className="shrink-0 gap-2"
                    disabled={busy || !dirty}
                    onClick={() => void save()}
                  >
                    {busy ? (
                      <Loader2 className="size-4 animate-spin" />
                    ) : (
                      <Save className="size-4" />
                    )}
                    Lưu tất cả thay đổi
                  </Button>
                </div>
              )}
              {editable ? (
                <>
                  {metadataFields(
                    {
                      ...selected,
                      handoverCode: selected.record.handoverCode,
                      handoverDate: selected.record.handoverDate,
                      lines: selected.lines,
                    },
                    (patch) =>
                      setSelected({
                        ...selected,
                        location: patch.location ?? selected.location,
                        locationEn: patch.locationEn ?? selected.locationEn,
                        record: {
                          ...selected.record,
                          handoverCode:
                            patch.handoverCode ?? selected.record.handoverCode,
                          handoverDate:
                            patch.handoverDate ?? selected.record.handoverDate,
                        },
                      }),
                  )}
                  <DocumentQuantitySelector
                    disabled={busy}
                    rows={quantities(selected.lines)}
                    onChange={(id, qty) => {
                      const next = changeLines(selected.lines, id, qty);
                      setSelected({
                        ...selected,
                        lines: next.map((l) => {
                          const old = selected.lines.find(
                            (x) =>
                              x.handoverScopeItemId === l.handoverScopeItemId,
                          );
                          const s = readiness?.items.find(
                            (x) =>
                              x.handoverScopeItemId === l.handoverScopeItemId,
                          );
                          return old
                            ? { ...old, ...l }
                            : {
                                ...l,
                                handoverLineId: 0,
                                code: s?.code ?? "",
                                nameVi: s?.nameVi ?? "",
                                nameEn: s?.nameEn,
                                unitVi: s?.unitVi ?? "",
                                unitEn: s?.unitEn,
                              };
                        }),
                      });
                    }}
                  />
                  {selected.lines.map((line) => (
                    <div
                      className="grid gap-2 sm:grid-cols-2"
                      key={line.handoverScopeItemId}
                    >
                      <DocumentField
                        disabled={busy}
                        label={`Ghi chú · ${line.nameVi}`}
                        value={line.notesVi ?? ""}
                        onChange={(v) =>
                          setSelected({
                            ...selected,
                            lines: selected.lines.map((x) =>
                              x.handoverScopeItemId === line.handoverScopeItemId
                                ? { ...x, notesVi: v }
                                : x,
                            ),
                          })
                        }
                      />
                      {isBilingual && (
                        <DocumentField
                          disabled={busy}
                          label="Ghi chú tiếng Anh"
                          value={line.notesEn ?? ""}
                          onChange={(v) =>
                            setSelected({
                              ...selected,
                              lines: selected.lines.map((x) =>
                                x.handoverScopeItemId ===
                                line.handoverScopeItemId
                                  ? { ...x, notesEn: v }
                                  : x,
                              ),
                            })
                          }
                        />
                      )}
                    </div>
                  ))}
                  {referenceFields(selected.appendixIds, (appendixIds) =>
                    setSelected({ ...selected, appendixIds }),
                  )}
                </>
              ) : (
                <div className="space-y-2 text-sm">
                  <p>
                    Ngày bàn giao: {formatDate(selected.record.handoverDate)} ·{" "}
                    {selected.location}
                  </p>
                  {isBilingual && <p>{selected.locationEn}</p>}
                  {selected.lines.map((x) => (
                    <p key={x.handoverLineId}>
                      {x.nameVi}: {x.quantity} {x.unitVi}
                    </p>
                  ))}
                  {selected.references.map((x, i) => (
                    <p key={i} className="italic">
                      {isBilingual && (
                        <>
                          {x.textEn}
                          <br />
                        </>
                      )}
                      {x.textVi}
                    </p>
                  ))}
                </div>
              )}
              <Collapsible defaultOpen={false}>
                <CollapsibleTrigger asChild>
                  <Button
                    variant="ghost"
                    className="group/handover-parties mb-2"
                  >
                    Thông tin hai bên
                    <ChevronDown
                      aria-hidden="true"
                      className="size-4 transition-transform duration-200 group-data-[state=open]/handover-parties:-rotate-180 motion-reduce:transition-none"
                    />
                  </Button>
                </CollapsibleTrigger>
                <CollapsibleContent className="grid gap-4 sm:grid-cols-2">
                  {selected.parties.map((p) => {
                    const provider =
                      selected.contractDirection === ContractDirection.Sale
                        ? p.subjectKind === 1
                        : p.subjectKind === 2;
                    return (
                      <DocumentPartyFields
                        key={p.subjectKind}
                        title={`${p.subjectKind === 1 ? "Doanh nghiệp" : "Đối tác"} · ${provider ? "Bên giao" : "Bên nhận"}`}
                        representativeLabel={
                          provider ? "Người giao" : "Người nhận"
                        }
                        party={p}
                        disabled={!editable || busy}
                        onChange={(key, value) =>
                          setSelected({
                            ...selected,
                            parties: selected.parties.map((x) =>
                              x.subjectKind === p.subjectKind
                                ? { ...x, [key]: value }
                                : x,
                            ),
                          })
                        }
                      />
                    );
                  })}
                </CollapsibleContent>
              </Collapsible>
              <Collapsible defaultOpen={false}>
                <CollapsibleTrigger asChild>
                  <Button
                    variant="ghost"
                    className="group/handover-parties mb-2"
                  >
                    Nội dung bàn giao{" "}
                    <ChevronDown
                      aria-hidden="true"
                      className="size-4 transition-transform duration-200 group-data-[state=open]/handover-parties:-rotate-180 motion-reduce:transition-none"
                    />
                  </Button>
                </CollapsibleTrigger>
                <CollapsibleContent className="space-y-3">
                  {editable && (
                    <Button
                      variant="outline"
                      disabled={busy}
                      onClick={() => {
                        let n = selected.sections.length + 1;
                        while (
                          selected.sections.some(
                            (x) => x.sectionCode === `SECTION-${n}`,
                          )
                        )
                          n++;
                        setSelected({
                          ...selected,
                          sections: [
                            ...selected.sections,
                            {
                              sectionCode: `SECTION-${n}`,
                              titleVi: "",
                              titleEn: "",
                              contentVi: "",
                              contentEn: "",
                              bilingualContentMode: 1,
                            },
                          ],
                        });
                      }}
                    >
                      Thêm mục nội dung
                    </Button>
                  )}
                  {selected.sections.map((x, i) => (
                    <ContractTermCard
                      key={x.sectionCode}
                      inputId={`handover-${selected.record.handoverRecordId}-${x.sectionCode}`}
                      term={{
                        termCode: x.sectionCode,
                        termTitle: x.titleVi,
                        termTitleEn: x.titleEn,
                        termContent: x.contentVi,
                        termContentEn: x.contentEn,
                        bilingualContentMode: x.bilingualContentMode,
                        isNegotiable: false,
                        displayOrder: i,
                      }}
                      editable={editable && !busy}
                      showNegotiation={false}
                      isBilingual={isBilingual}
                      titleLabel="Tiêu đề mục"
                      contentLabel="Nội dung"
                      orderLabel="Mục"
                      open={openSections.has(x.sectionCode)}
                      onOpenChange={(open) =>
                        setOpenSections((current) => {
                          const next = new Set(current);
                          if (open) next.add(x.sectionCode);
                          else next.delete(x.sectionCode);
                          return next;
                        })
                      }
                      canMoveUp={i > 0}
                      canMoveDown={i < selected.sections.length - 1}
                      onMove={(direction) => {
                        const next = [...selected.sections];
                        [next[i], next[i + direction]] = [
                          next[i + direction],
                          next[i],
                        ];
                        setSelected({ ...selected, sections: next });
                      }}
                      onRemove={
                        editable
                          ? () =>
                              setSelected({
                                ...selected,
                                sections: selected.sections.filter(
                                  (_, index) => i !== index,
                                ),
                              })
                          : undefined
                      }
                      onValueChange={(patch) =>
                        setSelected((current) =>
                          current
                            ? {
                                ...current,
                                sections: current.sections.map((s, index) =>
                                  index === i
                                    ? {
                                        ...s,
                                        titleVi: patch.termTitle ?? s.titleVi,
                                        titleEn:
                                          patch.termTitleEn !== undefined
                                            ? patch.termTitleEn
                                            : s.titleEn,
                                        contentVi:
                                          patch.termContent !== undefined
                                            ? patch.termContent
                                            : s.contentVi,
                                        contentEn:
                                          patch.termContentEn !== undefined
                                            ? patch.termContentEn
                                            : s.contentEn,
                                        bilingualContentMode:
                                          patch.bilingualContentMode ??
                                          s.bilingualContentMode,
                                      }
                                    : s,
                                ),
                              }
                            : current,
                        )
                      }
                      onChange={(key, value) => {
                        const map = {
                          termTitle: "titleVi",
                          termTitleEn: "titleEn",
                          termContent: "contentVi",
                          termContentEn: "contentEn",
                          bilingualContentMode: "bilingualContentMode",
                          isNegotiable: "",
                        } as const;
                        if (map[key])
                          setSelected({
                            ...selected,
                            sections: selected.sections.map((s, index) =>
                              index === i ? { ...s, [map[key]]: value } : s,
                            ),
                          });
                      }}
                    />
                  ))}
                </CollapsibleContent>
              </Collapsible>
              {editable && (
                <Card className="border-emerald-200 bg-gradient-to-r from-emerald-50/70 via-background to-emerald-50/30 p-5 shadow-sm dark:border-emerald-900/50 dark:from-emerald-950/20 dark:via-background dark:to-emerald-950/10">
                  <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-start gap-3.5">
                      <div className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-500/10 text-emerald-600 dark:bg-emerald-500/20 dark:text-emerald-400">
                        <CheckCircle2 className="size-5" />
                      </div>
                      <div className="space-y-1">
                        <div className="flex items-center gap-2">
                          <h4 className="font-semibold text-foreground">
                            Hoàn tất & Chốt biên bản bàn giao
                          </h4>
                        </div>
                        <p className="text-xs text-muted-foreground leading-relaxed max-w-xl">
                          Vui lòng kiểm tra kỹ thông tin các bên, căn cứ và các
                          hạng mục bàn giao. Khi chốt, hệ thống sẽ tự động lưu
                          lại toàn bộ thay đổi mới nhất, xuất file chính thức và
                          khóa nội dung để chuẩn bị ký kết.
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-2.5 sm:shrink-0">
                      <Button
                        type="button"
                        disabled={busy}
                        onClick={() => setConfirm("finalize")}
                        className="bg-emerald-600 hover:bg-emerald-700 text-white font-medium shadow-sm gap-2"
                      >
                        {busy ? (
                          <Loader2 className="size-4 animate-spin" />
                        ) : (
                          <FileSignature className="size-4" />
                        )}
                        Chốt bàn giao
                      </Button>
                    </div>
                  </div>
                </Card>
              )}
              <div className="flex flex-wrap gap-2">
                {canManage &&
                  contract.status === ContractStatus.Signed &&
                  [HandoverStatus.Draft, HandoverStatus.Finalized].includes(
                    selected.record.status,
                  ) && (
                    <Button
                      variant="destructive"
                      disabled={busy}
                      onClick={() => {
                        setReason("");
                        setConfirm("cancel");
                      }}
                    >
                      Hủy biên bản
                    </Button>
                  )}
              </div>
              <div className="text-xs text-muted-foreground space-y-1">
                <p>
                  Tạo bởi {selected.createdByEmployeeName ?? "—"} ·{" "}
                  {formatDateTime(selected.createdAt)}
                </p>
                {selected.finalizedAt && (
                  <p>
                    Chốt bởi {selected.finalizedByEmployeeName ?? "—"} ·{" "}
                    {formatDateTime(selected.finalizedAt)}
                  </p>
                )}
                {selected.cancelledAt && (
                  <p>
                    Hủy bởi {selected.cancelledByEmployeeName ?? "—"} ·{" "}
                    {formatDateTime(selected.cancelledAt)} ·{" "}
                    {selected.cancellationReason}
                  </p>
                )}
              </div>
              {canManage &&
                contract.status === ContractStatus.Signed &&
                selected.record.status === HandoverStatus.Finalized && (
                  <div className="space-y-3 rounded-md border border-amber-300 bg-amber-50 p-4 dark:bg-amber-950/20">
                    <b>Biên bản đã chốt và đang chờ bản ký</b>
                    <Label htmlFor="handover-signed-file">
                      Tệp bản ký hai bên <span className="text-red-500">*</span>
                    </Label>
                    <div>
                      <Input
                        id="handover-signed-file"
                        type="file"
                        required
                        accept=".pdf,.jpg,.jpeg,.png"
                        disabled={busy}
                        className="bg-background"
                        onChange={(e) => {
                          const next = e.target.files?.[0];
                          if (
                            next &&
                            (next.size > 20 * 1024 * 1024 ||
                              !/\.(pdf|jpe?g|png)$/i.test(next.name))
                          ) {
                            toast.error("Chọn tệp PDF/JPG/PNG tối đa 20 MB.");
                            e.target.value = "";
                            setFile(undefined);
                            return;
                          }
                          setFile(next);
                        }}
                      />
                    </div>
                    <p className="text-xs text-muted-foreground">
                      Tệp PDF/JPG/PNG, tối đa 20 MB. Vui lòng chọn ngày ký thực
                      tế trước khi tải bản ký.
                    </p>
                    <Label>
                      Ngày ký thực tế <span className="text-red-500">*</span>
                    </Label>
                    <DateFilter
                      className="bg-background flex-1"
                      disabled={busy}
                      date={day(signDate)}
                      onChange={(v) =>
                        setSignDate(v ? format(v, "yyyy-MM-dd") : "")
                      }
                      limit={{
                        from: day(selected.record.handoverDate),
                        to: new Date(),
                      }}
                      placeholder="Ngày ký"
                    />
                    <div className="flex justify-end border-t border-amber-200 pt-3 dark:border-amber-900/50">
                      <Button
                        type="button"
                        className="w-full sm:w-auto"
                        disabled={busy || !file || !signDate}
                        onClick={() => setConfirm("sign")}
                      >
                        <FileSignature className="size-4" /> Tải bản ký
                      </Button>
                    </div>
                  </div>
                )}
              {selected.signedEvidence && (
                <Alert>
                  <CheckCircle2 className="size-4" color="green" />
                  <AlertDescription className="space-y-1">
                    <p>
                      Đã ký · Ngày ký:{" "}
                      {formatDate(selected.signedEvidence.signDate)}
                    </p>
                    <p>
                      Tải bởi{" "}
                      {selected.signedEvidence.uploadedByEmployeeName ?? "—"} ·{" "}
                      {formatDateTime(selected.signedEvidence.uploadedAt)}
                    </p>
                    <Button
                      type="button"
                      variant="link"
                      disabled={busy}
                      onClick={() => void documentFile("evidence")}
                    >
                      {selected.signedEvidence.fileName}
                    </Button>
                  </AlertDescription>
                </Alert>
              )}
            </div>
          )}
        </CardContent>
      </Card>

      <Dialog
        open={baselineOpen}
        onOpenChange={(open) => {
          if (!busy && !confirm) setBaselineOpen(open);
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>Thiết lập phạm vi bàn giao</DialogTitle>
            <DialogDescription>
              Toàn bộ hạng mục từ hợp đồng đã ký. Chọn cách theo dõi từng dòng;
              xác nhận xong không sửa được.
            </DialogDescription>
          </DialogHeader>
          {candidates.map((x) => (
            <div
              key={x.contractItemId}
              className="rounded-lg border p-3 space-y-2"
            >
              <p>
                {x.nameVi} · {x.quantity} {x.unitVi}
              </p>
              <Select
                disabled={busy}
                value={modes[x.contractItemId]?.toString() ?? ""}
                onValueChange={(v) =>
                  setModes({
                    ...modes,
                    [x.contractItemId]: Number(v) as HandoverScopeMode,
                  })
                }
              >
                <SelectTrigger aria-label={`Cách theo dõi ${x.nameVi}`}>
                  <SelectValue placeholder="Chọn cách theo dõi (bắt buộc)" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="1">Theo số lượng đã ký</SelectItem>
                  <SelectItem value="2">
                    Nguyên gói (toàn bộ dòng = 1)
                  </SelectItem>
                </SelectContent>
              </Select>
            </div>
          ))}
          {!candidates.length && (
            <Alert>
              <AlertDescription>
                Hợp đồng chưa có hạng mục định lượng để thiết lập phạm vi. Không
                thể suy ra từ nội dung điều khoản.
              </AlertDescription>
            </Alert>
          )}
          <DialogFooter>
            <Button
              disabled={
                busy ||
                !candidates.length ||
                candidates.some((x) => !modes[x.contractItemId])
              }
              onClick={() => setConfirm("scope")}
            >
              Xác nhận phạm vi
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <Dialog
        open={createOpen}
        onOpenChange={(open) => {
          if (!busy) setCreateOpen(open);
        }}
      >
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-3xl">
          <DialogHeader>
            <DialogTitle>Tạo biên bản bàn giao · Bước {step}/3</DialogTitle>
            <DialogDescription>
              Chọn mẫu, thông tin và phạm vi giao lần này.
            </DialogDescription>
          </DialogHeader>
          {step === 1 && (
            <div className="space-y-3">
              <Label>
                Mẫu bàn giao <span className="text-red-500">*</span>
              </Label>
              <Select
                disabled={busy}
                value={templateId ? templateId.toString() : ""}
                onValueChange={(v) => setTemplateId(Number(v))}
              >
                <SelectTrigger className="w-full">
                  <SelectValue placeholder="Chọn mẫu" />
                </SelectTrigger>
                <SelectContent>
                  {templates.map((t) => (
                    <SelectItem
                      key={t.templateVersionId}
                      value={t.templateVersionId.toString()}
                    >
                      {t.templateName} · Phiên bản {t.versionNo}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {!templates.length && (
                <p>Chưa có mẫu bàn giao được phát hành đúng chiều/ngôn ngữ.</p>
              )}
            </div>
          )}
          {step === 2 && (
            <>
              {metadataFields(form, (patch) => setForm({ ...form, ...patch }))}
              {referenceFields(form.appendixIds, (appendixIds) =>
                setForm({ ...form, appendixIds }),
              )}
            </>
          )}
          {step === 3 && (
            <DocumentQuantitySelector
              rows={quantities(form.lines)}
              disabled={busy}
              onChange={(id, qty) =>
                setForm({ ...form, lines: changeLines(form.lines, id, qty) })
              }
            />
          )}
          <DialogFooter>
            {step > 1 && (
              <Button
                variant="outline"
                disabled={busy}
                onClick={() => setStep(step - 1)}
              >
                Quay lại
              </Button>
            )}
            {step < 3 ? (
              <Button
                disabled={busy || (step === 1 && !templateId)}
                onClick={() => {
                  if (step === 2) {
                    const e = metadataError({
                      ...form,
                      lines: [{ handoverScopeItemId: 0, quantity: 1 }],
                    });
                    if (e) {
                      toast.error(e);
                      return;
                    }
                  }
                  setStep(step + 1);
                }}
              >
                Tiếp tục
              </Button>
            ) : (
              <Button
                disabled={busy}
                onClick={async () => {
                  const e = metadataError(form);
                  if (e) {
                    toast.error(e);
                    return;
                  }
                  const next = await mutate(
                    () =>
                      api.create(contract.contractId, {
                        ...form,
                        templateVersionId: templateId,
                        contractVersionId: contract.currentVersion.versionId,
                        contractRowVersion: contract.rowVersion,
                        versionRowVersion: contract.currentVersion.rowVersion,
                      }),
                    "Đã tạo bản nháp bàn giao.",
                  );
                  if (next) setCreateOpen(false);
                }}
              >
                Tạo bản nháp
              </Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>
      <ConfirmDialog
        isOpen={!!confirm}
        onClose={() => setConfirm(undefined)}
        onConfirm={() => void confirmAction()}
        isLoading={busy}
        title={
          confirm === "scope"
            ? "Xác nhận phạm vi bất biến?"
            : confirm === "finalize"
              ? "Chốt biên bản bàn giao?"
              : confirm === "sign"
                ? "Xác nhận bản ký hợp lệ?"
                : "Hủy biên bản bàn giao?"
        }
        description={
          confirm === "cancel" ? (
            <div className="space-y-2">
              <p>Giữ lịch sử, giải phóng phạm vi chờ ký.</p>
              <Label>
                Lý do <span className="text-red-500">*</span>
              </Label>
              <Textarea
                value={reason}
                maxLength={1000}
                onChange={(e) => setReason(e.target.value)}
              />
            </div>
          ) : confirm === "sign" ? (
            "Tôi xác nhận tệp đã có chữ ký hợp lệ của hai bên và ngày ký thực tế chính xác. Chỉ sau bước này mới tính là đã bàn giao."
          ) : confirm === "scope" ? (
            "Mỗi chế độ do bạn chọn sẽ được khóa cùng toàn bộ hạng mục đã ký; không thể sửa hoặc bỏ hạng mục sau xác nhận."
          ) : (
            "Lưu toàn bộ thay đổi rồi khóa nội dung, tạo DOCX/PDF và giữ chỗ số lượng chờ ký. Lưu thất bại sẽ không tiếp tục chốt."
          )
        }
      />
    </div>
  );
}
