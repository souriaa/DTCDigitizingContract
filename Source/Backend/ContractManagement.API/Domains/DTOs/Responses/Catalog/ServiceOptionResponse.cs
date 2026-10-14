namespace ContractManagement.API.Domains.DTOs.Responses.Catalog;

public sealed class ServiceOptionResponse
{
    public int ServiceId { get; set; }
    public string? ServiceName { get; set; }
    public int? ServiceParentId { get; set; }
    public byte? Status { get; set; }
}
