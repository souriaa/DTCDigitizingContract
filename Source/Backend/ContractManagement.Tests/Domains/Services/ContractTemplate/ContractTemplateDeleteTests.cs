using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using ContractManagement.API.Domains.DTOs.Responses.ContractTemplate;
using ContractManagement.Common.Enums;
using ContractManagement.Domains.Interfaces.ContractTemplate;
using Microsoft.EntityFrameworkCore;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.ContractTemplate;

public sealed partial class ContractTemplatePreviewTests
{
    private static DeleteContractTemplateRequest DeleteRequest(ContractTemplateDetailResponse detail) => new()
    {
        RowVersion = detail.RowVersion,
        Versions = detail.Versions.Select(x => new DeleteContractTemplateVersionRequest {
            TemplateVersionId = x.TemplateVersionId, RowVersion = x.RowVersion }).ToList()
    };

    [Fact]
    public async Task Delete_UnusedDraft_RemovesAggregateAndFilesButKeepsAuditsAndOtherTemplates()
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        var service = CreateService(db, storage);
        var other = await service.CreateAsync(new() { ContractDirection = ContractDirection.Sale,
            TemplateCode = "KEEP-ME", TemplateName = "Keep me", LanguageMode = ContractLanguageMode.Vietnamese,
            DocumentType = TemplateDocumentType.SoftwareSupplyContract }, AdminOfficerId);
        var now = DateTime.UtcNow;
        db.TblContractTemplateAudits.Add(new() { TenantId = TenantId, TemplateId = TemplateId,
            TemplateVersionId = VersionId, ActorEmployeeId = AdminOfficerId,
            ActionType = ContractTemplateAuditActionTypes.DocumentUploaded, Result = ContractTemplateAuditResults.Succeeded,
            OccurredAt = now, CorrelationId = "before-delete" });
        db.TblContractTemplateTerms.Add(new() { TemplateTermId = 701, TemplateVersionId = VersionId,
            TermCode = "PAY", TermTitle = "Payment", TermKind = (byte)ContractTermKind.Payment,
            TermContent = RichText("Payment"), DisplayOrder = 1, CreatedEmployeeId = AdminOfficerId, CreatedDate = now, RowVersion = [1] });
        db.TblContractTemplatePaymentMilestones.Add(new() { TemplateVersionId = VersionId, TemplateTermId = 701,
            MilestoneCode = "P1", TitleVi = "Payment 1", PaymentPercent = 100, DueAnchor = 1, DayCountMode = 1,
            CreatedEmployeeId = AdminOfficerId, CreatedDate = now, RowVersion = [1] });
        db.TblContractTemplateAppendices.Add(new() { TemplateAppendixId = 801, TemplateVersionId = VersionId,
            AppendixCode = "A1", AppendixName = "Appendix", DisplayOrder = 1, CreatedEmployeeId = AdminOfficerId, CreatedDate = now, RowVersion = [1] });
        db.TblContractTemplateAppendixTerms.Add(new() { TemplateAppendixId = 801, TermCode = "T1", TermTitle = "Term",
            DisplayOrder = 1, CreatedEmployeeId = AdminOfficerId, CreatedDate = now, RowVersion = [1] });
        db.TblContractTemplateLegalBases.Add(new() { TemplateVersionId = VersionId, BasisCode = "B1", ContentVi = "Basis",
            DisplayOrder = 1, CreatedEmployeeId = AdminOfficerId, CreatedDate = now, RowVersion = [1] });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        await storage.SeedAsync(40, [1,2,3], "ContractTemplatePreview", VersionId, AdminOfficerId);
        (await db.TblContractTemplateVersions.SingleAsync(x => x.TemplateVersionId == VersionId)).PreviewFileId = 40;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var before = await service.GetAsync(TemplateId, AdminOfficerId);
        await service.DeleteAsync(TemplateId, DeleteRequest(before), AdminOfficerId);

        Assert.Equal(other.TemplateId, (await db.TblContractTemplates.SingleAsync()).TemplateId);
        Assert.DoesNotContain(await db.TblContractTemplateVersions.ToListAsync(), x => x.TemplateId == TemplateId);
        Assert.Empty(db.TblContractTemplateTerms); Assert.Empty(db.TblContractTemplateAppendices);
        Assert.Empty(db.TblContractTemplateAppendixTerms); Assert.Empty(db.TblContractTemplatePaymentMilestones);
        Assert.Empty(db.TblContractTemplateLegalBases);
        Assert.Empty(await db.TblContractTemplateFields.Where(x => x.TemplateVersionId == VersionId).ToListAsync());
        Assert.Empty(await db.TblContractTemplateItemTableColumnLayouts.Where(x => x.TemplateVersionId == VersionId).ToListAsync());
        Assert.Equal(new[] { 1, 40 }, storage.DeletedFileIds.Order());
        Assert.Empty(db.TblFileStorages);
        var audit = Assert.Single(await db.TblContractTemplateAudits.Where(x => x.ActionType == ContractTemplateAuditActionTypes.TemplateDeleted).ToListAsync());
        Assert.Equal(TemplateId, audit.TemplateId); Assert.Equal(VersionId, audit.TemplateVersionId);
        Assert.Contains(await db.TblContractTemplateAudits.ToListAsync(), x => x.CorrelationId == "before-delete");
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(TemplateId, AdminOfficerId));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetVersionAsync(VersionId, AdminOfficerId));
        var recreated = await service.CreateAsync(new() { ContractDirection = ContractDirection.Sale, TemplateCode = before.TemplateCode,
            TemplateName = "Recreated", LanguageMode = ContractLanguageMode.Vietnamese, DocumentType = before.DocumentType }, AdminOfficerId);
        Assert.NotEqual(TemplateId, recreated.TemplateId);
    }

    [Theory]
    [InlineData(InactiveAdminOfficerId)]
    public async Task Delete_RequiresActiveAdminOfficer(int actor)
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument()); var service = CreateService(db, storage);
        var request = DeleteRequest(await service.GetAsync(TemplateId, AdminOfficerId));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.DeleteAsync(TemplateId, request, actor));
        Assert.Single(db.TblContractTemplates); Assert.Empty(storage.DeletedFileIds);
    }

    [Theory]
    [InlineData("template")]
    [InlineData("version")]
    [InlineData("versions")]
    public async Task Delete_RejectsStaleSnapshot(string changed)
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument()); var service = CreateService(db, storage);
        var request = DeleteRequest(await service.GetAsync(TemplateId, AdminOfficerId));
        if (changed == "template") request.RowVersion = Convert.ToBase64String([9,9,9,9,9,9,9,9]);
        if (changed == "version") request.Versions[0].RowVersion = Convert.ToBase64String([9,9,9,9,9,9,9,9]);
        if (changed == "versions") request.Versions[0].TemplateVersionId = int.MaxValue;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.DeleteAsync(TemplateId, request, AdminOfficerId));
        Assert.Single(db.TblContractTemplates); Assert.Single(db.TblContractTemplateVersions); Assert.Empty(storage.DeletedFileIds);
    }

    [Theory]
    [InlineData(TemplateVersionStatus.Published)]
    [InlineData(TemplateVersionStatus.Retired)]
    public async Task Delete_RejectsPublishedAndRetiredTemplates(TemplateVersionStatus status)
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        (await db.TblContractTemplateVersions.SingleAsync()).Status = (byte)status;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var service = CreateService(db, storage);
        var request = DeleteRequest(await service.GetAsync(TemplateId, AdminOfficerId));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(TemplateId, request, AdminOfficerId));
        Assert.Equal("TemplateAlreadyPublished", error.Code); Assert.Empty(storage.DeletedFileIds);
        Assert.Single(db.TblContractTemplates);
    }

    [Theory]
    [InlineData("contract")]
    [InlineData("version")]
    [InlineData("acceptance")]
    [InlineData("handover")]
    public async Task Delete_RejectsReferencedDraftEvenWithoutPhysicalForeignKeys(string source)
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        if (source == "contract") db.TblContracts.Add(new() { TemplateVersionId = VersionId, ContractCode = "HD-1",
            ContractName = "Contract", CurrencyCode = "VND", RowVersion = [1] });
        if (source == "version") db.TblContractVersions.Add(new() { TemplateVersionId = VersionId, RowVersion = [1] });
        if (source == "acceptance") db.TblContractAcceptanceRecords.Add(new() { TemplateVersionId = VersionId,
            AcceptanceCode = "NT-1", Location = "Hanoi", RowVersion = [1] });
        if (source == "handover") db.TblContractHandoverRecords.Add(new() { TemplateVersionId = VersionId,
            HandoverCode = "BG-1", Location = "Hanoi", RowVersion = [1] });
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var service = CreateService(db, storage);
        var request = DeleteRequest(await service.GetAsync(TemplateId, AdminOfficerId));
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() => service.DeleteAsync(TemplateId, request, AdminOfficerId));
        Assert.Equal("TemplateInUse", error.Code); Assert.Single(db.TblContractTemplates); Assert.Empty(storage.DeletedFileIds);
    }

    [Fact]
    public async Task Delete_RejectsMismatchedFileOwnership()
    {
        await using var db = CreateContext(); var storage = new TestFileStorage(db);
        await SeedValidDraftAsync(db, storage, CreateSourceDocument());
        (await db.TblFileStorages.SingleAsync()).ObjectId = 999;
        await db.SaveChangesAsync(); db.ChangeTracker.Clear(); var service = CreateService(db, storage);
        var request = DeleteRequest(await service.GetAsync(TemplateId, AdminOfficerId));
        Assert.Equal("TemplateFileOwnershipInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DeleteAsync(TemplateId, request, AdminOfficerId))).Code);
        Assert.Single(db.TblContractTemplates); Assert.Empty(storage.DeletedFileIds);
    }
}
