using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.ContractTemplate;

/// <summary>Only a marker actually recognized during DOCX upload requires dynamic sections.</summary>
internal static class SupportingDocumentSectionReader
{
    internal static Task<bool> IsRequiredAsync(DbDtctechContext db, int templateVersionId,
        TemplateDocumentType documentType, CancellationToken ct)
    {
        if (!SupportingDocumentCatalog.Supports(documentType)) return Task.FromResult(false);
        var marker = SupportingDocumentCatalog.SectionsKey(documentType);
        return db.TblContractTemplateFields.AsNoTracking().AnyAsync(field =>
            field.TemplateVersionId == templateVersionId && field.IsSystem && field.PlaceholderKey == marker, ct);
    }
}
