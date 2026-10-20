using ContractManagement.API.Common.Enums;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task AcceptanceRepresentativePhone_RendersFrozenContact_WithoutReadingLiveMaster(ContractDirection direction)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: direction, documentBytes: RepresentativePhoneDocx(), counterpartyContactPhone: "090-FROZEN");
        (await db.TblCustomers.SingleAsync()).CustomerContactPersonPhone = "090-CHANGED-MASTER";
        await db.SaveChangesAsync();
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        using var document = WordprocessingDocument.Open(new MemoryStream(preview.Content), false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("090-CHANGED-MASTER", text);
        Assert.Equal(direction == ContractDirection.Sale, text.Contains("090-FROZEN", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task HandoverRepresentativePhone_RendersFrozenContact_WithoutReadingLiveMaster(ContractDirection direction)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, direction, counterpartyContactPhone: "090-FROZEN");
        (await db.TblCustomers.SingleAsync()).CustomerContactPersonPhone = "090-CHANGED-MASTER";
        await db.SaveChangesAsync();
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.Record.HandoverRecordId, "docx", EmployeeId);
        using var document = WordprocessingDocument.Open(new MemoryStream(preview.Content), false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.DoesNotContain("{{", text);
        Assert.DoesNotContain("090-CHANGED-MASTER", text);
        Assert.Equal(direction == ContractDirection.Sale, text.Contains("090-FROZEN", StringComparison.Ordinal));
    }

    private static byte[] RepresentativePhoneDocx()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("{{CUSTOMER_REPRESENTATIVE_PHONE}}")))));
            part.Document.Save();
        }
        return stream.ToArray();
    }
}
