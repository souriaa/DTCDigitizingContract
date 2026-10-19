using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
namespace ContractManagement.API.Domains.DTOs.Responses.Contract;
public sealed record HandoverScopeCandidateResponse(int ContractItemId, string NameVi, string? NameEn, string? UnitVi, string? UnitEn, decimal Quantity);
public sealed record HandoverScopeItemResponse(long HandoverScopeItemId, int SourceContractItemId, string Code,
    string NameVi, string? NameEn, string UnitVi, string? UnitEn, HandoverScopeMode Mode,
    decimal RequiredQuantity, decimal Delivered, decimal Reserved, decimal Remaining, decimal Accepted, decimal AcceptanceReserved, decimal SourceQuantity);
public sealed record HandoverReadinessResponse(int ContractVersionId, long? BaselineId,
    bool Completed, bool CanCreate, string? BlockerCode, string? BlockerMessage, int? ActiveRecordId,
    IReadOnlyList<HandoverScopeItemResponse> Items);
public sealed record HandoverTemplateOption(int TemplateVersionId, string TemplateName, int VersionNo,
    ContractDirection ContractDirection, ContractLanguageMode LanguageMode);
public sealed record HandoverRecordSummary(int HandoverRecordId, int ContractVersionId, string HandoverCode,
    DateTime HandoverDate, HandoverRecordStatus Status, string RowVersion);
public sealed record HandoverLineResponse(int HandoverLineId, long HandoverScopeItemId, decimal Quantity,
    string Code, string NameVi, string? NameEn, string UnitVi, string? UnitEn, string? NotesVi, string? NotesEn);
public sealed record HandoverEvidenceResponse(int HandoverEvidenceId, int FileId, string FileName, DateTime SignDate,
    int UploadedByEmployeeId, string? UploadedByEmployeeName, DateTime UploadedAt);
public sealed class HandoverDetailResponse
{
    public HandoverRecordSummary Record { get; set; } = null!;
    public int ContractVersionNo { get; set; }
    public int TemplateVersionNo { get; set; }
    public int TemplateVersionId { get; set; }
    public string TemplateName { get; set; } = "";
    public ContractDirection ContractDirection { get; set; }
    public ContractLanguageMode LanguageMode { get; set; }
    public DateTime ContractSignDate { get; set; }
    public string Location { get; set; } = "";
    public string? LocationEn { get; set; }
    public string? SnapshotHash { get; set; }
    public string? CreatedByEmployeeName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? FinalizedByEmployeeName { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public string? CancelledByEmployeeName { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public IReadOnlyList<ContractManagement.API.Domains.DTOs.Requests.Contract.HandoverPartyInput> Parties { get; set; } = [];
    public IReadOnlyList<ContractManagement.API.Domains.DTOs.Requests.Contract.HandoverSectionInput> Sections { get; set; } = [];
    public IReadOnlyList<int> AppendixIds { get; set; } = [];
    public IReadOnlyList<ContractTemplateRenderReference> References { get; set; } = [];
    public IReadOnlyList<HandoverLineResponse> Lines { get; set; } = [];
    public IReadOnlyList<ContractAcceptanceArtifactResponse> Artifacts { get; set; } = [];
    public HandoverEvidenceResponse? SignedEvidence { get; set; }
}
