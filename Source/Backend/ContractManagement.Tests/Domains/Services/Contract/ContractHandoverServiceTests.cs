using System.Security.Cryptography;
using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Policies.Contract;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using W = DocumentFormat.OpenXml.Wordprocessing;
namespace ContractManagement.Tests.Domains.Services.Contract;
public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Handover_Lifecycle_ReservesThenDelivers_WithoutCompletingAcceptance(ContractDirection direction)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, direction);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        Assert.Equal("Người liên hệ nghiệm thu", draft.Parties.Single(x => x.SubjectKind == 2).RepresentativeName);
        Assert.False((await service.ReadinessAsync(ContractId, EmployeeId)).CanCreate);
        var preview = await service.PreviewAsync(ContractId, draft.Record.HandoverRecordId, "docx", EmployeeId);
        using (var document = WordprocessingDocument.Open(new MemoryStream(preview.Content), false))
        {
            var text = document.MainDocumentPart!.Document!.InnerText;
            Assert.DoesNotContain("{{", text);
            Assert.Contains("ĐẠI DIỆN BÊN CUNG CẤP", text);
            Assert.Single(document.MainDocumentPart.Document.Descendants<W.Table>());
        }
        var finalized = await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        Assert.Equal(64, finalized.SnapshotHash!.Length);
        Assert.Equal(2, finalized.Artifacts.Count);
        var waiting = await service.ReadinessAsync(ContractId, EmployeeId);
        Assert.Equal(1m, waiting.Items[0].Reserved); Assert.Equal(0m, waiting.Items[0].Delivered);
        var signed = await service.SignAsync(ContractId, draft.Record.HandoverRecordId, new() {
            RowVersion = finalized.Record.RowVersion, File = FormFile("signed.pdf", "application/pdf", "%PDF-signed"),
            SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = true }, EmployeeId);
        Assert.Equal(HandoverRecordStatus.Signed, signed.Record.Status);
        var progress = await service.ReadinessAsync(ContractId, EmployeeId);
        Assert.Equal(1m, progress.Items[0].Delivered); Assert.Equal(1m, progress.Items[0].Remaining);
        Assert.Equal(0m, progress.Items[0].Accepted);
        Assert.Empty(db.TblContractAcceptanceRecords);
        Assert.Empty(db.TblContractAcceptanceEvidences);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CancelAsync(ContractId, signed.Record.HandoverRecordId, new() { RowVersion = signed.Record.RowVersion, Reason = "Wrong" }, EmployeeId));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SignAsync(ContractId, signed.Record.HandoverRecordId, new() {
            RowVersion = signed.Record.RowVersion, File = FormFile("signed.pdf", "application/pdf", "%PDF-signed"), SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = true }, EmployeeId));
        Assert.Single(db.TblContractHandoverEvidences);
        var artifact = await service.DownloadAsync(ContractId, signed.Record.HandoverRecordId, "docx", false, EmployeeId);
        Assert.Equal(finalized.Artifacts.Single(x => x.Format == "docx").Sha256, Sha(artifact.Content));
    }
    [Fact]
    public async Task Handover_CancelFinalized_ReleasesReservation_PreservesHistoryAndCode()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 2), EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        var cancelled = await service.CancelAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = finalized.Record.RowVersion, Reason = "Sai thông tin" }, EmployeeId);
        Assert.Equal(HandoverRecordStatus.Cancelled, cancelled.Record.Status);
        Assert.Equal(2, cancelled.Artifacts.Count);
        Assert.Equal(2m, (await service.ReadinessAsync(ContractId, EmployeeId)).Items[0].Remaining);
        var duplicate = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId));
        Assert.Equal("HandoverCodeAlreadyExists", duplicate.Code);
        var next = HandoverRequest(scope.Items[0].HandoverScopeItemId, 1); next.HandoverCode = "BBBG-002";
        await service.CreateAsync(ContractId, next, EmployeeId);
        Assert.Equal(2, db.TblContractHandoverRecords.Count());
    }
    [Fact]
    public async Task Handover_StorageFailure_CleansFirstArtifactAndLeavesDraft()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        storage.FailOnSaveNumber = 2;
        await Assert.ThrowsAsync<IOException>(() => service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId));
        Assert.Single(storage.DeletedKeys);
        Assert.Equal((byte)HandoverRecordStatus.Draft, (await db.TblContractHandoverRecords.AsNoTracking().SingleAsync()).Status);
        Assert.Empty(db.TblFileStorages.Where(x => x.ObjectType == "ContractHandoverArtifact"));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public async Task Handover_RejectsZeroOrExcessQuantity(int quantity)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, quantity), EmployeeId));
        Assert.Empty(db.TblContractHandoverRecords);
    }
    [Fact]
    public async Task Handover_RejectsStaleAndMismatchedTemplateAndScope()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var input = HandoverRequest(scope.Items[0].HandoverScopeItemId, 1); input.VersionRowVersion = Convert.ToBase64String([9]);
        Assert.Equal("StaleRowVersion", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, input, EmployeeId))).Code);
        input = HandoverRequest(long.MaxValue, 1);
        Assert.Equal("HandoverScopeMismatch", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, input, EmployeeId))).Code);
        input = HandoverRequest(scope.Items[0].HandoverScopeItemId, 1); input.TemplateVersionId = 999;
        Assert.Equal("HandoverTemplateMismatch", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, input, EmployeeId))).Code);
        Assert.Empty(db.TblContractHandoverRecords);
    }
    [Fact]
    public async Task Handover_Bilingual_UsesEnglishFirstAndRequiresLocation()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, ContractDirection.Purchase, ContractLanguageMode.Bilingual);
        var input = HandoverRequest(scope.Items[0].HandoverScopeItemId, 1);
        Assert.Equal("HandoverLocationEnRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, input, EmployeeId))).Code);
        input.LocationEn = "Hanoi";
        var draft = await service.CreateAsync(ContractId, input, EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.Record.HandoverRecordId, "docx", EmployeeId);
        using var document = WordprocessingDocument.Open(new MemoryStream(preview.Content), false);
        var text = document.MainDocumentPart!.Document!.InnerText;
        Assert.True(text.IndexOf("Product scope", StringComparison.Ordinal) < text.IndexOf("Phạm vi sản phẩm", StringComparison.Ordinal));
        Assert.Contains("CUSTOMER REPRESENTATIVE", text);
    }

    [Fact]
    public async Task Handover_AggregateUpdateKeepsOneRowVersionAndRejectsStaleSave()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        var request = new UpdateContractHandoverRequest { RowVersion = draft.Record.RowVersion, HandoverCode = draft.Record.HandoverCode,
            HandoverDate = draft.Record.HandoverDate, Location = "Địa điểm mới", Parties = draft.Parties, Sections = draft.Sections,
            Lines = [new(scope.Items[0].HandoverScopeItemId, 2)] };
        var saved = await service.UpdateAsync(ContractId, draft.Record.HandoverRecordId, request, EmployeeId);
        Assert.Equal("Địa điểm mới", saved.Location); Assert.Equal(2m, Assert.Single(saved.Lines).Quantity);
        Assert.NotEqual(draft.Record.RowVersion, saved.Record.RowVersion);
        var stale = await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(ContractId, draft.Record.HandoverRecordId, request, EmployeeId));
        Assert.Equal("StaleRowVersion", stale.Code);
        request.RowVersion = saved.Record.RowVersion; request.Parties[0].LegalName = "Tên khác";
        Assert.Equal("HandoverLegalNameImmutable", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.UpdateAsync(ContractId, draft.Record.HandoverRecordId, request, EmployeeId))).Code);
    }
    [Fact]
    public async Task Handover_RealAuditAcceptsNewSubjectsAndTypedFields()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage, realAudit: true);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        var audits = await db.TblContractAudits.AsNoTracking().Where(x => x.ActionType.StartsWith("Handover")).ToListAsync();
        Assert.Equal(3, audits.Count);
        Assert.Contains(audits, x => x.SubjectType == "HandoverRecord" && x.ActionType == "HandoverFinalized");
        Assert.Contains(audits, x => x.ActionType == "HandoverScopeConfirmed");
    }
    [Fact]
    public async Task Handover_ScopeAndFinalizedContentAreImmutable()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var item = await db.TblContractHandoverScopeItems.FirstAsync(); item.RequiredQuantity = 99;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        var section = await db.TblContractHandoverSections.SingleAsync(); section.TitleVi = "Sửa trái phép";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task Handover_AllScopeSigned_BlocksNewRecords_WithoutMarkingAcceptance()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        Assert.Equal(12m, scope.Items[1].SourceQuantity); Assert.Equal(1m, scope.Items[1].RequiredQuantity);
        var request = HandoverRequest(scope.Items[0].HandoverScopeItemId, 2);
        request.Lines = [new(scope.Items[0].HandoverScopeItemId, 2), new(scope.Items[1].HandoverScopeItemId, 1)];
        var draft = await service.CreateAsync(ContractId, request, EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        await service.SignAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = finalized.Record.RowVersion,
            File = FormFile("signed.pdf", "application/pdf", "%PDF-proof"), SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = true }, EmployeeId);
        var progress = await service.ReadinessAsync(ContractId, EmployeeId);
        Assert.True(progress.Completed); Assert.False(progress.CanCreate); Assert.Equal("HandoverAlreadyComplete", progress.BlockerCode);
        Assert.All(progress.Items, x => { Assert.Equal(0m, x.Remaining); Assert.Equal(0m, x.Accepted); });
        Assert.Empty(db.TblContractAcceptanceRecords);
    }
    [Fact]
    public async Task Handover_InvalidProofAndMissingContractSignatureDoNotAdvanceState()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        var (service, scope) = await HandoverFixture(db, storage);
        var draft = await service.CreateAsync(ContractId, HandoverRequest(scope.Items[0].HandoverScopeItemId, 1), EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.Record.HandoverRecordId, new() { RowVersion = draft.Record.RowVersion }, EmployeeId);
        var saves = storage.SavedKeys.Count;
        Assert.Equal("HandoverEvidenceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.SignAsync(ContractId, draft.Record.HandoverRecordId,
            new() { RowVersion = finalized.Record.RowVersion, File = FormFile("signed.pdf", "application/pdf", "%PDF-proof"), SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = false }, EmployeeId))).Code);
        Assert.Equal(saves, storage.SavedKeys.Count);
        (await db.TblContractSignedEvidences.SingleAsync()).Status = (byte)SignedEvidenceStatus.Superseded; await db.SaveChangesAsync();
        Assert.Equal("HandoverSignedEvidenceMissing", (await service.ReadinessAsync(ContractId, EmployeeId)).BlockerCode);
        Assert.Equal("HandoverSignedEvidenceMissing", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.SignAsync(ContractId, draft.Record.HandoverRecordId,
            new() { RowVersion = finalized.Record.RowVersion, File = FormFile("signed.pdf", "application/pdf", "%PDF-proof"), SignDate = new DateTime(2026,9,19), ConfirmBothPartiesSigned = true }, EmployeeId))).Code);
        Assert.Empty(db.TblContractHandoverEvidences);
    }

    private static CreateContractHandoverRequest HandoverRequest(long item, decimal quantity) => new() {
        ContractVersionId = VersionId, TemplateVersionId = TemplateVersionId, HandoverCode = "BBBG-001",
        HandoverDate = new DateTime(2026,9,18), Location = "Hà Nội", Lines = [new(item, quantity)],
        ContractRowVersion = Convert.ToBase64String(InitialVersion), VersionRowVersion = Convert.ToBase64String(InitialVersion) };
    private static async Task<(ContractHandoverService Service, ContractManagement.API.Domains.DTOs.Responses.Contract.HandoverReadinessResponse Scope)>
        HandoverFixture(DbDtctechContext db, TrackingStorage storage, ContractDirection direction = ContractDirection.Sale,
            ContractLanguageMode language = ContractLanguageMode.Vietnamese, bool realAudit = false, decimal contractTotal = 0m,
            string? counterpartyContactPhone = null, bool omitSections = false, byte[]? documentBytes = null)
    {
        var docx = documentBytes ?? HandoverDocx();
        await SeedAsync(db, storage, contractNameEn: "Software agreement", tenantLegalNameEn: "Tenant company",
            contractTotal: contractTotal, direction: direction, language: language, documentType: TemplateDocumentType.HandoverRecord, documentBytes: docx,
            counterpartyContactPhone: counterpartyContactPhone, omitSections: omitSections,
            handoverItems: [
                new TblContractItem { ContractItemId = 1701, ContractId = ContractId, VersionId = VersionId,
                    ItemName = "Phạm vi sản phẩm", ItemNameEn = "Product scope", UnitName = "Giấy phép", UnitNameEn = "License", Quantity = 2, DisplayOrder = 1, RowVersion = InitialVersion },
                new TblContractItem { ContractItemId = 1702, ContractId = ContractId, VersionId = VersionId,
                    ItemName = "Dịch vụ theo tháng", ItemNameEn = "Monthly service", UnitName = "Tháng", UnitNameEn = "Month", Quantity = 12, DisplayOrder = 2, RowVersion = InitialVersion }
            ]);
        var tenant = new CurrentTenant(); tenant.Set(new ResolvedTenant(1, "TENANT-PHASE7", "Tenant Phase 7", TenantDatabaseMode.Dedicated, "InMemory"));
        ContractManagement.Domains.Interfaces.Contract.IContractAuditWriter writer = realAudit
            ? new ContractAuditWriter(db, tenant, new HttpContextAccessor { HttpContext = new DefaultHttpContext() }) : new CapturingAuditWriter();
        var service = new ContractHandoverService(db, new TestAuthorization(), writer, storage, tenant,
            new ContractTemplatePreviewRenderer(), new FakePdfRenderer(), new ContractPlaceholderSourceRegistry());
        var scope = await service.ConfirmScopeAsync(ContractId, new() { ContractVersionId = VersionId,
            ContractRowVersion = Convert.ToBase64String(InitialVersion), VersionRowVersion = Convert.ToBase64String(InitialVersion),
            Items = [new(1701, HandoverScopeMode.Quantity), new(1702, HandoverScopeMode.WholePackage)] }, EmployeeId);
        return (service, scope);
    }
    private static byte[] HandoverDocx()
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true)) {
            var part = document.AddMainDocumentPart(); var body = new W.Body();
            foreach (var marker in new[] { "HANDOVER_CODE", "HANDOVER_DATE", "HANDOVER_LOCATION", "PROVIDER_COMPANY_NAME", "CUSTOMER_COMPANY_NAME", "CUSTOMER_REPRESENTATIVE_PHONE", "DOCUMENT_REFERENCES", "HANDOVER_ITEM_TABLE", "HANDOVER_SECTIONS", "SIGNATURE_PROVIDER", "SIGNATURE_CUSTOMER" })
                body.Append(new W.Paragraph(new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center }), new W.Run(new W.Text("{{" + marker + "}}"))));
            part.Document = new W.Document(body); part.Document.Save();
        } return stream.ToArray();
    }
}
