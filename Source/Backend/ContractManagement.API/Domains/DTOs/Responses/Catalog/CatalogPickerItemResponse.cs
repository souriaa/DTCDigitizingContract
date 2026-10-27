using ContractManagement.API.Common.Enums;

namespace ContractManagement.API.Domains.DTOs.Responses.Catalog;

public sealed class CatalogPickerItemResponse
{
    public int SourceId { get; set; }
    public ContractItemType ItemType { get; set; }
    public string? Name { get; set; }
    public string? NameEn { get; set; }
    public string? UnitName { get; set; }
    public string? UnitNameEn { get; set; }
    public double? Price { get; set; }
}
