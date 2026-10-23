using ContractManagement.Common.Enums;

namespace ContractManagement.Domains.Policies.ContractTemplate;

public static class SupportingDocumentCatalog
{
    public static bool Supports(TemplateDocumentType type) => type == TemplateDocumentType.AcceptanceRecord;

    public static string SectionsKey(TemplateDocumentType type) => type == TemplateDocumentType.AcceptanceRecord
        ? "ACCEPTANCE_SECTIONS" : throw new ArgumentOutOfRangeException(nameof(type));

    public static IReadOnlyList<SoftwareSupplyPlaceholderDefinition> Get(TemplateDocumentType type) =>
        type == TemplateDocumentType.AcceptanceRecord
            ? AcceptanceRecordPlaceholderCatalog.All : throw new ArgumentOutOfRangeException(nameof(type));

    public static string Version(TemplateDocumentType type) => type == TemplateDocumentType.AcceptanceRecord
        ? AcceptanceRecordPlaceholderCatalog.Version : throw new ArgumentOutOfRangeException(nameof(type));
}
