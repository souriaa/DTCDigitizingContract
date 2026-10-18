import type { CreateContractAppendixDraftRequest, CreateContractAppendixTermDraftRequest } from "@/services/contract-api";
import type { ContractTemplateAppendixResponse } from "@/services/contract-template-api";

export interface CreateAppendixTermDraft extends CreateContractAppendixTermDraftRequest {
  clientId: string;
  displayOrder: number;
  isNegotiable: boolean;
}

export interface CreateAppendixDraft extends Omit<CreateContractAppendixDraftRequest, "terms"> {
  clientId: string;
  isRequired: boolean;
  terms: CreateAppendixTermDraft[];
}

export function createAppendixFromTemplate(source: ContractTemplateAppendixResponse): CreateAppendixDraft {
  return {
    clientId: `template-appendix-${source.templateAppendixId}`,
    sourceTemplateAppendixId: source.templateAppendixId,
    appendixCode: source.appendixCode,
    appendixName: source.appendixName,
    appendixNameEn: source.appendixNameEn,
    appendixDescription: source.appendixDescription,
    isRequired: source.isRequired,
    terms: [...source.terms].sort((a, b) => a.displayOrder - b.displayOrder).map((term, index) => ({
      clientId: `template-appendix-term-${term.templateAppendixTermId}`,
      sourceTemplateAppendixTermId: term.templateAppendixTermId,
      termCode: term.termCode, termTitle: term.termTitle, termTitleEn: term.termTitleEn,
      termContent: term.termContent, termContentEn: term.termContentEn,
      bilingualContentMode: term.bilingualContentMode,
      displayOrder: index + 1, isNegotiable: false,
    })),
  };
}

export function getAppendixDraftError(appendices: CreateAppendixDraft[], isBilingual: boolean): string | null {
  const codes = new Set<string>();
  for (const appendix of appendices) {
    const code = appendix.appendixCode.trim().toUpperCase();
    if (!code || !appendix.appendixName.trim()) return "Mã và tên phụ lục không được để trống.";
    if (codes.has(code)) return `Mã phụ lục '${code}' bị trùng.`;
    codes.add(code);
    if (isBilingual && !appendix.appendixNameEn?.trim()) return `Phụ lục '${code}' phải có tên tiếng Anh.`;
    if (!appendix.terms.length) return `Phụ lục '${code}' phải có ít nhất một điều khoản.`;
    const termError = getAppendixTermDraftError(appendix.terms, isBilingual, code);
    if (termError) return termError;
  }
  return null;
}

export function getAppendixTermDraftError(terms: CreateAppendixTermDraft[], isBilingual: boolean, code: string): string | null {
    const termCodes = new Set<string>();
    for (const term of terms) {
      const termCode = term.termCode.trim().toUpperCase();
      if (!termCode || !term.termTitle.trim()) return `Phụ lục '${code}': mã và tiêu đề điều khoản không được để trống.`;
      if (termCodes.has(termCode)) return `Phụ lục '${code}': mã điều khoản '${termCode}' bị trùng.`;
      termCodes.add(termCode);
      if (isBilingual && !term.termTitleEn?.trim()) return `Phụ lục '${code}' — điều khoản '${termCode}': thiếu tiêu đề tiếng Anh.`;
      if (!isBilingual && term.bilingualContentMode === 2) return `Phụ lục '${code}': chỉ hợp đồng song ngữ được gộp nội dung.`;
      if (isBilingual && term.bilingualContentMode === 1 && Boolean(term.termContent?.trim()) !== Boolean(term.termContentEn?.trim()))
        return `Phụ lục '${code}' — điều khoản '${termCode}': cần đủ nội dung tiếng Anh và tiếng Việt trong chế độ tách riêng.`;
    }
  return null;
}

export function toCreateAppendixRequest(appendix: CreateAppendixDraft): CreateContractAppendixDraftRequest {
  return {
    sourceTemplateAppendixId: appendix.sourceTemplateAppendixId ?? null,
    appendixCode: appendix.appendixCode.trim().toUpperCase(),
    appendixName: appendix.appendixName.trim(),
    appendixNameEn: appendix.appendixNameEn?.trim() || null,
    appendixDescription: appendix.appendixDescription?.trim() || null,
    terms: appendix.terms.map((term) => ({
      sourceTemplateAppendixTermId: term.sourceTemplateAppendixTermId ?? null,
      termCode: term.termCode.trim().toUpperCase(), termTitle: term.termTitle.trim(),
      termTitleEn: term.termTitleEn?.trim() || null,
      termContent: term.termContent || null, termContentEn: term.termContentEn || null,
      bilingualContentMode: term.bilingualContentMode,
    })),
  };
}
