namespace ContractManagement.Domains.Policies.Catalog;

public static class CatalogEnglishNamePolicy
{
    public static string Require(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{label} không được để trống.");
        var name = value.Trim();
        if (name.Length > 500) throw new ArgumentException($"{label} không được vượt quá 500 ký tự.");
        return name;
    }
}
