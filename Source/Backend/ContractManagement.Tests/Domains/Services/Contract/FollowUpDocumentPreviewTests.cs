using ContractManagement.API.Common.Enums;
using ContractManagement.Common.Enums;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractDocumentPreviewServiceTests
{
    [Theory]
    [InlineData(ContractType.SoftwareMaintenance)]
    [InlineData(ContractType.SoftwareUpkeep)]
    public async Task FollowUp_UsesCorrectTemplateAndRendersParentBasisForSubmission(ContractType type)
    {
        await using var db = CreateContext();
        var source = CreateSourceDocument();
        await SeedAsync(db, source);
        (await db.TblContracts.SingleAsync()).ContractType = (byte)type;
        (await db.TblContracts.SingleAsync()).Status = (byte)ContractStatus.Negotiating;
        (await db.TblContractTemplates.SingleAsync()).DocumentType = type == ContractType.SoftwareMaintenance
            ? (byte)TemplateDocumentType.SoftwareMaintenanceContract : (byte)TemplateDocumentType.SoftwareUpkeepContract;
        (await db.TblContractLegalBases.SingleAsync()).ContentVi = RichText("Căn cứ Hợp đồng số HD-PARENT ký ngày 01/09/2026.");
        await db.SaveChangesAsync();
        var pdf = new CapturingPdfRenderer();
        var result = await CreateService(db, source, pdf).RenderAsync(ContractId, OwnerId);
        Assert.Equal((byte)type, result.Snapshot.Contract.ContractType);
        using var document = WordprocessingDocument.Open(new MemoryStream(result.DocxContent), false);
        Assert.Contains("HD-PARENT", document.MainDocumentPart!.Document!.Body!.InnerText);
        Assert.DoesNotContain("{{CONTRACT_LEGAL_BASES}}", document.MainDocumentPart.Document.Body.InnerText);
        Assert.Equal(result.DocxContent, pdf.InputDocx);
        Assert.NotEmpty(result.PdfContent);
    }
}
