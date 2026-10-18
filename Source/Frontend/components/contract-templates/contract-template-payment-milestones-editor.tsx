"use client";

import { useEffect, useState } from "react";
import { ArrowDown, ArrowUp, Loader2, Plus, Save, Trash2, X } from "lucide-react";
import { toast } from "@/components/ui/sonner";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";




import { contractTemplateApi, PaymentDayCountMode, PaymentDueAnchor, type ContractTemplatePaymentMilestoneResponse, type ContractTemplateTermResponse, type ContractTemplateVersionDetailResponse, type SaveContractTemplatePaymentMilestoneRequest } from "@/services/contract-template-api";
import { getContractTemplateErrorMessage } from "./contract-template-utils";
import { PaymentMilestoneFields as Fields } from "@/components/contract-documents/payment-milestone-fields";

interface Props {
  version: ContractTemplateVersionDetailResponse;
  term: ContractTemplateTermResponse;
  isBilingual: boolean;
  editable: boolean;
  onRefresh: () => Promise<void>;
  drafts: Record<number, PaymentMilestoneDraft>;
  onDraftChange: (milestoneId: number, draft: PaymentMilestoneDraft) => void;
  onSaveAll: () => Promise<void>;
  savingAll: boolean;
  hasUnsavedChanges: boolean;
  onAddingChange: (adding: boolean) => void;
}

export type PaymentMilestoneDraft = Omit<
  SaveContractTemplatePaymentMilestoneRequest,
  "versionRowVersion"
>;
type Draft = PaymentMilestoneDraft;

const emptyDraft = (order: number): Draft => ({
  milestoneCode: `PAYMENT_${order}`,
  titleVi: `Đợt ${order}`,
  titleEn: "",
  paymentPercent: 0,
  dueAnchor:
    order === 1
      ? PaymentDueAnchor.ContractSigned
      : PaymentDueAnchor.PreviousMilestonePaid,
  dueOffsetDays: 0,
  dayCountMode: PaymentDayCountMode.CalendarDays,
  conditionVi: "",
  conditionEn: "",
  displayOrder: order,
});

const nextDraft = (rows: ContractTemplatePaymentMilestoneResponse[]): Draft => {
  const order = Math.max(0, ...rows.map((row) => row.displayOrder)) + 1;
  const existingCodes = new Set(
    rows.map((row) => row.milestoneCode.toUpperCase()),
  );
  let codeIndex = 1;
  while (existingCodes.has(`PAYMENT_${codeIndex}`)) codeIndex += 1;
  return {
    ...emptyDraft(order),
    milestoneCode: `PAYMENT_${codeIndex}`,
    titleVi: `Đợt ${rows.length + 1}`,
  };
};

export const paymentMilestoneDraftFromRow = (
  row: ContractTemplatePaymentMilestoneResponse,
): Draft => ({
  milestoneCode: row.milestoneCode,
  titleVi: row.titleVi,
  titleEn: row.titleEn ?? "",
  paymentPercent: row.paymentPercent,
  dueAnchor: row.dueAnchor,
  dueOffsetDays: row.dueOffsetDays,
  dayCountMode: row.dayCountMode,
  conditionVi: row.conditionVi ?? "",
  conditionEn: row.conditionEn ?? "",
  displayOrder: row.displayOrder,
});

export function ContractTemplatePaymentMilestonesEditor({
  version,
  term,
  isBilingual,
  editable,
  onRefresh,
  drafts,
  onDraftChange,
  onSaveAll,
  savingAll,
  hasUnsavedChanges,
  onAddingChange,
}: Props) {
  const rows = [...(term.paymentMilestones ?? [])].sort(
    (a, b) => a.displayOrder - b.displayOrder,
  );
  const [adding, setAdding] = useState(false);
  const [newDraft, setNewDraft] = useState<Draft>(() => nextDraft(rows));
  const [busy, setBusy] = useState<number | "new" | "order" | null>(null);
  useEffect(() => () => onAddingChange(false), [onAddingChange]);
  const changeAdding = (next: boolean) => {
    setAdding(next);
    onAddingChange(next);
  };
  const total =
    Math.round(
      rows.reduce(
        (sum, row) =>
          sum +
          (drafts[row.templatePaymentMilestoneId]?.paymentPercent ??
            row.paymentPercent),
        0,
      ) * 10_000,
    ) / 10_000;
  const totalValid = total === 100;
  const latestVersionRow = async () =>
    (await contractTemplateApi.getVersion(version.templateVersionId))
      .rowVersion;
  const payload = (draft: Draft, versionRowVersion: string) => ({
    ...draft,
    titleEn: draft.titleEn?.trim() || null,
    conditionVi: draft.conditionVi?.trim() || null,
    conditionEn: draft.conditionEn?.trim() || null,
    versionRowVersion,
  });

  const add = async () => {
    if (hasUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi hiện tại trước khi thêm đợt.");
      return;
    }
    if (
      !newDraft.milestoneCode.trim() ||
      !newDraft.titleVi.trim() ||
      newDraft.paymentPercent <= 0
    )
      return toast.error("Nhập đủ mã, tên và tỷ lệ đợt thanh toán.");
    try {
      setBusy("new");
      await contractTemplateApi.addPaymentMilestone(
        version.templateVersionId,
        term.templateTermId,
        payload(newDraft, await latestVersionRow()),
      );
      toast.success("Đã thêm đợt thanh toán.");
      changeAdding(false);
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setBusy(null);
    }
  };
  const remove = async (row: ContractTemplatePaymentMilestoneResponse) => {
    if (hasUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi hiện tại trước khi xóa đợt.");
      return;
    }
    try {
      setBusy(row.templatePaymentMilestoneId);
      await contractTemplateApi.deletePaymentMilestone(
        version.templateVersionId,
        term.templateTermId,
        row.templatePaymentMilestoneId,
        row.rowVersion,
        await latestVersionRow(),
      );
      toast.success("Đã xóa đợt thanh toán.");
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setBusy(null);
    }
  };
  const move = async (index: number, direction: -1 | 1) => {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= rows.length) return;
    if (hasUnsavedChanges) {
      toast.error("Vui lòng lưu các thay đổi hiện tại trước khi đổi thứ tự.");
      return;
    }
    const ordered = [...rows];
    [ordered[index], ordered[targetIndex]] = [
      ordered[targetIndex],
      ordered[index],
    ];
    try {
      setBusy("order");
      await contractTemplateApi.reorderPaymentMilestones(
        version.templateVersionId,
        term.templateTermId,
        await latestVersionRow(),
        ordered.map((row, orderIndex) => ({
          milestoneId: row.templatePaymentMilestoneId,
          rowVersion: row.rowVersion,
          displayOrder: orderIndex + 1,
        })),
      );
      toast.success("Đã đổi thứ tự đợt thanh toán.");
      await onRefresh();
    } catch (error) {
      toast.error(getContractTemplateErrorMessage(error));
      await onRefresh();
    } finally {
      setBusy(null);
    }
  };

  return (
    <div
      data-template-payment-editor
      className="space-y-3 rounded-xl border bg-muted/20 p-3"
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="font-medium">Theo dõi các đợt thanh toán</p>
          <p className="text-sm text-muted-foreground">
            Tổng tỷ lệ:{" "}
            <strong
              className={totalValid ? "text-emerald-600" : "text-destructive"}
            >
              {total}%
            </strong>
          </p>
        </div>
        {editable && (
          <Button
            size="sm"
            variant="outline"
            onClick={() => {
              if (hasUnsavedChanges) {
                toast.error(
                  "Vui lòng lưu các thay đổi hiện tại trước khi thêm đợt.",
                );
              } else {
                changeAdding(true);
              }
            }}
          >
            <Plus className="size-4" /> Thêm đợt
          </Button>
        )}
      </div>
      {!totalValid && (
        <Alert variant="destructive">
          <AlertTitle>Chưa thể phát hành</AlertTitle>
          <AlertDescription>
            Tổng tỷ lệ các đợt phải đúng 100%.
          </AlertDescription>
        </Alert>
      )}
      {rows.map((row, index) => (
        <div
          key={row.templatePaymentMilestoneId}
          className="space-y-3 rounded-lg border bg-background p-3"
        >
          <Fields
            value={
              drafts[row.templatePaymentMilestoneId] ??
              paymentMilestoneDraftFromRow(row)
            }
            onChange={(next) =>
              onDraftChange(row.templatePaymentMilestoneId, next)
            }
            isBilingual={isBilingual}
            disabled={!editable}
            codeDisabled
          />
          <div className="flex justify-end gap-2">
            {editable && (
              <>
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  aria-label="Chuyển đợt thanh toán lên"
                  onClick={() => move(index, -1)}
                  disabled={busy !== null || index === 0}
                >
                  <ArrowUp className="size-4" />
                </Button>
                <Button
                  type="button"
                  size="icon"
                  variant="outline"
                  aria-label="Chuyển đợt thanh toán xuống"
                  onClick={() => move(index, 1)}
                  disabled={busy !== null || index === rows.length - 1}
                >
                  <ArrowDown className="size-4" />
                </Button>
                <Button
                  size="sm"
                  variant="destructive"
                  onClick={() => remove(row)}
                  disabled={busy !== null}
                >
                  <Trash2 className="size-4" /> Xóa
                </Button>
                {/* <Button
                  size="sm"
                  onClick={() => void onSaveAll()}
                  disabled={busy !== null || savingAll || !hasUnsavedChanges}
                >
                  {savingAll ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    <Save className="size-4" />
                  )}{" "}
                  Lưu thay đổi
                </Button> */}
              </>
            )}
          </div>
        </div>
      ))}
      {rows.length === 0 && !adding && (
        <p className="py-4 text-center text-sm text-muted-foreground">
          Chưa có đợt thanh toán.
        </p>
      )}
      {adding && (
        <div className="space-y-3 rounded-lg border border-dashed bg-background p-3">
          <Fields
            value={newDraft}
            onChange={setNewDraft}
            isBilingual={isBilingual}
          />
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => changeAdding(false)}>
              <X className="size-4" /> Hủy
            </Button>
            <Button onClick={add} disabled={busy !== null}>
              {busy === "new" && <Loader2 className="size-4 animate-spin" />}{" "}
              Thêm đợt
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
