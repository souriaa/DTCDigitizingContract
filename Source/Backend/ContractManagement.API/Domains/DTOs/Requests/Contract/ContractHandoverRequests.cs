using ContractManagement.API.Common.Enums;
namespace ContractManagement.API.Domains.DTOs.Requests.Contract;
public sealed record HandoverScopeChoice(int ContractItemId, HandoverScopeMode Mode);
public sealed class ConfirmHandoverScopeRequest
{
    public int ContractVersionId { get; set; }
    public string ContractRowVersion { get; set; } = "";
    public string VersionRowVersion { get; set; } = "";
    public IReadOnlyList<HandoverScopeChoice> Items { get; set; } = [];
}
public sealed record HandoverLineInput(long HandoverScopeItemId, decimal Quantity, string? NotesVi = null, string? NotesEn = null);
public sealed class HandoverPartyInput
{
    public byte SubjectKind { get; set; }
    public string LegalName { get; set; } = "";
    public string? LegalNameEn { get; set; }
    public string? Address { get; set; }
    public string? AddressEn { get; set; }
    public string? Phone { get; set; }
    public string? Fax { get; set; }
    public string? TaxCode { get; set; }
    public string RepresentativeName { get; set; } = "";
    public string? RepresentativeTitle { get; set; }
    public string? RepresentativeTitleEn { get; set; }
}
public sealed class HandoverSectionInput
{
    public string SectionCode { get; set; } = "";
    public string TitleVi { get; set; } = "";
    public string? TitleEn { get; set; }
    public string? ContentVi { get; set; }
    public string? ContentEn { get; set; }
    public byte BilingualContentMode { get; set; } = 1;
}
public class HandoverMetadataRequest
{
    public string HandoverCode { get; set; } = "";
    public DateTime HandoverDate { get; set; }
    public string Location { get; set; } = "";
    public string? LocationEn { get; set; }
    public IReadOnlyList<int> AppendixIds { get; set; } = [];
    public IReadOnlyList<HandoverLineInput> Lines { get; set; } = [];
}
public sealed class CreateContractHandoverRequest : HandoverMetadataRequest
{
    public int ContractVersionId { get; set; }
    public int TemplateVersionId { get; set; }
    public string ContractRowVersion { get; set; } = "";
    public string VersionRowVersion { get; set; } = "";
}
public sealed class UpdateContractHandoverRequest : HandoverMetadataRequest
{
    public string RowVersion { get; set; } = "";
    public IReadOnlyList<HandoverPartyInput> Parties { get; set; } = [];
    public IReadOnlyList<HandoverSectionInput> Sections { get; set; } = [];
}
public class HandoverRowVersionRequest { public string RowVersion { get; set; } = ""; }
public sealed class CancelContractHandoverRequest : HandoverRowVersionRequest { public string Reason { get; set; } = ""; }
public sealed class UploadHandoverSignedRequest : HandoverRowVersionRequest
{
    public IFormFile File { get; set; } = null!;
    public DateTime SignDate { get; set; }
    public bool ConfirmBothPartiesSigned { get; set; }
}
