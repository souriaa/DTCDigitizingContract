namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>
/// Phân loại dữ liệu mà DOCX renderer sẽ xử lý ở các slice tiếp theo.
/// </summary>
public enum TemplatePlaceholderDataKind
{
    Scalar = 1,
    DynamicBlock = 2
}

/// <summary>
/// Số lần một placeholder được phép xuất hiện trong DOCX.
/// </summary>
public enum TemplatePlaceholderMultiplicity
{
    ExactlyOne = 1,
    ZeroOrOne = 2,
    ZeroOrMore = 3
}

/// <summary>
/// Một mục trong catalog cố định của SoftwareSupply.
/// Catalog là policy tĩnh, không đọc hoặc ghi tbl_ContractTemplateField.
/// </summary>
public sealed record SoftwareSupplyPlaceholderDefinition(
    string Key,
    string Label,
    bool IsRequired,
    TemplatePlaceholderDataKind DataKind,
    TemplatePlaceholderMultiplicity Multiplicity,
    string DataSource)
{
    public int? Id { get; init; }
    public bool IsSystem { get; init; } = true;
    public bool IsActive { get; init; } = true;
    public string? SourceFieldKey { get; init; }
    public string? ModuleKey { get; init; }
    public PlaceholderValueType? ValueType { get; init; }
    public string? DefaultValue { get; init; }
    public string? FormatString { get; init; }
    public string? RowVersion { get; init; }
}

/// <summary>
/// Catalog hiện hành cho template hợp đồng cung cấp phần mềm.
///
/// Tenant chỉ cấu hình template/version/term. Tenant không được thay đổi
/// key, DataSource, requiredness hoặc multiplicity của catalog này.
/// </summary>
public static class SoftwareSupplyPlaceholderCatalog
{
    public const string Version = "V7";

    private static readonly IReadOnlyList<SoftwareSupplyPlaceholderDefinition>
        Items =
        [
            new("CONTRACT_DATE_EN", "Ngày hợp đồng tiếng Anh", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.CreatedDate"),
            new("EFFECTIVE_DATE_EN", "Ngày hiệu lực tiếng Anh", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.EffectiveDate"),
            new("EXPIRE_DATE_EN", "Ngày hết hạn tiếng Anh", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ExpireDate"),
            new("CONTRACT_TOTAL_AMOUNT_IN_WORDS_EN", "Tổng giá trị bằng chữ tiếng Anh", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.TotalAmount"),
            new("CONTRACT_CODE", "Mã hợp đồng", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ContractCode"),
            new("CONTRACT_NAME", "Tên hợp đồng", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ContractName"),
            new("CONTRACT_DATE", "Ngày hợp đồng", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.CreatedDate"),
            new("EFFECTIVE_DATE", "Ngày hiệu lực", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.EffectiveDate"),
            new("EXPIRE_DATE", "Ngày hết hạn", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ExpireDate"),
            new("CONTRACT_CURRENCY", "Loại tiền", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.CurrencyCode"),
            new("CONTRACT_LEGAL_BASES", "Căn cứ hợp đồng", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.LegalBases"),
            new("CONTRACT_TERMS", "Các điều khoản hợp đồng", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.Terms"),
            new("CONTRACT_ITEM_TABLE", "Bảng sản phẩm/dịch vụ", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.Items"),
            new("SIGNATURE_PROVIDER", "Chữ ký bên cung cấp", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ProviderSignature"),
            new("SIGNATURE_CUSTOMER", "Chữ ký khách hàng", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.CustomerSignature"),
            new("CONTRACT_APPENDICES", "Các phụ lục đính kèm", false, TemplatePlaceholderDataKind.DynamicBlock, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.Appendices"),
            new("CONTRACT_NAME_EN", "Tên hợp đồng tiếng Anh", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.ContractNameEn"),
            new("CONTRACT_TOTAL_AMOUNT", "Tổng giá trị hợp đồng", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Contract.TotalAmount"),
            new("CONTRACT_TOTAL_AMOUNT_IN_WORDS", "Tổng giá trị bằng chữ", false, TemplatePlaceholderDataKind.Scalar, TemplatePlaceholderMultiplicity.ZeroOrOne, "Manual.ContractTotalAmountInWords")
        ];

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> All => Items.Concat(DocumentPartyMarkers.Definitions).ToArray();

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> GetAll() => All;

    public static SoftwareSupplyPlaceholderDefinition? Find(string key)
    {
        var normalized = key?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? null
            : All.FirstOrDefault(item =>
                string.Equals(item.Key, normalized, StringComparison.Ordinal));
    }
}
