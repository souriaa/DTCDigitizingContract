namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>
/// Placeholder schema used by acceptance-record DOCX templates.
/// Every marker is optional so an author can choose the document layout.
/// When present, dynamic markers are replaced with the corresponding record data.
/// </summary>
public static class AcceptanceRecordPlaceholderCatalog
{
    public const string Version = "V5";

    private static readonly IReadOnlyList<SoftwareSupplyPlaceholderDefinition>
        Items =
        [
            Scalar("ACCEPTANCE_DATE_EN", "Ngày nghiệm thu tiếng Anh", "Acceptance.AcceptanceDate"),
            Scalar("ACCEPTANCE_LOCATION_EN", "Địa điểm nghiệm thu tiếng Anh", "Acceptance.LocationEn"),
            Scalar("ACCEPTANCE_CODE", "Mã biên bản nghiệm thu", "Acceptance.AcceptanceCode"),
            Scalar("ACCEPTANCE_DATE", "Ngày nghiệm thu", "Acceptance.AcceptanceDate"),
            Scalar("ACCEPTANCE_LOCATION", "Địa điểm nghiệm thu", "Acceptance.Location"),
            Dynamic("DOCUMENT_REFERENCES", "Căn cứ hợp đồng và phụ lục", "Acceptance.References"),
            Dynamic("ACCEPTANCE_SECTIONS", "Nội dung nghiệm thu", "Acceptance.Sections"),
            Dynamic("SIGNATURE_PROVIDER", "Chữ ký bên cung cấp", "Acceptance.Provider.Signature"),
            Dynamic("SIGNATURE_CUSTOMER", "Chữ ký đại diện khách hàng", "Acceptance.Customer.Signature")
        ];

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> All => Items.Concat(DocumentPartyMarkers.Definitions).ToArray();

    private static SoftwareSupplyPlaceholderDefinition Scalar(
        string key,
        string label,
        string dataSource) => new(
            key,
            label,
            false,
            TemplatePlaceholderDataKind.Scalar,
            TemplatePlaceholderMultiplicity.ZeroOrMore,
            dataSource)
        {
            SourceFieldKey = $"system.{key}"
        };

    private static SoftwareSupplyPlaceholderDefinition Dynamic(
        string key,
        string label,
        string dataSource) => new(
            key,
            label,
            false,
            TemplatePlaceholderDataKind.DynamicBlock,
            TemplatePlaceholderMultiplicity.ZeroOrOne,
            dataSource)
        {
            SourceFieldKey = $"system.{key}"
        };
}
