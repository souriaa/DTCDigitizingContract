using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract;

public sealed class CreateContractAcceptanceRecordRequest
{
    [Range(1, int.MaxValue)] public int ContractVersionId { get; set; }
    [Range(1, int.MaxValue)] public int TemplateVersionId { get; set; }
    [Required, MaxLength(100)] public string AcceptanceCode { get; set; } = string.Empty;
    public DateTime AcceptanceDate { get; set; }
    [Required, MaxLength(500)] public string Location { get; set; } = string.Empty;
    public string? LocationEn { get; set; }
    public AcceptanceKind AcceptanceKind { get; set; }
    public List<int> AppendixIds { get; set; } = [];
    public List<int> PaymentMilestoneIds { get; set; } = [];
    [Required] public string ContractRowVersion { get; set; } = string.Empty;
    [Required] public string VersionRowVersion { get; set; } = string.Empty;
}
public sealed record AcceptanceHandoverScopeInput(int HandoverLineId, decimal Quantity);
public sealed class ReplaceAcceptanceHandoverScopeRequest
{
    public List<AcceptanceHandoverScopeInput> Items { get; set; } = [];
    [Required] public string RowVersion { get; set; } = string.Empty;
}


public sealed class UpdateContractAcceptanceRecordRequest
{
    [Required, MaxLength(100)] public string AcceptanceCode { get; set; } = string.Empty;
    public DateTime AcceptanceDate { get; set; }
    [Required, MaxLength(500)] public string Location { get; set; } = string.Empty;
    public string? LocationEn { get; set; }
    public AcceptanceKind AcceptanceKind { get; set; }
    [Required] public ContractAcceptancePartyInput CustomerParty { get; set; } = new();
    [Required] public ContractAcceptancePartyInput TenantParty { get; set; } = new();
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ContractAcceptancePartyInput
{
    [MaxLength(500)] public string? LegalNameEn { get; set; }
    [MaxLength(2000)] public string? AddressEn { get; set; }
    [MaxLength(300)] public string? RepresentativeTitleEn { get; set; }
    [Required, MaxLength(500)] public string LegalName { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(50)] public string? Fax { get; set; }
    [MaxLength(50)] public string? TaxCode { get; set; }
    [Required, MaxLength(300)] public string RepresentativeName { get; set; } = string.Empty;
    [MaxLength(300)] public string? RepresentativeTitle { get; set; }
}

public sealed class ReplaceContractAcceptanceReferencesRequest
{
    public List<int> AppendixIds { get; set; } = [];
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ReplaceContractAcceptanceSectionsRequest
{
    public List<ContractAcceptanceSectionInput> Sections { get; set; } = [];
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ContractAcceptanceSectionInput
{
    [Required, MaxLength(100)] public string SectionCode { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string TitleVi { get; set; } = string.Empty;
    [MaxLength(1000)] public string? TitleEn { get; set; }
    public string? ContentVi { get; set; }
    public string? ContentEn { get; set; }
    [System.Text.Json.Serialization.JsonRequired]
    [Range(1, 2)]
    public byte BilingualContentMode { get; set; } = 1;
    [Range(0, int.MaxValue)] public int DisplayOrder { get; set; }
}

public sealed class ReplaceContractAcceptanceMilestonesRequest
{
    public List<int> PaymentMilestoneIds { get; set; } = [];
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class FinalizeContractAcceptanceRecordRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class CancelContractAcceptanceRecordRequest
{
    [Required, MaxLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class UploadContractAcceptanceSignedEvidenceRequest
{
    public IFormFile File { get; set; } = null!;
    [Required(ErrorMessage = "Vui lòng chọn ngày ký thực tế.")]
    public DateTime? SignDate { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}
