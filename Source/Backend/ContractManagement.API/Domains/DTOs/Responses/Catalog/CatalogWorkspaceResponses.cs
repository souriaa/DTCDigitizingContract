namespace ContractManagement.API.Domains.DTOs.Responses.Catalog;

public sealed class CatalogGroupNode
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int? ParentId { get; set; }
    public int? Order { get; set; }
    public int DirectCount { get; set; }
    public int SubtreeCount { get; set; }
    public int DescendantCount { get; set; }
    public string Path { get; set; } = "";
}
public sealed record CatalogGroupsResponse(IReadOnlyList<CatalogGroupNode> Groups, int UnassignedCount, int TotalCount);
public sealed class CatalogItemSummaryResponse
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? UnitName { get; set; }
    public string? UnitNameEn { get; set; }
    public int? GroupId { get; set; }
    public string GroupPath { get; set; } = "";
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public double? Price { get; set; }
    public double? SetupPrice { get; set; }
    public double? MaintainPrice { get; set; }
    public byte? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
