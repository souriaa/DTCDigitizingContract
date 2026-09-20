namespace ContractManagement.Domains.Policies.ContractTemplate;

/// <summary>
/// Placeholder schema used by acceptance-record DOCX templates.
/// Every marker is optional so an author can choose the document layout.
/// When present, dynamic markers are replaced with the corresponding record data.
/// </summary>
public static class AcceptanceRecordPlaceholderCatalog
{
    public const string Version = "V2";

    private static readonly IReadOnlyList<SoftwareSupplyPlaceholderDefinition>
        Items =
        [
            Scalar("ACCEPTANCE_CODE", "Mã biên bản nghiệm thu", "Acceptance.AcceptanceCode"),
            Scalar("ACCEPTANCE_DATE", "Ngày nghiệm thu", "Acceptance.AcceptanceDate"),
            Scalar("ACCEPTANCE_LOCATION", "Địa điểm nghiệm thu", "Acceptance.Location"),
            Scalar("CUSTOMER_LEGAL_NAME", "Tên pháp nhân khách hàng", "Acceptance.Customer.LegalName"),
            Scalar("CUSTOMER_ADDRESS", "Địa chỉ khách hàng", "Acceptance.Customer.Address"),
            Scalar("CUSTOMER_PHONE", "Điện thoại khách hàng", "Acceptance.Customer.Phone"),
            Scalar("CUSTOMER_FAX", "Fax khách hàng", "Acceptance.Customer.Fax"),
            Scalar("CUSTOMER_TAX_CODE", "Mã số thuế khách hàng", "Acceptance.Customer.TaxCode"),
            Scalar("CUSTOMER_REPRESENTATIVE_NAME", "Người đại diện khách hàng", "Acceptance.Customer.RepresentativeName"),
            Scalar("CUSTOMER_REPRESENTATIVE_TITLE", "Chức vụ đại diện khách hàng", "Acceptance.Customer.RepresentativeTitle"),
            Scalar("TENANT_LEGAL_NAME", "Tên pháp nhân doanh nghiệp", "Acceptance.Tenant.LegalName"),
            Scalar("TENANT_ADDRESS", "Địa chỉ doanh nghiệp", "Acceptance.Tenant.Address"),
            Scalar("TENANT_PHONE", "Điện thoại doanh nghiệp", "Acceptance.Tenant.Phone"),
            Scalar("TENANT_FAX", "Fax doanh nghiệp", "Acceptance.Tenant.Fax"),
            Scalar("TENANT_TAX_CODE", "Mã số thuế doanh nghiệp", "Acceptance.Tenant.TaxCode"),
            Scalar("TENANT_REPRESENTATIVE_NAME", "Người đại diện doanh nghiệp", "Acceptance.Tenant.RepresentativeName"),
            Scalar("TENANT_REPRESENTATIVE_TITLE", "Chức vụ đại diện doanh nghiệp", "Acceptance.Tenant.RepresentativeTitle"),
            Dynamic("DOCUMENT_REFERENCES", "Căn cứ hợp đồng và phụ lục", "Acceptance.References"),
            Dynamic("ACCEPTANCE_SECTIONS", "Nội dung nghiệm thu", "Acceptance.Sections"),
            Dynamic("SIGNATURE_PROVIDER", "Chữ ký đại diện doanh nghiệp", "Acceptance.Tenant.Signature"),
            Dynamic("SIGNATURE_CUSTOMER", "Chữ ký đại diện khách hàng", "Acceptance.Customer.Signature")
        ];

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> All => Items;

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
