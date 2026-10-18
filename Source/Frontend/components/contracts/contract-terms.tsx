"use client";

import { useState, useCallback, useRef } from "react";
import { MessageSquareText } from "lucide-react";
import { useContractDiscussionRealtime } from "@/hooks/use-contract-discussion-realtime";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { PaymentPlanEditor } from "@/components/contract-documents/payment-plan-editor";
import { ContractTermComments } from "./contract-term-comments";
import { CreateContractTermsEditor, type CreateContractTermDraft } from "./create-contract-terms-editor";
import { contractApi, ContractLanguageMode, ContractStatus, ContractTermKind,
  type ContractDetailResponse, type ContractNegotiationCommentResponse } from "@/services/contract-api";
import type { ContractPaymentMilestoneResponse } from "@/services/contract-completion-api";
import { allocateContractPaymentAmounts } from "@/lib/contract-finance";

export function ContractTerms({ contract, setContract, canEdit, canComment, onDraftChange }: {
  contract: ContractDetailResponse;
  setContract: React.Dispatch<React.SetStateAction<ContractDetailResponse | null>>;
  canEdit: boolean; canComment: boolean; onDraftChange?: () => void;
}) {
  const terms = contract.currentVersion.terms;
  const versionId = contract.currentVersion.versionId;
  const [discussionRevision, setDiscussionRevision] = useState(0);
  const nextLocalId = useRef(-Date.now());
  const refreshDiscussions = useCallback(async (signal: AbortSignal) => {
    const roots = await contractApi.getRootComments(contract.contractId, signal);
    if (signal.aborted) return;
    setContract((current) => {
      if (!current?.currentVersion || current.currentVersion.versionId !== versionId) return current;
      return { ...current, currentVersion: { ...current.currentVersion,
        comments: [...current.currentVersion.comments.filter((comment) => comment.parentCommentId),
          ...roots.filter((comment) => comment.versionId === versionId)] } };
    });
    setDiscussionRevision((revision) => revision + 1);
  }, [contract.contractId, setContract, versionId]);
  useContractDiscussionRealtime(`/api/contracts/${contract.contractId}/versions/${versionId}/discussion-hub`, refreshDiscussions);

  const handleCommentChanged = (changed: ContractNegotiationCommentResponse) => setContract((current) => {
    if (!current?.currentVersion || current.currentVersion.versionId !== versionId) return current;
    const comments = current.currentVersion.comments;
    return { ...current, currentVersion: { ...current.currentVersion,
      comments: comments.some((comment) => comment.commentId === changed.commentId)
        ? comments.map((comment) => comment.commentId === changed.commentId ? changed : comment) : [...comments, changed] } };
  });
  const comments = (termId: number, termCode?: string, termTitle?: string) => <ContractTermComments
    contractId={contract.contractId} versionId={versionId} termId={termId} termCode={termCode} termTitle={termTitle}
    comments={contract.currentVersion.comments} canWrite={canComment}
    onCommentChanged={handleCommentChanged} realtimeRevision={discussionRevision} />;
  const drafts: CreateContractTermDraft[] = terms.map((term) => ({ ...term, clientId: String(term.termId),
    paymentMilestones: term.termKind === ContractTermKind.Payment ? contract.currentVersion.paymentMilestones : [] }));
  const updateDrafts = (next: CreateContractTermDraft[]) => {
    const updatedTerms = next.map((draft) => {
      const existing = terms.find((term) => String(term.termId) === draft.clientId);
      return { ...existing, ...draft, termId: existing?.termId ?? --nextLocalId.current, rowVersion: existing?.rowVersion ?? "" };
    });
    const rows = next.flatMap((term) => term.termKind === ContractTermKind.Payment ? term.paymentMilestones : []);
    const amounts = allocateContractPaymentAmounts(contract.totalAmount, rows, contract.currencyCode);
    const paymentMilestones: ContractPaymentMilestoneResponse[] = rows.map((row, index) => ({
      paidAmount: 0, paymentStatus: 0, isOverdue: false,
      ...row, paymentMilestoneId: row.paymentMilestoneId ?? --nextLocalId.current,
      versionId, displayOrder: index + 1, rowVersion: row.rowVersion ?? "", amount: amounts[index], remainingAmount: amounts[index],
    }));
    onDraftChange?.();
    setContract((current) => !current?.currentVersion || current.currentVersion.versionId !== versionId ? current : ({
      ...current, currentVersion: { ...current.currentVersion, terms: updatedTerms, paymentMilestones },
    }));
  };
  const renderComments = (term: { termId: number; termCode: string; termTitle: string; isNegotiable: boolean }) =>
    term.isNegotiable && term.termId > 0 && (contract.status === ContractStatus.Negotiating ||
      contract.currentVersion.comments.some((comment) => comment.termId === term.termId))
      ? comments(term.termId, term.termCode, term.termTitle) : undefined;

  return <div className="space-y-4">
    <Card className="gap-0 p-0"><CardContent className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between sm:p-6">
      <div className="flex items-start gap-3"><div className="rounded-xl bg-primary/10 p-2.5 text-primary"><MessageSquareText className="size-5" /></div>
        <div><h2 className="font-semibold">Trao đổi chung</h2><p className="mt-1 text-sm text-muted-foreground">Thảo luận các nội dung áp dụng cho toàn bộ hợp đồng.</p></div>
      </div>
      <div className="flex items-center gap-2">{!canComment && <Badge variant="secondary">Chỉ xem</Badge>}
        <ContractTermComments contractId={contract.contractId} versionId={versionId} termId={null}
          comments={contract.currentVersion.comments} canWrite={canComment} onCommentChanged={handleCommentChanged}
          realtimeRevision={discussionRevision} triggerClassName="mt-0" />
      </div>
    </CardContent></Card>
    {canEdit ? <CreateContractTermsEditor key={versionId} terms={drafts} templateName="Điều khoản của phiên bản hiện tại"
      savedRevision={contract.currentVersion.rowVersion}
      isBilingual={contract.languageMode === ContractLanguageMode.Bilingual} onChange={updateDrafts}
      renderTermChildren={(draft) => { const term = terms.find((item) => String(item.termId) === draft.clientId); return term ? renderComments(term) : undefined; }} />
      : <TermsListEditor terms={terms} getKey={(term) => term.termId} title="Điều khoản hợp đồng"
        getCardProps={(term) => ({ inputId: `contract-${term.termId}`, isBilingual: contract.languageMode === ContractLanguageMode.Bilingual,
          children: <>{term.termKind === ContractTermKind.Payment && <PaymentPlanEditor rows={contract.currentVersion.paymentMilestones}
            isBilingual={contract.languageMode === ContractLanguageMode.Bilingual} editable={false} onChange={() => {}} />}{renderComments(term)}</> })} />}
  </div>;
}
