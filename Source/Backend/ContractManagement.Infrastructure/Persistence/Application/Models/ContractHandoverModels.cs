namespace ContractManagement.Infrastructure.Persistence.Application.Models;
public sealed class TblContractHandoverBaseline
{
    public long HandoverBaselineId { get; set; }
    public int ContractId { get; set; }
    public int ContractVersionId { get; set; }
    public int ConfirmedByEmployeeId { get; set; }
    public DateTime ConfirmedAt { get; set; }
}
public sealed class TblContractHandoverScopeItem
{
    public long HandoverScopeItemId { get; set; }
    public long HandoverBaselineId { get; set; }
    public int SourceContractItemId { get; set; }
    public byte ScopeMode { get; set; }
    public string ItemCode { get; set; } = null!;
    public string NameVi { get; set; } = null!;
    public string? NameEn { get; set; }
    public string UnitVi { get; set; } = null!;
    public string? UnitEn { get; set; }
    public decimal SourceQuantity { get; set; }
    public decimal RequiredQuantity { get; set; }
    public int DisplayOrder { get; set; }
}
public sealed class TblContractHandoverRecord
{
    public int HandoverRecordId { get; set; }
    public int ContractId { get; set; }
    public int ContractVersionId { get; set; }
    public long HandoverBaselineId { get; set; }
    public int TemplateVersionId { get; set; }
    public string HandoverCode { get; set; } = null!;
    public DateTime HandoverDate { get; set; }
    public DateTime ContractSignDate { get; set; }
    public string Location { get; set; } = null!;
    public string? LocationEn { get; set; }
    public byte Status { get; set; }
    public string? SnapshotHash { get; set; }
    public int CreatedEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? UpdatedEmployeeId { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? FinalizedByEmployeeId { get; set; }
    public DateTime? FinalizedAt { get; set; }
    public int? SignedByEmployeeId { get; set; }
    public DateTime? SignedAt { get; set; }
    public int? CancelledByEmployeeId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public byte[] RowVersion { get; set; } = null!;
}
public sealed class TblContractHandoverLine
{
    public int HandoverLineId { get; set; }
    public int HandoverRecordId { get; set; }
    public long HandoverScopeItemId { get; set; }
    public decimal Quantity { get; set; }
    public int DisplayOrder { get; set; }
    public string? NotesVi { get; set; }
    public string? NotesEn { get; set; }
}
public sealed class TblContractHandoverParty
{
    public int HandoverPartyId { get; set; }
    public int HandoverRecordId { get; set; }
    public byte SubjectKind { get; set; }
    public string LegalName { get; set; } = null!;
    public string? LegalNameEn { get; set; }
    public string? Address { get; set; }
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? TaxCode { get; set; }
    public string RepresentativeName { get; set; } = null!;
    public string? RepresentativeTitle { get; set; }
    public string? RepresentativeTitleEn { get; set; }
}
public sealed class TblContractHandoverReference
{
    public int HandoverReferenceId { get; set; }
    public int HandoverRecordId { get; set; }
    public int? AppendixId { get; set; }
    public int DisplayOrder { get; set; }
}
public sealed class TblContractHandoverSection
{
    public int HandoverSectionId { get; set; }
    public int HandoverRecordId { get; set; }
    public string SectionCode { get; set; } = null!;
    public string TitleVi { get; set; } = null!;
    public string? TitleEn { get; set; }
    public string? ContentVi { get; set; }
    public string? ContentEn { get; set; }
    public byte BilingualContentMode { get; set; }
    public int DisplayOrder { get; set; }
}
public sealed class TblContractHandoverEvidence
{
    public int HandoverEvidenceId { get; set; }
    public int HandoverRecordId { get; set; }
    public int FileId { get; set; }
    public DateTime SignDate { get; set; }
    public int UploadedByEmployeeId { get; set; }
    public DateTime UploadedAt { get; set; }
}
public sealed class TblContractHandoverPlaceholderValue
{
    public long HandoverPlaceholderValueId { get; set; }
    public int HandoverRecordId { get; set; }
    public string PlaceholderKey { get; set; } = null!;
    public string SourceFieldKey { get; set; } = null!;
    public string RenderedValue { get; set; } = null!;
}
public sealed class TblContractAcceptanceScopeAllocation
{
    public long AcceptanceScopeAllocationId { get; set; }
    public int AcceptanceRecordId { get; set; }
    public int HandoverLineId { get; set; }
    public decimal Quantity { get; set; }
}
