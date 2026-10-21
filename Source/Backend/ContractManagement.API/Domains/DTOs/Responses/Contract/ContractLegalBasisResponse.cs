namespace ContractManagement.API.Domains.DTOs.Responses.Contract;

public sealed class ContractLegalBasisResponse
{
    public int LegalBasisId { get; set; }
    public int? SourceTemplateLegalBasisId { get; set; }
    public string BasisCode { get; set; } = string.Empty;
    public string ContentVi { get; set; } = string.Empty;
    public string? ContentEn { get; set; }
    public int DisplayOrder { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}
