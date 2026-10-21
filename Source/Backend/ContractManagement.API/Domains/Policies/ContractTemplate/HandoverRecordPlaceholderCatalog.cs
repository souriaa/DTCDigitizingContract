using ContractManagement.Common.Enums;

namespace ContractManagement.Domains.Policies.ContractTemplate;

public static class HandoverRecordPlaceholderCatalog
{
    public const string Version = "V2";
    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> All { get; } = new[]
    {
        Field("HANDOVER_CODE", "Mã biên bản bàn giao"),
        Field("HANDOVER_DATE", "Ngày bàn giao"), Field("HANDOVER_DATE_EN", "Ngày bàn giao tiếng Anh"),
        Field("HANDOVER_LOCATION", "Địa điểm bàn giao"), Field("HANDOVER_LOCATION_EN", "Địa điểm tiếng Anh"),
        Field("DOCUMENT_REFERENCES", "Căn cứ hợp đồng và phụ lục", true),
        Field("HANDOVER_ITEM_TABLE", "Danh sách hạng mục bàn giao", true),
        Field("HANDOVER_SECTIONS", "Nội dung bàn giao", true),
        Field("SIGNATURE_PROVIDER", "Chữ ký bên cung cấp / bên giao", true),
        Field("SIGNATURE_CUSTOMER", "Chữ ký bên mua / bên nhận", true)
    }.Concat(DocumentPartyMarkers.Definitions).ToArray();

    private static SoftwareSupplyPlaceholderDefinition Field(string key, string label, bool block = false) =>
        new(key, label, false, block ? TemplatePlaceholderDataKind.DynamicBlock : TemplatePlaceholderDataKind.Scalar,
            block ? TemplatePlaceholderMultiplicity.ZeroOrOne : TemplatePlaceholderMultiplicity.ZeroOrMore, "Handover." + key)
        { SourceFieldKey = "system." + key };
}

public static class SupportingDocumentCatalog
{
    public static bool Supports(TemplateDocumentType type) => type is TemplateDocumentType.AcceptanceRecord or TemplateDocumentType.HandoverRecord;
    public static string SectionsKey(TemplateDocumentType type) => type switch
    {
        TemplateDocumentType.AcceptanceRecord => "ACCEPTANCE_SECTIONS",
        TemplateDocumentType.HandoverRecord => "HANDOVER_SECTIONS",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> Get(TemplateDocumentType type) => type switch
    {
        TemplateDocumentType.AcceptanceRecord => AcceptanceRecordPlaceholderCatalog.All,
        TemplateDocumentType.HandoverRecord => HandoverRecordPlaceholderCatalog.All,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
    public static string Version(TemplateDocumentType type) => type switch
    {
        TemplateDocumentType.AcceptanceRecord => AcceptanceRecordPlaceholderCatalog.Version,
        TemplateDocumentType.HandoverRecord => HandoverRecordPlaceholderCatalog.Version,
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
