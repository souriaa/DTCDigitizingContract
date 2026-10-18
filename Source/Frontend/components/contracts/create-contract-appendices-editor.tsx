"use client";

import { useEffect, useRef, useState } from "react";
import { ArrowDown, ArrowUp, Minus, Plus } from "lucide-react";
import { AppendixCard } from "@/components/contract-documents/appendix-card";
import { TermsListEditor } from "@/components/contract-documents/terms-list-editor";
import { TermFields, type TermFormValue } from "@/components/contract-documents/term-fields";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { showConfirmToast } from "@/components/ui/custom/confirm-toast";
import { toast } from "@/components/ui/sonner";
import { createAppendixFromTemplate, getAppendixTermDraftError, type CreateAppendixDraft } from "@/lib/contract-appendix-draft";
import type { ContractTemplateAppendixResponse } from "@/services/contract-template-api";

const emptyTerm: TermFormValue = { code: "", title: "", titleEn: "", content: "", contentEn: "", bilingualContentMode: 1 };
const newClientId = () => `appendix-draft-${Date.now()}-${Math.random().toString(36).slice(2)}`;

export function CreateContractAppendicesEditor({ appendices, templates, isBilingual, onChange, onPendingFormChange }: {
  appendices: CreateAppendixDraft[];
  templates: ContractTemplateAppendixResponse[];
  isBilingual: boolean;
  onChange: (value: CreateAppendixDraft[]) => void;
  onPendingFormChange: (pending: boolean) => void;
}) {
  const draftsRef = useRef(appendices);
  draftsRef.current = appendices;
  const [addingTo, setAddingTo] = useState<string | null>(null);
  const [newTerm, setNewTerm] = useState<TermFormValue>(emptyTerm);
  const [openKeys, setOpenKeys] = useState<Set<string>>(new Set());
  useEffect(() => { onPendingFormChange(addingTo !== null); }, [addingTo, onPendingFormChange]);
  useEffect(() => () => onPendingFormChange(false), [onPendingFormChange]);
  const emit = (next: CreateAppendixDraft[]) => { draftsRef.current = next; onChange(next); };
  const update = (id: string, patch: Partial<CreateAppendixDraft>) => emit(draftsRef.current.map((item) => item.clientId === id ? { ...item, ...patch } : item));
  const updateTerms = (id: string, change: (terms: CreateAppendixDraft["terms"]) => CreateAppendixDraft["terms"]) => {
    const appendix = draftsRef.current.find((item) => item.clientId === id);
    if (appendix) update(id, { terms: change(appendix.terms).map((term, index) => ({ ...term, displayOrder: index + 1 })) });
  };
  const move = <T,>(items: T[], index: number, direction: -1 | 1) => {
    const next = [...items];
    const target = index + direction;
    if (target >= 0 && target < items.length) [next[index], next[target]] = [next[target], next[index]];
    return next;
  };
  const available = templates.filter((source) => !appendices.some((item) => item.sourceTemplateAppendixId === source.templateAppendixId));
  const addAppendix = () => {
    const codes = new Set(draftsRef.current.map((item) => item.appendixCode.trim().toUpperCase()));
    let number = 1;
    while (codes.has(`APPENDIX_CUSTOM_${number}`)) number++;
    const clientId = newClientId();
    emit([...draftsRef.current, { clientId, appendixCode: `APPENDIX_CUSTOM_${number}`, appendixName: "", isRequired: false, terms: [] }]);
    setOpenKeys((current) => new Set(current).add(clientId));
  };
  const openAddTerm = (appendix: CreateAppendixDraft) => {
    const largest = appendix.terms.reduce((value, term) => {
      const match = /^TERM_(\d+)$/i.exec(term.termCode.trim());
      const number = match ? Number(match[1]) : NaN;
      return Number.isSafeInteger(number) ? Math.max(value, number) : value;
    }, 0);
    let number = largest + 1;
    const codes = new Set(appendix.terms.map((term) => term.termCode.trim().toUpperCase()));
    while (codes.has(`TERM_${number}`)) number++;
    setNewTerm({ ...emptyTerm, code: `TERM_${number}` });
    setAddingTo(appendix.clientId);
  };
  const addTerm = () => {
    const appendix = draftsRef.current.find((item) => item.clientId === addingTo);
    if (!appendix) return;
    const term = {
      clientId: newClientId(), termCode: newTerm.code.trim().toUpperCase(), termTitle: newTerm.title.trim(),
      termTitleEn: newTerm.titleEn.trim(), termContent: newTerm.content, termContentEn: newTerm.contentEn,
      bilingualContentMode: newTerm.bilingualContentMode, displayOrder: appendix.terms.length + 1, isNegotiable: false,
    };
    const error = getAppendixTermDraftError([...appendix.terms, term], isBilingual, appendix.appendixCode);
    if (error) { toast.error(error); return; }
    updateTerms(appendix.clientId, (terms) => [...terms, term]);
    setAddingTo(null);
  };

  return <div className="space-y-4">
    <div className="flex flex-wrap items-center justify-between gap-3 rounded-xl border bg-muted/20 p-4">
      <div><p className="font-semibold">Phụ lục đính kèm ({appendices.length})</p><p className="text-sm text-muted-foreground">Thêm, sửa, xóa và sắp xếp phụ lục. Thay đổi được lưu cùng hợp đồng, không thay đổi mẫu gốc.</p></div>
      <Button type="button" onClick={addAppendix}><Plus className="size-4" />Tạo phụ lục mới</Button>
    </div>
    {available.length > 0 && <div className="space-y-2 rounded-xl border p-4">
      <p className="text-sm font-medium">Phụ lục từ mẫu có thể thêm</p>
      {available.map((source) => <div key={source.templateAppendixId} className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-sm">{source.appendixCode} — {source.appendixName}</span>
        <Button type="button" size="sm" variant="outline" onClick={() => {
          const draft = createAppendixFromTemplate(source);
          emit([...draftsRef.current, draft]); setOpenKeys((current) => new Set(current).add(draft.clientId));
        }}><Plus className="size-4" />Thêm phụ lục</Button>
      </div>)}
    </div>}
    {appendices.map((appendix, index) => <AppendixCard key={appendix.clientId} index={index}
      code={appendix.appendixCode} name={appendix.appendixName || "Phụ lục mới"} nameEn={isBilingual ? appendix.appendixNameEn : undefined}
      description={appendix.appendixDescription} required={appendix.isRequired} termCount={appendix.terms.length}
      open={openKeys.has(appendix.clientId)} onOpenChange={(open) => setOpenKeys((current) => { const next = new Set(current); if (open) next.add(appendix.clientId); else next.delete(appendix.clientId); return next; })}
      actions={<>
        <Button type="button" variant="ghost" size="icon" aria-label="Chuyển phụ lục lên" disabled={index === 0} onClick={() => emit(move(draftsRef.current, index, -1))}><ArrowUp className="size-4" /></Button>
        <Button type="button" variant="ghost" size="icon" aria-label="Chuyển phụ lục xuống" disabled={index === appendices.length - 1} onClick={() => emit(move(draftsRef.current, index, 1))}><ArrowDown className="size-4" /></Button>
        {!appendix.isRequired && <Button type="button" variant="outline" size="sm" onClick={() => showConfirmToast({
          title: "Rút phụ lục khỏi hợp đồng?", description: "Phụ lục và các điều khoản đang soạn sẽ được bỏ khỏi hợp đồng này. Mẫu gốc không thay đổi.",
          confirmLabel: "Rút phụ lục", cancelLabel: "Hủy", onConfirm: () => { emit(draftsRef.current.filter((item) => item.clientId !== appendix.clientId)); },
        })}><Minus className="size-4" />{appendix.sourceTemplateAppendixId ? "Rút phụ lục" : "Xóa phụ lục"}</Button>}
      </>}>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-2"><Label htmlFor={`${appendix.clientId}-code`}>Mã phụ lục <span className="text-destructive">*</span></Label><Input id={`${appendix.clientId}-code`} value={appendix.appendixCode} maxLength={50} onChange={(event) => update(appendix.clientId, { appendixCode: event.target.value })} /></div>
        <div className="space-y-2"><Label htmlFor={`${appendix.clientId}-name`}>Tên phụ lục <span className="text-destructive">*</span></Label><Input id={`${appendix.clientId}-name`} value={appendix.appendixName} maxLength={500} onChange={(event) => update(appendix.clientId, { appendixName: event.target.value })} /></div>
        {isBilingual && <div className="space-y-2 sm:col-span-2"><Label htmlFor={`${appendix.clientId}-name-en`}>Tên phụ lục tiếng Anh <span className="text-destructive">*</span></Label><Input id={`${appendix.clientId}-name-en`} value={appendix.appendixNameEn ?? ""} maxLength={500} onChange={(event) => update(appendix.clientId, { appendixNameEn: event.target.value })} /></div>}
        <div className="space-y-2 sm:col-span-2"><Label htmlFor={`${appendix.clientId}-description`}>Mô tả</Label><Textarea id={`${appendix.clientId}-description`} value={appendix.appendixDescription ?? ""} maxLength={2000} onChange={(event) => update(appendix.clientId, { appendixDescription: event.target.value })} /></div>
      </div>
      <TermsListEditor terms={appendix.terms} getKey={(term) => term.clientId} title="Điều khoản phụ lục"
        onAdd={() => openAddTerm(appendix)} getCardProps={(term, termIndex) => ({
          inputId: `create-appendix-${term.clientId}`, editable: true, isBilingual, showNegotiation: false,
          onChange: (field, value) => updateTerms(appendix.clientId, (terms) => terms.map((item) => item.clientId === term.clientId ? { ...item, [field]: value } : item)),
          onValueChange: (patch) => updateTerms(appendix.clientId, (terms) => terms.map((item) => item.clientId === term.clientId ? { ...item, ...patch } : item)),
          onMove: (direction) => updateTerms(appendix.clientId, (terms) => move(terms, termIndex, direction)),
          onRemove: () => showConfirmToast({ title: "Xóa điều khoản phụ lục?", description: `Điều khoản “${term.termTitle}” sẽ được bỏ khỏi phụ lục này.`, confirmLabel: "Xóa điều khoản", cancelLabel: "Hủy",
            onConfirm: () => { updateTerms(appendix.clientId, (terms) => terms.filter((item) => item.clientId !== term.clientId)); } }),
        })} />
    </AppendixCard>)}
    <Dialog open={addingTo !== null} onOpenChange={(open) => { if (!open) setAddingTo(null); }}>
      <DialogContent className="sm:max-w-3xl max-h-[85vh] overflow-y-auto"><DialogHeader><DialogTitle>Thêm điều khoản phụ lục</DialogTitle><DialogDescription>Điều khoản được lưu cùng hợp đồng mới, không thay đổi mẫu gốc.</DialogDescription></DialogHeader>
        <TermFields id={`create-appendix-new-${addingTo}`} value={newTerm} onChange={setNewTerm} isBilingual={isBilingual} />
        <DialogFooter><Button type="button" variant="outline" onClick={() => setAddingTo(null)}>Hủy</Button><Button type="button" onClick={addTerm}>Thêm điều khoản</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  </div>;
}
