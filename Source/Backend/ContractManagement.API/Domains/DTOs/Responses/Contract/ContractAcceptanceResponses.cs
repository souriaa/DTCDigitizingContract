using ContractManagement.API.Common.Enums;

namespace ContractManagement.API.Domains.DTOs.Responses.Contract;

public sealed class ContractAcceptanceTemplateOptionResponse
{
    public int TemplateId { get; set; }
    public int TemplateVersionId { get; set; }
    public int VersionNo { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public ContractLanguageMode LanguageMode { get; set; }
}

public class ContractAcceptanceRecordSummaryResponse
{
    public int AcceptanceRecordId { get; set; }
    public int ContractVersionId { get; set; }
    public int VersionNo { get; set; }
    public string AcceptanceCode { get; set; } = string.Empty;
    public DateTime AcceptanceDate { get; set; }
    public AcceptanceKind AcceptanceKind { get; set; }
    public AcceptanceRecordStatus Status { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ContractAcceptanceRecordResponse
    : ContractAcceptanceRecordSummaryResponse
{
    public int ContractId { get; set; }
    public int TemplateVersionId { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string? SnapshotHash { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public IReadOnlyList<ContractAcceptanceReferenceResponse> References { get; set; } = [];
    public IReadOnlyList<ContractAcceptancePartyResponse> Parties { get; set; } = [];
    public IReadOnlyList<ContractAcceptanceSectionResponse> Sections { get; set; } = [];
    public IReadOnlyList<ContractAcceptanceMilestoneResponse> Milestones { get; set; } = [];
    public IReadOnlyList<ContractAcceptanceArtifactResponse> Artifacts { get; set; } = [];
    public ContractAcceptanceEvidenceResponse? SignedEvidence { get; set; }
}

public sealed class ContractAcceptanceReferenceResponse
{
    public int AcceptanceReferenceId { get; set; }
    public AcceptanceReferenceType ReferenceType { get; set; }
    public int? AppendixId { get; set; }
    public string TextVi { get; set; } = string.Empty;
    public string? TextEn { get; set; }
    public int DisplayOrder { get; set; }
}

public sealed class ContractAcceptancePartyResponse
{
    public int AcceptancePartyId { get; set; }
    public AcceptancePartyRole PartyRole { get; set; }
    public string LegalName { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? TaxCode { get; set; }
    public string RepresentativeName { get; set; } = string.Empty;
    public string? RepresentativeTitle { get; set; }
}

public sealed class ContractAcceptanceSectionResponse
{
    public int AcceptanceSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
    public string? ContentVi { get; set; }
    public string? ContentEn { get; set; }
    public int DisplayOrder { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class ContractAcceptanceMilestoneResponse
{
    public int AcceptanceMilestoneId { get; set; }
    public int PaymentMilestoneId { get; set; }
    public string MilestoneCode { get; set; } = string.Empty;
    public string TitleVi { get; set; } = string.Empty;
    public string? TitleEn { get; set; }
}

public sealed class ContractAcceptanceArtifactResponse
{
    public int FileId { get; set; }
    public string Format { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string Sha256 { get; set; } = string.Empty;
}

public sealed record ContractAcceptanceDocumentContent(
    byte[] Content,
    string FileName,
    string ContentType);
