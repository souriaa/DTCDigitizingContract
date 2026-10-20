using ContractManagement.API.Common.Enums;
using ContractManagement.Common.Enums;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.Domains.Policies.ContractTemplate;
using ContractManagement.Domains.Services.ContractTemplate;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed partial class ContractTemplateServiceTests
{
    [Fact]
    public async Task AvailableAndClone_PreserveDirectionPerVersion()
    {
        await using var db = CreateContext(); await SeedEmployeesAsync(db);
        var service = CreateService(db);
        var sale = await service.CreateAsync(CreateRequest("DIR-SALE"), AdminOfficerId);
        var purchaseRequest = CreateRequest("DIR-PURCHASE");
        purchaseRequest.ContractDirection = ContractDirection.Purchase;
        var purchase = await service.CreateAsync(purchaseRequest, AdminOfficerId);
        foreach (var owner in new[] { sale, purchase })
        {
            var version = await db.TblContractTemplateVersions.SingleAsync(x => x.TemplateVersionId == owner.Versions[0].TemplateVersionId);
            version.Status = (byte)TemplateVersionStatus.Published;
            (await db.TblContractTemplates.SingleAsync(x => x.TemplateId == owner.TemplateId)).CurrentPublishedVersionId = version.TemplateVersionId;
        }
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var found = await service.SearchAvailableAsync(new() { ContractDirection = ContractDirection.Purchase, LanguageMode = ContractLanguageMode.Vietnamese });
        Assert.Equal(purchase.TemplateId, Assert.Single(found.Items).TemplateId);
        Assert.Equal(ContractDirection.Purchase, found.Items[0].ContractDirection);
        var source = await service.GetVersionAsync(purchase.Versions[0].TemplateVersionId, AdminOfficerId);
        var clone = await service.CopyVersionAsync(source.TemplateVersionId, new() { RowVersion = source.RowVersion }, AdminOfficerId);
        Assert.Equal(ContractDirection.Purchase, clone.ContractDirection);
        Assert.Equal(ContractDirection.Purchase, (await service.GetVersionAsync(source.TemplateVersionId, AdminOfficerId)).ContractDirection);
        Assert.Equal(TemplateVersionStatus.Published, (await service.GetVersionAsync(source.TemplateVersionId, AdminOfficerId)).Status);
    }

    [Theory]
    [InlineData(ContractDirection.Sale)] [InlineData(ContractDirection.Purchase)]
    public void AuthoringPreview_ResolvesBothRolesAccordingToDirection(ContractDirection direction)
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var part = doc.AddMainDocumentPart();
            part.Document = new W.Document(new W.Body(
                new W.Paragraph(new W.Run(new W.Text("{{PROVIDER_COMPANY_NAME}}"))),
                new W.Paragraph(new W.Run(new W.Text("{{CUSTOMER_COMPANY_NAME}}")))));
            part.Document.Save();
        }
        var result = new ContractTemplatePreviewRenderer().RenderSample(stream.ToArray(), ContractLanguageMode.Vietnamese,
            ContractPlaceholderCatalog.SystemDefinitions, new Dictionary<string, string>(), new() { ContractDirection = direction });
        using var rendered = WordprocessingDocument.Open(new MemoryStream(result), false);
        var paragraphs = rendered.MainDocumentPart!.Document!.Body!.Elements<W.Paragraph>().Select(x => x.InnerText).ToArray();
        var samples = SoftwareSupplyPreviewDatasetV1.GetScalarValues(ContractLanguageMode.Vietnamese);
        Assert.Equal(samples[direction == ContractDirection.Sale ? "PROVIDER_COMPANY_NAME" : "CUSTOMER_COMPANY_NAME"], paragraphs[0]);
        Assert.Equal(samples[direction == ContractDirection.Sale ? "CUSTOMER_COMPANY_NAME" : "PROVIDER_COMPANY_NAME"], paragraphs[1]);
    }
}
