using System.ComponentModel.DataAnnotations;
namespace ContractManagement.API.Domains.DTOs.Requests.Catalog;

public sealed class CatalogWorkspaceFilter
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [Range(1, 255)] public int? GroupId { get; set; }
    public bool UnassignedOnly { get; set; }
    [Range(0, 2)] public byte? Status { get; set; }
    [MaxLength(500)] public string? Keyword { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public void EnsureValid()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || Status > 2 || GroupId is < 1 or > 255
            || (UnassignedOnly && GroupId.HasValue) || FromDate?.Date > ToDate?.Date
            || (long)(Page - 1) * PageSize > int.MaxValue || ToDate?.Date == DateTime.MaxValue.Date)
            throw new ArgumentException("Bộ lọc danh mục không hợp lệ.");
    }
}
