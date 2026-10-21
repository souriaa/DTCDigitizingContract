using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed partial class ContractTemplatePreviewTests
{
    [Theory]
    [InlineData(TemplateDocumentType.AcceptanceRecord, false, ContractLanguageMode.Vietnamese)]
    [InlineData(TemplateDocumentType.AcceptanceRecord, true, ContractLanguageMode.Vietnamese)]
    [InlineData(TemplateDocumentType.HandoverRecord, false, ContractLanguageMode.Vietnamese)]
    [InlineData(TemplateDocumentType.HandoverRecord, true, ContractLanguageMode.Vietnamese)]
    [InlineData(TemplateDocumentType.AcceptanceRecord, false, ContractLanguageMode.Bilingual)]
    [InlineData(TemplateDocumentType.AcceptanceRecord, true, ContractLanguageMode.Bilingual)]
    [InlineData(TemplateDocumentType.HandoverRecord, false, ContractLanguageMode.Bilingual)]
    [InlineData(TemplateDocumentType.HandoverRecord, true, ContractLanguageMode.Bilingual)]
    public async Task SupportingDocument_RequiresSectionsOnlyWhenDocxContainsMarker(
        TemplateDocumentType type, bool includeSectionsMarker, ContractLanguageMode language)
    {
        await using var db = CreateContext();
        var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        var template = await db.TblContractTemplates.SingleAsync();
        template.DocumentType = (byte)type;
        template.LanguageMode = (byte)language;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var prefix = type == TemplateDocumentType.HandoverRecord ? "HANDOVER" : "ACCEPTANCE";
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var part = doc.AddMainDocumentPart();
            var body = new W.Body(new W.Paragraph(new W.Run(new W.Text("{{" + prefix + "_CODE}}"))));
            if (includeSectionsMarker) body.Append(new W.Paragraph(new W.Run(new W.Text("{{" + prefix + "_SECTIONS}}"))));
            part.Document = new W.Document(body);
            part.Document.Save();
        }
        var bytes = stream.ToArray();
        var service = CreateService(db, storage, pdfRenderer: new FakePdfRenderer("%PDF-sections"));
        var uploaded = await service.UploadDocumentAsync(VersionId, new UploadContractTemplateDocumentRequest {
            File = new FormFile(new MemoryStream(bytes), 0, bytes.Length, "File", "sections.docx"),
            VersionRowVersion = Convert.ToBase64String((await GetVersionAsync(db)).RowVersion) }, AdminOfficerId);
        Assert.Equal(TemplateValidationStatus.Valid, uploaded.ValidationStatus);
        Assert.Equal(includeSectionsMarker, uploaded.RequiresSections);
        var preview = await service.GeneratePreviewAsync(VersionId, PreviewRequest(uploaded.RowVersion), AdminOfficerId);
        Assert.Equal(includeSectionsMarker, (await service.GetVersionAsync(VersionId, AdminOfficerId)).RequiresSections);
        if (includeSectionsMarker)
        {
            var error = await Assert.ThrowsAsync<ContractTemplatePreviewException>(() =>
                service.PublishAsync(VersionId, PublishRequest(preview.RowVersion), AdminOfficerId));
            Assert.Equal(type == TemplateDocumentType.HandoverRecord ? "HandoverSectionsMissing" : "AcceptanceSectionsMissing", error.FailureCode);
        }
        else
        {
            var published = await service.PublishAsync(VersionId, PublishRequest(preview.RowVersion), AdminOfficerId);
            Assert.Equal(TemplateVersionStatus.Published, published.Status);
            Assert.Empty(published.Terms);
        }
    }
}
