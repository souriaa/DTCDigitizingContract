namespace ContractManagement.API.Common.Security;

public sealed record EmployeeLandingPageOption(string Path, string Label);

public static class EmployeePreferenceRoutes
{
    public const string DefaultPage = "/contracts";

    private sealed record RouteDefinition(
        string Path,
        string Label,
        params string[] AnyPermissions);

    private static readonly RouteDefinition[] Definitions =
    [
        new(DefaultPage, "Danh sách hợp đồng", RbacPermissions.ContractReadOwn, RbacPermissions.ContractReadTenant),
        new("/dashboard", "Tổng quan"),
        new("/quotations", "Báo giá", RbacPermissions.QuotationManage),
        new("/customers", "Khách hàng", RbacPermissions.CustomerManage),
        new("/catalog/products", "Sản phẩm và dịch vụ", RbacPermissions.CatalogRead),
        new("/contract-approvals", "Duyệt hợp đồng", RbacPermissions.ContractApprovalDecide),
        new("/contract-audits", "Nhật ký hợp đồng", RbacPermissions.ContractAuditReadOwn, RbacPermissions.ContractAuditReadTenant),
        new("/security-audits", "Nhật ký bảo mật", RbacPermissions.SecurityAuditReadTenant),
        new("/admin/employees", "Nhân viên", RbacPermissions.EmployeeManage),
        new("/admin/contract-templates", "Mẫu hợp đồng", RbacPermissions.TemplateManage),
        new("/admin/legal-profile", "Hồ sơ pháp lý", RbacPermissions.TenantLegalProfileManage)
    ];

    public static IReadOnlyList<EmployeeLandingPageOption> GetAvailable(
        IReadOnlyCollection<string> permissions) =>
        Definitions
            .Where(definition => IsAvailable(definition, permissions))
            .Select(definition => new EmployeeLandingPageOption(
                definition.Path,
                definition.Label))
            .ToArray();

    public static bool IsAvailable(
        string? path,
        IReadOnlyCollection<string> permissions) =>
        Definitions.Any(definition =>
            string.Equals(definition.Path, path, StringComparison.Ordinal)
            && IsAvailable(definition, permissions));

    public static string ResolveDefault(
        string? path,
        IReadOnlyCollection<string> permissions)
    {
        if (IsAvailable(path, permissions)) return path!;
        return IsAvailable(DefaultPage, permissions) ? DefaultPage : "/dashboard";
    }

    private static bool IsAvailable(
        RouteDefinition definition,
        IReadOnlyCollection<string> permissions) =>
        definition.AnyPermissions.Length == 0
        || definition.AnyPermissions.Any(permission =>
            permissions.Contains(permission, StringComparer.Ordinal));
}
