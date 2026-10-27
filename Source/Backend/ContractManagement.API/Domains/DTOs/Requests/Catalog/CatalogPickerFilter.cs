using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;

namespace ContractManagement.API.Domains.DTOs.Requests.Catalog;

public sealed class CatalogPickerFilter
{
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(500)] public string? Keyword { get; set; }
    public ContractItemType? ItemType { get; set; }
    public bool SelectedOnly { get; set; }
    public List<int>? ProductIds { get; set; }
    public List<int>? ServiceIds { get; set; }
    public ContractItemType? FocusItemType { get; set; }
    public int? FocusItemId { get; set; }

    public void EnsureValid()
    {
        static bool ValidType(ContractItemType? type) => type is null or ContractItemType.Product or ContractItemType.Service;
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue
            || Keyword?.Length > 500 || !ValidType(ItemType) || !ValidType(FocusItemType)
            || FocusItemId is <= 0 || FocusItemId.HasValue != FocusItemType.HasValue
            || ProductIds?.Any(id => id <= 0) == true || ServiceIds?.Any(id => id <= 0) == true)
            throw new ArgumentException("Bộ lọc sản phẩm/dịch vụ không hợp lệ.");
    }
}
