using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Fact]
    public async Task Acceptance_WithoutSectionsMarker_AllowsEmptySectionsThroughoutLifecycle()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage, documentBytes: SectionlessDocument("ACCEPTANCE_CODE"), omitSections: true);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        Assert.Empty(draft.Sections);
        draft = await service.ReplaceSectionsAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, Sections = [] }, EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20), File = FormFile("signed.pdf", "application/pdf", "%PDF-proof") }, EmployeeId);
        Assert.Equal(AcceptanceRecordStatus.Signed, (await service.GetAsync(ContractId, draft.AcceptanceRecordId, EmployeeId)).Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Acceptance_WithSectionsMarker_RejectsCreatingOrSavingEmptySections(bool omitSourceSections)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage, omitSections: omitSourceSections);
        var service = CreateService(db, storage);
        if (omitSourceSections)
        {
            Assert.Equal("AcceptanceSectionsMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(ContractId, CreateRequest(), EmployeeId))).Code);
        }
        else
        {
            var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
            Assert.Equal("AcceptanceSectionsMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.ReplaceSectionsAsync(ContractId, draft.AcceptanceRecordId,
                    new() { RowVersion = draft.RowVersion, Sections = [] }, EmployeeId))).Code);
        }
    }

    [Fact]
    public async Task Handover_WithoutSectionsMarker_AllowsEmptySectionsThroughoutLifecycle()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, omitSections: true,
            documentBytes: SectionlessDocument("HANDOVER_CODE"));
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        Assert.Empty(draft.Sections);
        draft = await service.UpdateAsync(ContractId, draft.Record.HandoverRecordId, new() {
            RowVersion = draft.Record.RowVersion, HandoverCode = draft.Record.HandoverCode,
            HandoverDate = draft.Record.HandoverDate, Location = draft.Location, Parties = draft.Parties,
            Sections = [], Lines = [new(scope.Items[0].HandoverScopeItemId, 1)] }, EmployeeId);
        await service.PreviewAsync(ContractId, draft.Record.HandoverRecordId, "docx", EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId,
            new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        var signed = await service.SignAsync(ContractId, draft.Record.HandoverRecordId, new() {
            RowVersion = finalized.Record.RowVersion, File = FormFile("signed.pdf", "application/pdf", "%PDF-proof"),
            SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = true }, EmployeeId);
        Assert.Equal(HandoverRecordStatus.Signed, signed.Record.Status);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handover_WithSectionsMarker_RejectsCreatingOrSavingEmptySections(bool omitSourceSections)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, omitSections: omitSourceSections);
        if (omitSourceSections)
        {
            Assert.Equal("HandoverSectionsMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId))).Code);
        }
        else
        {
            var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
            Assert.Equal("HandoverSectionsMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.UpdateAsync(ContractId, draft.Record.HandoverRecordId, new() {
                    RowVersion = draft.Record.RowVersion, HandoverCode = draft.Record.HandoverCode,
                    HandoverDate = draft.Record.HandoverDate, Location = draft.Location, Parties = draft.Parties,
                    Sections = [], Lines = [new(scope.Items[0].HandoverScopeItemId, 1)] }, EmployeeId))).Code);
        }
    }

    private static byte[] SectionlessDocument(string marker)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var part = document.AddMainDocumentPart();
            part.Document = new W.Document(new W.Body(new W.Paragraph(new W.Run(new W.Text("{{" + marker + "}}")))));
            part.Document.Save();
        }
        return stream.ToArray();
    }
}
