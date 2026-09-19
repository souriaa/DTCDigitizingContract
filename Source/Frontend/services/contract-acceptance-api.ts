import axiosClient from "@/lib/axios-interceptor";

const BASE_URL = "/api/contracts";

export enum AcceptanceRecordStatus {
  Draft = 0,
  Finalized = 1,
  Signed = 2,
  Cancelled = 3,
}

export enum AcceptanceKind {
  Partial = 1,
  Final = 2,
}

export enum AcceptanceReferenceType {
  Contract = 1,
  Appendix = 2,
}

export enum AcceptancePartyRole {
  Customer = 1,
  Tenant = 2,
}

export interface ContractAcceptanceTemplateOption {
  templateId: number;
  templateVersionId: number;
  versionNo: number;
  templateCode: string;
  templateName: string;
  languageMode: number;
}

export interface ContractAcceptanceRecordSummary {
  acceptanceRecordId: number;
  contractVersionId: number;
  versionNo: number;
  acceptanceCode: string;
  acceptanceDate: string;
  acceptanceKind: AcceptanceKind;
  status: AcceptanceRecordStatus;
  rowVersion: string;
}

export interface ContractAcceptanceReference {
  acceptanceReferenceId: number;
  referenceType: AcceptanceReferenceType;
  appendixId?: number | null;
  textVi: string;
  textEn?: string | null;
  displayOrder: number;
}

export interface ContractAcceptanceParty {
  acceptancePartyId: number;
  partyRole: AcceptancePartyRole;
  legalName: string;
  address?: string | null;
  phone?: string | null;
  fax?: string | null;
  taxCode?: string | null;
  representativeName: string;
  representativeTitle?: string | null;
}

export interface ContractAcceptanceSection {
  acceptanceSectionId: number;
  sectionCode: string;
  titleVi: string;
  titleEn?: string | null;
  contentVi?: string | null;
  contentEn?: string | null;
  displayOrder: number;
  rowVersion: string;
}

export interface ContractAcceptanceMilestone {
  acceptanceMilestoneId: number;
  paymentMilestoneId: number;
  milestoneCode: string;
  titleVi: string;
  titleEn?: string | null;
}

export interface ContractAcceptanceArtifact {
  fileId: number;
  format: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  sha256: string;
}

export interface ContractAcceptanceSignedEvidence {
  acceptanceEvidenceId: number;
  contractId: number;
  versionId: number;
  versionNo: number;
  fileId: number;
  fileName: string;
  fileType: string;
  contentType: string;
  fileSize: number;
  sha256: string;
  uploadedByEmployeeId: number;
  uploadedByEmployeeName?: string | null;
  uploadedAt: string;
  rowVersion: string;
}

export interface ContractAcceptanceRecord
  extends ContractAcceptanceRecordSummary {
  contractId: number;
  templateVersionId: number;
  templateName: string;
  location: string;
  snapshotHash?: string | null;
  finalizedAt?: string | null;
  signedAt?: string | null;
  cancelledAt?: string | null;
  cancellationReason?: string | null;
  references: ContractAcceptanceReference[];
  parties: ContractAcceptanceParty[];
  sections: ContractAcceptanceSection[];
  milestones: ContractAcceptanceMilestone[];
  artifacts: ContractAcceptanceArtifact[];
  signedEvidence?: ContractAcceptanceSignedEvidence | null;
}

export interface ContractAcceptancePartyInput {
  legalName: string;
  address?: string | null;
  phone?: string | null;
  fax?: string | null;
  taxCode?: string | null;
  representativeName: string;
  representativeTitle?: string | null;
}

const multipart = (file: File, rowVersion: string) => {
  const data = new FormData();
  data.append("file", file);
  data.append("rowVersion", rowVersion);
  return data;
};

const url = (contractId: number) =>
  `${BASE_URL}/${contractId}/acceptance-records`;

export const contractAcceptanceApi = {
  templates: (contractId: number) =>
    axiosClient.get<unknown, ContractAcceptanceTemplateOption[]>(
      `${url(contractId)}/templates`,
    ),
  list: (contractId: number) =>
    axiosClient.get<unknown, ContractAcceptanceRecordSummary[]>(url(contractId)),
  get: (contractId: number, recordId: number) =>
    axiosClient.get<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}`,
    ),
  create: (
    contractId: number,
    values: {
      contractVersionId: number;
      templateVersionId: number;
      acceptanceCode: string;
      acceptanceDate: string;
      location: string;
      acceptanceKind: AcceptanceKind;
      appendixIds: number[];
      paymentMilestoneIds: number[];
      contractRowVersion: string;
      versionRowVersion: string;
    },
  ) =>
    axiosClient.post<unknown, ContractAcceptanceRecord>(url(contractId), values),
  update: (
    contractId: number,
    recordId: number,
    values: {
      acceptanceCode: string;
      acceptanceDate: string;
      location: string;
      acceptanceKind: AcceptanceKind;
      customerParty: ContractAcceptancePartyInput;
      tenantParty: ContractAcceptancePartyInput;
      rowVersion: string;
    },
  ) =>
    axiosClient.put<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}`,
      values,
    ),
  replaceReferences: (
    contractId: number,
    recordId: number,
    appendixIds: number[],
    rowVersion: string,
  ) =>
    axiosClient.put<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}/references`,
      { appendixIds, rowVersion },
    ),
  replaceSections: (
    contractId: number,
    recordId: number,
    sections: Array<{
      sectionCode: string;
      titleVi: string;
      titleEn?: string | null;
      contentVi?: string | null;
      contentEn?: string | null;
      displayOrder: number;
    }>,
    rowVersion: string,
  ) =>
    axiosClient.put<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}/sections`,
      { sections, rowVersion },
    ),
  replaceMilestones: (
    contractId: number,
    recordId: number,
    paymentMilestoneIds: number[],
    rowVersion: string,
  ) =>
    axiosClient.put<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}/milestones`,
      { paymentMilestoneIds, rowVersion },
    ),
  preview: (contractId: number, recordId: number, format: "docx" | "pdf") =>
    axiosClient.get<unknown, Blob>(
      `${url(contractId)}/${recordId}/preview/${format}`,
      { responseType: "blob" },
    ),
  finalize: (contractId: number, recordId: number, rowVersion: string) =>
    axiosClient.post<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}/finalize`,
      { rowVersion },
    ),
  uploadSignedEvidence: (
    contractId: number,
    recordId: number,
    file: File,
    rowVersion: string,
  ) =>
    axiosClient.post<unknown, ContractAcceptanceSignedEvidence>(
      `${url(contractId)}/${recordId}/signed-evidence`,
      multipart(file, rowVersion),
      { headers: { "Content-Type": "multipart/form-data" } },
    ),
  cancel: (
    contractId: number,
    recordId: number,
    reason: string,
    rowVersion: string,
  ) =>
    axiosClient.post<unknown, ContractAcceptanceRecord>(
      `${url(contractId)}/${recordId}/cancel`,
      { reason, rowVersion },
    ),
  artifact: (contractId: number, recordId: number, format: "docx" | "pdf") =>
    axiosClient.get<unknown, Blob>(
      `${url(contractId)}/${recordId}/artifacts/${format}`,
      { responseType: "blob" },
    ),
};
