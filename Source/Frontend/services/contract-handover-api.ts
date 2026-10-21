import axiosClient from "@/lib/axios-interceptor";
import type { ContractDirection } from "@/services/contract-api";
import type {
  ContractAcceptanceArtifact,
  ContractAcceptancePartyInput,
} from "@/services/contract-acceptance-api";

export enum HandoverStatus {
  Draft = 0,
  Finalized = 1,
  Signed = 2,
  Cancelled = 3,
}
export enum HandoverScopeMode {
  Quantity = 1,
  WholePackage = 2,
}
export interface HandoverScopeCandidate {
  contractItemId: number;
  nameVi: string;
  nameEn?: string | null;
  unitVi?: string | null;
  unitEn?: string | null;
  quantity: number;
}
export interface HandoverScopeItem {
  handoverScopeItemId: number;
  sourceContractItemId: number;
  code: string;
  nameVi: string;
  nameEn?: string | null;
  unitVi: string;
  unitEn?: string | null;
  mode: HandoverScopeMode;
  requiredQuantity: number;
  delivered: number;
  reserved: number;
  remaining: number;
  accepted: number;
  acceptanceReserved: number;
  sourceQuantity: number;
}
export interface HandoverReadiness {
  contractVersionId: number;
  baselineId?: number | null;
  completed: boolean;
  canCreate: boolean;
  blockerCode?: string | null;
  blockerMessage?: string | null;
  activeRecordId?: number | null;
  items: HandoverScopeItem[];
}
export interface HandoverTemplate {
  templateVersionId: number;
  templateName: string;
  versionNo: number;
  contractDirection: ContractDirection;
  languageMode: number;
}
export interface HandoverSummary {
  handoverRecordId: number;
  contractVersionId: number;
  handoverCode: string;
  handoverDate: string;
  status: HandoverStatus;
  rowVersion: string;
}
export interface HandoverLineInput {
  handoverScopeItemId: number;
  quantity: number;
  notesVi?: string | null;
  notesEn?: string | null;
}
export interface HandoverLine extends HandoverLineInput {
  handoverLineId: number;
  code: string;
  nameVi: string;
  nameEn?: string | null;
  unitVi: string;
  unitEn?: string | null;
}
export interface HandoverParty extends ContractAcceptancePartyInput {
  subjectKind: number;
}
export interface HandoverSection {
  sectionCode: string;
  titleVi: string;
  titleEn?: string | null;
  contentVi?: string | null;
  contentEn?: string | null;
  bilingualContentMode: number;
}
export interface HandoverEvidence {
  handoverEvidenceId: number;
  fileId: number;
  fileName: string;
  signDate: string;
  uploadedByEmployeeName?: string | null;
  uploadedAt: string;
}
export interface HandoverDetail {
  record: HandoverSummary;
  contractVersionNo: number;
  templateVersionNo: number;
  templateVersionId: number;
  templateName: string;
  contractDirection: ContractDirection;
  languageMode: number;
  contractSignDate: string;
  location: string;
  locationEn?: string | null;
  snapshotHash?: string | null;
  createdByEmployeeName?: string | null;
  createdAt: string;
  finalizedByEmployeeName?: string | null;
  finalizedAt?: string | null;
  cancelledByEmployeeName?: string | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
  parties: HandoverParty[];
  sections: HandoverSection[];
  appendixIds: number[];
  references: { textVi: string; textEn?: string | null }[];
  lines: HandoverLine[];
  artifacts: ContractAcceptanceArtifact[];
  signedEvidence?: HandoverEvidence | null;
}
export interface HandoverMetadata {
  handoverCode: string;
  handoverDate: string;
  location: string;
  locationEn?: string | null;
  appendixIds: number[];
  lines: HandoverLineInput[];
}
export interface HandoverCreate extends HandoverMetadata {
  contractVersionId: number;
  templateVersionId: number;
  contractRowVersion: string;
  versionRowVersion: string;
}
export interface HandoverUpdate extends HandoverMetadata {
  rowVersion: string;
  parties: HandoverParty[];
  sections: HandoverSection[];
}
const url = (contractId: number) =>
  `/api/contracts/${contractId}/handover-records`;
export const contractHandoverApi = {
  list: (id: number) => axiosClient.get<unknown, HandoverSummary[]>(url(id)),
  readiness: (id: number) =>
    axiosClient.get<unknown, HandoverReadiness>(`${url(id)}/readiness`),
  templates: (id: number) =>
    axiosClient.get<unknown, HandoverTemplate[]>(`${url(id)}/templates`),
  candidates: (id: number) =>
    axiosClient.get<unknown, HandoverScopeCandidate[]>(
      `${url(id)}/scope/candidates`,
    ),
  confirmScope: (
    id: number,
    value: {
      contractVersionId: number;
      contractRowVersion: string;
      versionRowVersion: string;
      items: { contractItemId: number; mode: HandoverScopeMode }[];
    },
  ) =>
    axiosClient.post<unknown, HandoverReadiness>(
      `${url(id)}/scope/confirm`,
      value,
    ),
  get: (id: number, recordId: number) =>
    axiosClient.get<unknown, HandoverDetail>(`${url(id)}/${recordId}`),
  create: (id: number, value: HandoverCreate) =>
    axiosClient.post<unknown, HandoverDetail>(url(id), value),
  update: (id: number, recordId: number, value: HandoverUpdate) =>
    axiosClient.put<unknown, HandoverDetail>(`${url(id)}/${recordId}`, value),
  finalize: (id: number, recordId: number, rowVersion: string) =>
    axiosClient.post<unknown, HandoverDetail>(
      `${url(id)}/${recordId}/finalize`,
      { rowVersion },
    ),
  cancel: (id: number, recordId: number, rowVersion: string, reason: string) =>
    axiosClient.post<unknown, HandoverDetail>(`${url(id)}/${recordId}/cancel`, {
      rowVersion,
      reason,
    }),
  sign: (
    id: number,
    recordId: number,
    file: File,
    rowVersion: string,
    signDate: string,
  ) => {
    const data = new FormData();
    data.append("file", file);
    data.append("rowVersion", rowVersion);
    data.append("signDate", signDate);
    data.append("confirmBothPartiesSigned", "true");
    return axiosClient.post<unknown, HandoverDetail>(
      `${url(id)}/${recordId}/signed-evidence`,
      data,
      { headers: { "Content-Type": "multipart/form-data" } },
    );
  },
  document: (
    id: number,
    recordId: number,
    format: "docx" | "pdf",
    draft: boolean,
  ) =>
    axiosClient.get<unknown, Blob>(
      `${url(id)}/${recordId}/${draft ? "preview" : "artifacts"}/${format}`,
      { responseType: "blob" },
    ),
  evidence: (id: number, recordId: number) =>
    axiosClient.get<unknown, Blob>(`${url(id)}/${recordId}/signed-evidence`, {
      responseType: "blob",
    }),
};
