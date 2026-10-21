using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using W = DocumentFormat.OpenXml.Wordprocessing;
using static ContractManagement.Tests.ContractRichTextTestData;
namespace ContractManagement.Tests.Domains.Services.ContractTemplate;
public sealed partial class ContractTemplatePreviewTests
{
    [Theory]
    [InlineData(ContractDirection.Sale, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractDirection.Purchase, ContractLanguageMode.Vietnamese)]
    [InlineData(ContractDirection.Sale, ContractLanguageMode.Bilingual)]
    [InlineData(ContractDirection.Purchase, ContractLanguageMode.Bilingual)]
    public async Task Handover_TemplateUploadPreviewPublish_UsesOwnCatalogAndCombinedSections(ContractDirection direction, ContractLanguageMode language)
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        var definition = await db.TblContractTemplates.SingleAsync(); definition.DocumentType = (byte)TemplateDocumentType.HandoverRecord; definition.LanguageMode = (byte)language;
        var row = await GetVersionAsync(db); row.ContractDirection = (byte)direction;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var source = HandoverSource();
        var service = CreateService(db, storage, pdfRenderer: new FakePdfRenderer("%PDF-handover"));
        var uploaded = await service.UploadDocumentAsync(VersionId, new UploadContractTemplateDocumentRequest {
            File = new FormFile(new MemoryStream(source), 0, source.Length, "File", "handover.docx"),
            VersionRowVersion = Convert.ToBase64String((await GetVersionAsync(db)).RowVersion) }, AdminOfficerId);
        Assert.Equal(TemplateValidationStatus.Valid, uploaded.ValidationStatus);
        var fields = await db.TblContractTemplateFields.AsNoTracking().ToListAsync();
        Assert.Equal(8, fields.Count);
        Assert.All(fields, x => Assert.Contains(HandoverRecordPlaceholderCatalog.All, definition => definition.Key == x.PlaceholderKey));
        db.TblContractTemplateTerms.Add(new() { TemplateVersionId = VersionId, TermCode = "HANDOVER_RESULT", TermTitle = "Kết quả bàn giao",
            TermTitleEn = "Handover result", TermContent = RichText("NỘI DUNG GỘP"), BilingualContentMode = language == ContractLanguageMode.Bilingual ? (byte)2 : (byte)1,
            TermKind = (byte)ContractTermKind.General, DisplayOrder = 1, CreatedEmployeeId = AdminOfficerId, CreatedDate = DateTime.UtcNow, RowVersion = [2] });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var preview = await service.GeneratePreviewAsync(VersionId, PreviewRequest(uploaded.RowVersion), AdminOfficerId);
        var download = await service.DownloadPreviewAsync(VersionId, AdminOfficerId);
        await using (download.Stream)
        using (var doc = WordprocessingDocument.Open(download.Stream, false)) {
            var text = doc.MainDocumentPart!.Document!.InnerText;
            Assert.DoesNotContain("{{", text);
            Assert.Single(doc.MainDocumentPart.Document.Descendants<W.Table>());
            Assert.Equal(1, text.Split("NỘI DUNG GỘP").Length - 1);
            var signature = doc.MainDocumentPart.Document.Descendants<W.Paragraph>().Single(x => x.InnerText.Contains("ĐẠI DIỆN BÊN CUNG CẤP"));
            Assert.Equal(W.JustificationValues.Center, signature.ParagraphProperties!.GetFirstChild<W.Justification>()!.Val!.Value);
        }
        var published = await service.PublishAsync(VersionId, PublishRequest(preview.RowVersion), AdminOfficerId);
        Assert.Equal(TemplateVersionStatus.Published, published.Status);
    }
    [Fact]
    public async Task Handover_PublishWithoutSections_IsBlocked_ButMarkersRemainOptional()
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        (await db.TblContractTemplates.SingleAsync()).DocumentType = (byte)TemplateDocumentType.HandoverRecord;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var source = HandoverSource();
        var service = CreateService(db, storage, pdfRenderer: new FakePdfRenderer("%PDF-handover"));
        var uploaded = await service.UploadDocumentAsync(VersionId, new() { File = new FormFile(new MemoryStream(source), 0, source.Length, "File", "handover.docx"), VersionRowVersion = Convert.ToBase64String((await GetVersionAsync(db)).RowVersion) }, AdminOfficerId);
        var preview = await service.GeneratePreviewAsync(VersionId, PreviewRequest(uploaded.RowVersion), AdminOfficerId);
        var error = await Assert.ThrowsAsync<ContractManagement.Domains.Interfaces.ContractTemplate.ContractTemplatePreviewException>(() => service.PublishAsync(VersionId, PublishRequest(preview.RowVersion), AdminOfficerId));
        Assert.Equal("HandoverSectionsMissing", error.FailureCode);
        Assert.All(HandoverRecordPlaceholderCatalog.All, x => Assert.False(x.IsRequired));
        Assert.DoesNotContain(HandoverRecordPlaceholderCatalog.All, x => x.Key.StartsWith("TENANT_") || x.Key.StartsWith("ACCEPTANCE_"));
    }
    private static byte[] HandoverSource()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document)) {
            var part = doc.AddMainDocumentPart(); var body = new W.Body();
            foreach (var key in new[] { "HANDOVER_CODE", "PROVIDER_COMPANY_NAME", "CUSTOMER_COMPANY_NAME", "DOCUMENT_REFERENCES", "HANDOVER_ITEM_TABLE", "HANDOVER_SECTIONS", "SIGNATURE_PROVIDER", "SIGNATURE_CUSTOMER" })
                body.Append(new W.Paragraph(new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }), new W.Run(new W.Text("{{" + key + "}}"))));
            part.Document = new W.Document(body); part.Document.Save();
        }
        return stream.ToArray();
    }
}
