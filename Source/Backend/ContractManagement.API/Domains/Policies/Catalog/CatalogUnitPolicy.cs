namespace ContractManagement.Domains.Policies.Catalog;

public static class CatalogUnitPolicy
{
    public static string Require(string? value, string label)
    {
        var unit = value?.Trim();
        if (string.IsNullOrWhiteSpace(unit)) throw new ArgumentException($"{label} là bắt buộc. Hãy cập nhật đơn vị trong danh mục sản phẩm/dịch vụ.");
        if (unit.Length > 100) throw new ArgumentException($"{label} không được vượt quá 100 ký tự.");
        return unit;
    }
}
