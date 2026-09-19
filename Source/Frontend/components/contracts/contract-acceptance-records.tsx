"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { format } from "date-fns";
import {
  CheckCircle2,
  Download,
  Eye,
  FileCheck2,
  FileSignature,
  Loader2,
  Plus,
  Save,
  XCircle,
} from "lucide-react";
import { toast } from "@/components/ui/sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
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
import { Textarea } from "@/components/ui/textarea";
import { ConfirmDialog } from "@/components/ui/custom/confirm-dialog";
import { ContractRichTextEditor } from "@/components/ui/custom/contract-rich-text-editor";
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
  const [templates, setTemplates] = useState<ContractAcceptanceTemplateOption[]>([]);
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
      toast.error(getApiErrorMessage(error, "Không thể tải biên bản nghiệm thu."));
    } finally {
      setLoading(false);
    }
  }, [contract.contractId]);

  useEffect(() => {
    const timeout = window.setTimeout(() => void load(), 0);
    return () => window.clearTimeout(timeout);
  }, [load]);

  const appendices = useMemo(
    () => [...(contract.currentVersion.appendices ?? [])].sort(
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
      setSelected(await contractAcceptanceApi.get(contract.contractId, recordId));
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

  const saveMetadata = async () => {
    if (!selected) return;
    const customer = selected.parties.find(
      (party) => party.partyRole === AcceptancePartyRole.Customer,
    );
    const tenant = selected.parties.find(
      (party) => party.partyRole === AcceptancePartyRole.Tenant,
    );
    if (!customer || !tenant) return;
    await commit(
      () =>
        contractAcceptanceApi.update(
          contract.contractId,
          selected.acceptanceRecordId,
          {
            acceptanceCode: selected.acceptanceCode,
            acceptanceDate: selected.acceptanceDate.slice(0, 10),
            location: selected.location,
            acceptanceKind: selected.acceptanceKind,
            customerParty: partyInput(customer),
            tenantParty: partyInput(tenant),
            rowVersion: selected.rowVersion,
          },
        ),
      "Đã lưu thông tin biên bản.",
    );
  };

  const saveReferences = async () => {
    if (!selected) return;
    await commit(
      () =>
        contractAcceptanceApi.replaceReferences(
          contract.contractId,
          selected.acceptanceRecordId,
          selected.references
            .filter((item) => item.referenceType === AcceptanceReferenceType.Appendix)
            .flatMap((item) => (item.appendixId ? [item.appendixId] : [])),
          selected.rowVersion,
        ),
      "Đã lưu phạm vi căn cứ.",
    );
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

  const saveMilestones = async () => {
    if (!selected) return;
    await commit(
      () =>
        contractAcceptanceApi.replaceMilestones(
          contract.contractId,
          selected.acceptanceRecordId,
          selected.acceptanceKind === AcceptanceKind.Partial
            ? selected.milestones.map((item) => item.paymentMilestoneId)
            : [],
          selected.rowVersion,
        ),
      "Đã lưu phạm vi nghiệm thu.",
    );
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
        ? selected.milestones.some((item) => item.paymentMilestoneId === milestoneId)
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

  const saveSections = async () => {
    if (!selected) return;
    await commit(
      () =>
        contractAcceptanceApi.replaceSections(
          contract.contractId,
          selected.acceptanceRecordId,
          selected.sections.map((section) => ({
            sectionCode: section.sectionCode,
            titleVi: section.titleVi,
            titleEn: section.titleEn,
            contentVi: section.contentVi,
            contentEn: section.contentEn,
            displayOrder: section.displayOrder,
          })),
          selected.rowVersion,
        ),
      "Đã lưu nội dung nghiệm thu.",
    );
  };

  const openDocument = async (
    format: "docx" | "pdf",
    official: boolean,
  ) => {
    if (!selected) return;
    const previewWindow = format === "pdf" ? window.open("about:blank", "_blank") : null;
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
  const canCreate =
    canManage && contract.status === ContractStatus.Signed && signedActive;
  const customer = selected?.parties.find(
    (party) => party.partyRole === AcceptancePartyRole.Customer,
  );
  const tenant = selected?.parties.find(
    (party) => party.partyRole === AcceptancePartyRole.Tenant,
  );

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between gap-3">
        <CardTitle className="flex items-center gap-2">
          <FileCheck2 className="size-5" />
          Biên bản nghiệm thu
        </CardTitle>
        {canCreate && (
          <Button size="sm" onClick={startCreate} disabled={busy || templates.length === 0}>
            <Plus className="mr-2 size-4" /> Tạo biên bản
          </Button>
        )}
      </CardHeader>
      <CardContent className="space-y-5">
        {canManage && contract.status === ContractStatus.Signed && !signedActive && (
          <Alert>
            <AlertDescription>
              Hợp đồng chưa có bản ký active nên chưa thể tạo biên bản nghiệm thu.
            </AlertDescription>
          </Alert>
        )}
        {templates.length === 0 && canCreate && (
          <Alert>
            <AlertDescription>
              Chưa có Acceptance Template đã phát hành. Hãy phát hành template trước khi tạo biên bản.
            </AlertDescription>
          </Alert>
        )}
        {records.length === 0 ? (
          <p className="text-sm text-muted-foreground">Chưa tạo biên bản nghiệm thu.</p>
        ) : (
          <div className="grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
            {records.map((record) => (
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
                  <Badge variant={record.status === AcceptanceRecordStatus.Cancelled ? "destructive" : "secondary"}>
                    {STATUS[record.status]}
                  </Badge>
                </div>
                <p className="mt-1 text-xs text-muted-foreground">
                  {new Date(record.acceptanceDate).toLocaleDateString("vi-VN")} · Version {record.versionNo}
                </p>
              </button>
            ))}
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
                <Button variant="outline" size="sm" disabled={busy} onClick={() => void openDocument("pdf", !isDraft)}>
                  <Eye className="mr-2 size-4" /> Xem PDF
                </Button>
                <Button variant="outline" size="sm" disabled={busy} onClick={() => void openDocument("docx", !isDraft)}>
                  <Download className="mr-2 size-4" /> DOCX
                </Button>
                {isDraft && canManage && (
                  <Button size="sm" disabled={busy} onClick={() => setFinalizeOpen(true)}>
                    <FileSignature className="mr-2 size-4" /> Chốt biên bản
                  </Button>
                )}
              </div>
            </div>

            {isDraft ? (
              <>
                <section className="space-y-3">
                  <h4 className="font-medium">Thông tin và người tham gia</h4>
                  <div className="grid gap-3 md:grid-cols-3">
                    <Field label="Mã biên bản" value={selected.acceptanceCode} onChange={(value) => setSelected({ ...selected, acceptanceCode: value })} />
                    <Field label="Ngày nghiệm thu" type="date" value={selected.acceptanceDate.slice(0, 10)} onChange={(value) => setSelected({ ...selected, acceptanceDate: value })} />
                    <Field label="Địa điểm" value={selected.location} onChange={(value) => setSelected({ ...selected, location: value })} />
                  </div>
                  <div className="grid gap-4 md:grid-cols-2">
                    {customer && (
                      <PartyEditor title="Bên A — Khách hàng" party={customer} onChange={(key, value) => updateParty(AcceptancePartyRole.Customer, key, value)} />
                    )}
                    {tenant && (
                      <PartyEditor title="Bên B — Doanh nghiệp" party={tenant} onChange={(key, value) => updateParty(AcceptancePartyRole.Tenant, key, value)} />
                    )}
                  </div>
                  <Button size="sm" onClick={() => void saveMetadata()} disabled={busy}>
                    <Save className="mr-2 size-4" /> Lưu thông tin
                  </Button>
                </section>

                <section className="space-y-3">
                  <h4 className="font-medium">Căn cứ tự sinh</h4>
                  <label className="flex items-start gap-3 rounded-md border bg-muted/40 p-3 text-sm">
                    <Checkbox checked disabled />
                    <span>{selected.references.find((item) => item.referenceType === AcceptanceReferenceType.Contract)?.textVi}</span>
                  </label>
                  {appendices.map((appendix) => {
                    const reference = selected.references.find((item) => item.appendixId === appendix.appendixId);
                    return (
                      <label key={appendix.appendixId} className="flex items-start gap-3 rounded-md border p-3 text-sm">
                        <Checkbox checked={Boolean(reference)} onCheckedChange={(value) => toggleReference(appendix.appendixId, value === true)} />
                        <span>{reference?.textVi ?? `${appendix.appendixCode} — ${appendix.appendixName}`}</span>
                      </label>
                    );
                  })}
                  <Button size="sm" variant="outline" onClick={() => void saveReferences()} disabled={busy}>
                    <Save className="mr-2 size-4" /> Lưu căn cứ
                  </Button>
                </section>

                <section className="space-y-3">
                  <div className="flex flex-wrap items-center justify-between gap-3">
                    <h4 className="font-medium">Phạm vi nghiệm thu</h4>
                    <select
                      className="h-9 rounded-md border bg-background px-3 text-sm"
                      value={selected.acceptanceKind}
                      onChange={(event) => setSelected({ ...selected, acceptanceKind: Number(event.target.value) as AcceptanceKind, milestones: [] })}
                    >
                      <option value={AcceptanceKind.Partial}>Nghiệm thu một phần</option>
                      <option value={AcceptanceKind.Final}>Nghiệm thu toàn bộ</option>
                    </select>
                  </div>
                  {selected.acceptanceKind === AcceptanceKind.Partial && paymentMilestones.map((milestone) => (
                    <label key={milestone.paymentMilestoneId} className="flex items-center gap-3 rounded-md border p-3 text-sm">
                      <Checkbox checked={selected.milestones.some((item) => item.paymentMilestoneId === milestone.paymentMilestoneId)} onCheckedChange={(value) => toggleMilestone(milestone.paymentMilestoneId, value === true)} />
                      <span>{milestone.milestoneCode} — {milestone.titleVi} ({milestone.paymentPercent}%)</span>
                    </label>
                  ))}
                  <Button size="sm" variant="outline" onClick={() => void saveMilestones()} disabled={busy}>
                    <Save className="mr-2 size-4" /> Lưu phạm vi
                  </Button>
                </section>

                <section className="space-y-4">
                  <h4 className="font-medium">Nội dung biên bản</h4>
                  {selected.sections.map((section, index) => (
                    <div key={section.acceptanceSectionId} className="space-y-3 rounded-md border p-3">
                      <Field label="Tiêu đề" value={section.titleVi} onChange={(value) => setSelected({ ...selected, sections: selected.sections.map((item, itemIndex) => itemIndex === index ? { ...item, titleVi: value } : item) })} />
                      <ContractRichTextEditor
                        id={`acceptance-${selected.acceptanceRecordId}-${section.acceptanceSectionId}-vi`}
                        value={section.contentVi}
                        onChange={(value) => setSelected({ ...selected, sections: selected.sections.map((item, itemIndex) => itemIndex === index ? { ...item, contentVi: value } : item) })}
                        ariaLabel={`Nội dung ${section.titleVi}`}
                      />
                      {(section.titleEn != null || section.contentEn != null) && (
                        <>
                          <Field label="English title" value={section.titleEn ?? ""} onChange={(value) => setSelected({ ...selected, sections: selected.sections.map((item, itemIndex) => itemIndex === index ? { ...item, titleEn: value } : item) })} />
                          <ContractRichTextEditor
                            id={`acceptance-${selected.acceptanceRecordId}-${section.acceptanceSectionId}-en`}
                            value={section.contentEn}
                            onChange={(value) => setSelected({ ...selected, sections: selected.sections.map((item, itemIndex) => itemIndex === index ? { ...item, contentEn: value } : item) })}
                            ariaLabel={`English content ${section.titleEn ?? section.sectionCode}`}
                          />
                        </>
                      )}
                    </div>
                  ))}
                  <Button size="sm" onClick={() => void saveSections()} disabled={busy}>
                    <Save className="mr-2 size-4" /> Lưu nội dung
                  </Button>
                </section>
              </>
            ) : (
              <div className="space-y-4 text-sm">
                <div className="grid gap-3 md:grid-cols-2">
                  <div><span className="text-muted-foreground">Ngày nghiệm thu:</span> {new Date(selected.acceptanceDate).toLocaleDateString("vi-VN")}</div>
                  <div><span className="text-muted-foreground">Địa điểm:</span> {selected.location}</div>
                </div>
                <div className="space-y-2">
                  {selected.references.map((reference) => <p key={reference.acceptanceReferenceId} className="italic">{reference.textVi}</p>)}
                </div>
                {selected.snapshotHash && <p className="break-all text-xs text-muted-foreground">Snapshot SHA-256: {selected.snapshotHash}</p>}
              </div>
            )}

            {selected.status === AcceptanceRecordStatus.Finalized && canManage && (
              <div className="space-y-3 rounded-md border border-amber-300 bg-amber-50 p-4 dark:bg-amber-950/20">
                <b>Biên bản đã chốt và đang chờ bản ký</b>
                <div className="flex flex-col gap-2 sm:flex-row">
                  <Input type="file" accept=".pdf,.jpg,.jpeg,.png" onChange={(event) => setSignedFile(event.target.files?.[0])} />
                  <Button disabled={!signedFile || busy} onClick={() => signedFile && void (async () => {
                    try {
                      setBusy(true);
                      await contractAcceptanceApi.uploadSignedEvidence(contract.contractId, selected.acceptanceRecordId, signedFile, selected.rowVersion);
                      toast.success("Đã lưu bản ký biên bản nghiệm thu.");
                      setSignedFile(undefined);
                      setSelected(await contractAcceptanceApi.get(contract.contractId, selected.acceptanceRecordId));
                      setRecords(await contractAcceptanceApi.list(contract.contractId));
                      await onChanged();
                    } catch (error) {
                      toast.error(getApiErrorMessage(error, "Không thể tải bản ký."));
                    } finally {
                      setBusy(false);
                    }
                  })()}>
                    <FileSignature className="mr-2 size-4" /> Tải bản ký
                  </Button>
                </div>
              </div>
            )}
            {selected.status === AcceptanceRecordStatus.Signed && selected.signedEvidence && (
              <Alert>
                <CheckCircle2 className="size-4" />
                <AlertDescription>
                  Đã ký: {selected.signedEvidence.fileName} · {new Date(selected.signedEvidence.uploadedAt).toLocaleString("vi-VN")}
                </AlertDescription>
              </Alert>
            )}
            {selected.status === AcceptanceRecordStatus.Cancelled && (
              <Alert variant="destructive">
                <XCircle className="size-4" />
                <AlertDescription>{selected.cancellationReason}</AlertDescription>
              </Alert>
            )}
            {canManage && (selected.status === AcceptanceRecordStatus.Draft || selected.status === AcceptanceRecordStatus.Finalized) && (
              <Button variant="ghost" className="text-destructive" onClick={() => setCancelOpen(true)} disabled={busy}>
                Hủy biên bản
              </Button>
            )}
          </div>
        )}
      </CardContent>

      <Dialog open={createOpen} onOpenChange={setCreateOpen}>
        <DialogContent className="max-h-[90vh] max-w-3xl overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Tạo biên bản nghiệm thu · Bước {createStep}/3</DialogTitle>
            <DialogDescription>
              Hợp đồng gốc luôn là căn cứ bắt buộc; nội dung căn cứ do backend tự sinh.
            </DialogDescription>
          </DialogHeader>
          {createStep === 1 && (
            <div className="grid gap-4 md:grid-cols-2">
              <div className="space-y-2 md:col-span-2">
                <Label>Acceptance Template</Label>
                <select className="h-10 w-full rounded-md border bg-background px-3" value={createForm.templateVersionId} onChange={(event) => setCreateForm({ ...createForm, templateVersionId: Number(event.target.value) })}>
                  <option value={0}>Chọn template</option>
                  {templates.map((template) => <option key={template.templateVersionId} value={template.templateVersionId}>{template.templateName} · v{template.versionNo}</option>)}
                </select>
              </div>
              <Field label="Mã biên bản" value={createForm.acceptanceCode} onChange={(value) => setCreateForm({ ...createForm, acceptanceCode: value })} />
              <Field label="Ngày nghiệm thu" type="date" value={createForm.acceptanceDate} onChange={(value) => setCreateForm({ ...createForm, acceptanceDate: value })} />
              <div className="md:col-span-2"><Field label="Địa điểm" value={createForm.location} onChange={(value) => setCreateForm({ ...createForm, location: value })} /></div>
            </div>
          )}
          {createStep === 2 && (
            <div className="space-y-4">
              <label className="flex items-center gap-3 rounded-md border bg-muted/50 p-3 text-sm"><Checkbox checked disabled /> Hợp đồng {contract.contractCode} — căn cứ bắt buộc</label>
              {appendices.map((appendix) => <label key={appendix.appendixId} className="flex items-center gap-3 rounded-md border p-3 text-sm"><Checkbox checked={createForm.appendixIds.includes(appendix.appendixId)} onCheckedChange={(value) => setCreateForm({ ...createForm, appendixIds: value === true ? [...createForm.appendixIds, appendix.appendixId] : createForm.appendixIds.filter((id) => id !== appendix.appendixId) })} /> {appendix.appendixCode} — {appendix.appendixName}</label>)}
            </div>
          )}
          {createStep === 3 && (
            <div className="space-y-4">
              <div className="space-y-2">
                <Label>Loại nghiệm thu</Label>
                <select className="h-10 w-full rounded-md border bg-background px-3" value={createForm.acceptanceKind} onChange={(event) => setCreateForm({ ...createForm, acceptanceKind: Number(event.target.value) as AcceptanceKind, paymentMilestoneIds: [] })}>
                  <option value={AcceptanceKind.Final}>Nghiệm thu toàn bộ</option>
                  <option value={AcceptanceKind.Partial}>Nghiệm thu một phần</option>
                </select>
              </div>
              {createForm.acceptanceKind === AcceptanceKind.Partial && paymentMilestones.map((milestone) => <label key={milestone.paymentMilestoneId} className="flex items-center gap-3 rounded-md border p-3 text-sm"><Checkbox checked={createForm.paymentMilestoneIds.includes(milestone.paymentMilestoneId)} onCheckedChange={(value) => setCreateForm({ ...createForm, paymentMilestoneIds: value === true ? [...createForm.paymentMilestoneIds, milestone.paymentMilestoneId] : createForm.paymentMilestoneIds.filter((id) => id !== milestone.paymentMilestoneId) })} /> {milestone.milestoneCode} — {milestone.titleVi}</label>)}
              <Alert><AlertDescription>Sau khi tạo Draft, hệ thống snapshot hai bên và sections từ template. Bạn có thể chỉnh người tham gia và nội dung trước khi chốt.</AlertDescription></Alert>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => createStep === 1 ? setCreateOpen(false) : setCreateStep((step) => step - 1)}> {createStep === 1 ? "Hủy" : "Quay lại"} </Button>
            {createStep < 3 ? (
              <Button disabled={(createStep === 1 && (!createForm.templateVersionId || !createForm.acceptanceCode.trim() || !createForm.location.trim()))} onClick={() => setCreateStep((step) => step + 1)}>Tiếp tục</Button>
            ) : (
              <Button disabled={busy || (createForm.acceptanceKind === AcceptanceKind.Partial && createForm.paymentMilestoneIds.length === 0)} onClick={() => void createRecord()}>{busy && <Loader2 className="mr-2 size-4 animate-spin" />} Tạo Draft</Button>
            )}
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ConfirmDialog
        isOpen={finalizeOpen}
        onClose={() => setFinalizeOpen(false)}
        onConfirm={() => selected && void commit(() => contractAcceptanceApi.finalize(contract.contractId, selected.acceptanceRecordId, selected.rowVersion), "Đã chốt biên bản nghiệm thu.").then((result) => result && setFinalizeOpen(false))}
        title="Chốt biên bản nghiệm thu?"
        description="Hệ thống sẽ tạo và khóa DOCX/PDF chính thức. Sau bước này không thể sửa nội dung."
        confirmText="Chốt biên bản"
        isLoading={busy}
      />
      <Dialog open={cancelOpen} onOpenChange={setCancelOpen}>
        <DialogContent>
          <DialogHeader><DialogTitle>Hủy biên bản nghiệm thu</DialogTitle><DialogDescription>Record được giữ lại để audit và không thể dùng để hoàn tất hợp đồng.</DialogDescription></DialogHeader>
          <Textarea value={cancelReason} onChange={(event) => setCancelReason(event.target.value)} placeholder="Lý do hủy bắt buộc" />
          <DialogFooter><Button variant="outline" onClick={() => setCancelOpen(false)}>Đóng</Button><Button variant="destructive" disabled={!cancelReason.trim() || busy} onClick={() => selected && void commit(() => contractAcceptanceApi.cancel(contract.contractId, selected.acceptanceRecordId, cancelReason.trim(), selected.rowVersion), "Đã hủy biên bản.").then((result) => { if (result) { setCancelOpen(false); setCancelReason(""); } })}>Hủy biên bản</Button></DialogFooter>
        </DialogContent>
      </Dialog>
    </Card>
  );
}

function Field({ label, value, onChange, type = "text" }: { label: string; value: string; onChange: (value: string) => void; type?: string }) {
  return <div className="space-y-2"><Label>{label}</Label><Input type={type} value={value} onChange={(event) => onChange(event.target.value)} /></div>;
}

function PartyEditor({ title, party, onChange }: { title: string; party: ContractAcceptanceParty; onChange: (key: keyof ContractAcceptanceParty, value: string) => void }) {
  return (
    <div className="space-y-3 rounded-md border p-3">
      <b className="text-sm">{title}</b>
      <Field label="Tên pháp nhân" value={party.legalName} onChange={(value) => onChange("legalName", value)} />
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Người đại diện" value={party.representativeName} onChange={(value) => onChange("representativeName", value)} />
        <Field label="Chức vụ" value={party.representativeTitle ?? ""} onChange={(value) => onChange("representativeTitle", value)} />
      </div>
      <Field label="Địa chỉ" value={party.address ?? ""} onChange={(value) => onChange("address", value)} />
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Điện thoại" value={party.phone ?? ""} onChange={(value) => onChange("phone", value)} />
        <Field label="Fax" value={party.fax ?? ""} onChange={(value) => onChange("fax", value)} />
        <Field label="Mã số thuế" value={party.taxCode ?? ""} onChange={(value) => onChange("taxCode", value)} />
      </div>
    </div>
  );
}
