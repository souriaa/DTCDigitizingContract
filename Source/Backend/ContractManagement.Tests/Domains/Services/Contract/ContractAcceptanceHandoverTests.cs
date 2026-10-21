using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Fact]
    public async Task Acceptance_NoPaymentMilestones_CompletesByScopeWithoutAllowingDuplicate()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage, omitMilestones: true);
        var service = CreateService(db, storage); var request = CreateRequest(); request.PaymentMilestoneIds = [];
        var draft = await service.CreateAsync(ContractId, request, EmployeeId); await SeedSignedHandoverScopeAsync(db, storage);
        draft = await service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, Items = [new(10200,1), new(10201,1)] }, EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20), File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, EmployeeId);
        request.AcceptanceCode = "NT-AFTER";
        Assert.Equal("AcceptanceAlreadyCompleted", (await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(ContractId, request, EmployeeId))).Code);
    }

    [Fact]
    public async Task Acceptance_RejectsDifferentSignedVersion()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage); var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        await SeedSignedHandoverScopeAsync(db, storage, versionId: VersionId + 1);
        Assert.Empty((await service.HandoverOptionsAsync(ContractId, draft.AcceptanceRecordId, EmployeeId)).Items);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, Items = [new(10200,1)] }, EmployeeId));
    }

    [Fact]
    public async Task Acceptance_CannotMoveFinalizedAllocationToAnotherDraft()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage); var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId);
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(999, AcceptanceRecordStatus.Draft, AcceptanceKind.Partial));
        (await db.TblContractAcceptanceScopeAllocations.SingleAsync()).AcceptanceRecordId = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Acceptance_LastPartialMustNotStrandUnacceptedMilestones()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage); var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        await SeedSignedHandoverScopeAsync(db, storage);
        draft = await service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, Items = [new(10200,1), new(10201,1)] }, EmployeeId);
        Assert.Contains("thiếu đợt thanh toán", draft.HandoverBlocker);
        Assert.Equal("AcceptanceHandoverCoverageRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.FinalizeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId))).Code);
        draft = await service.ReplaceMilestonesAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, PaymentMilestoneIds = [MilestoneId, MilestoneId+1] }, EmployeeId);
        Assert.Null(draft.HandoverBlocker);
        await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId);
    }

    [Theory]
    [InlineData(1, ContractId, "TENANT-PHASE7")]
    [InlineData(3, ContractId, "TENANT-PHASE7")]
    [InlineData(2, ContractId + 1, "TENANT-PHASE7")]
    [InlineData(2, ContractId, "OTHER-TENANT")]
    public async Task Acceptance_RejectsUnsignedCancelledForeignContractOrTenantSource(byte status, int owner, string tenantCode)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage); var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        await SeedSignedHandoverScopeAsync(db, storage, status, owner, tenantCode);
        var options = await service.HandoverOptionsAsync(ContractId, draft.AcceptanceRecordId, EmployeeId);
        Assert.Empty(options.Items);
        Assert.Equal("AcceptanceHandoverSourceInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion, Items = [new(10200, 1)] }, EmployeeId))).Code);
    }

    [Fact]
    public async Task Acceptance_WholePackageCannotBeSplit()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage); var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        await SeedSignedHandoverScopeAsync(db, storage, scopeMode: 2);
        await Assert.ThrowsAnyAsync<Exception>(() => service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion, Items = [new(10200, 0.5m)] }, EmployeeId));
        Assert.Empty(await db.TblContractAcceptanceScopeAllocations.ToListAsync());
    }

    // An explicit signed-source fixture, not production auto-selection or old-data fallback.
    private static async Task SeedSignedHandoverScopeAsync(DbDtctechContext db, TrackingStorage storage, byte status = 2, int contractId = ContractId, string tenantCode = "TENANT-PHASE7", byte scopeMode = 1, int versionId = VersionId)
    {
        if (await db.TblContractHandoverBaselines.AnyAsync()) return;
        storage.Add("handover/proof.pdf", "%PDF-signed-handover"u8.ToArray());
        db.TblContractHandoverBaselines.Add(new() { HandoverBaselineId = 10001, ContractId = ContractId, ContractVersionId = versionId,
            ConfirmedByEmployeeId = EmployeeId, ConfirmedAt = DateTime.UtcNow });
        db.TblContractHandoverScopeItems.Add(new() { HandoverScopeItemId = 10002, HandoverBaselineId = 10001,
            SourceContractItemId = 1701, ScopeMode = scopeMode, ItemCode = "SCOPE-1", NameVi = "Phần mềm đã ký", NameEn = "Signed software",
            UnitVi = "Bộ", UnitEn = "Set", SourceQuantity = 2, RequiredQuantity = 2, DisplayOrder = 1 });
        for (var i = 0; i < 2; i++)
        {
            db.TblContractHandoverRecords.Add(new() { HandoverRecordId = 10100 + i, ContractId = contractId,
                ContractVersionId = versionId, HandoverBaselineId = 10001, TemplateVersionId = TemplateVersionId,
                HandoverCode = "BBBG-" + (i+1), HandoverDate = new DateTime(2026,9,14+i),
                ContractSignDate = new DateTime(2026,9,12), Location = "Hà Nội", Status = status,
                SnapshotHash = new string('a',64), CreatedEmployeeId = EmployeeId, CreatedAt = DateTime.UtcNow,
                RowVersion = InitialVersion.ToArray() });
            db.TblContractHandoverLines.Add(new() { HandoverLineId = 10200+i, HandoverRecordId = 10100+i,
                HandoverScopeItemId = 10002, Quantity = 1, DisplayOrder = 1 });
            db.TblFileStorages.Add(new() { FileId = 10300+i, ObjectType = "ContractHandoverEvidence", ObjectId = 10100+i,
                FileName = "bàn giao.pdf", FilePath = "", StorageKey = "handover/proof.pdf",
                TenantCode = tenantCode, ContentType = "application/pdf", FileType = "pdf", FileSize = 20,
                Sha256 = Sha("%PDF-signed-handover"u8.ToArray()) });
            db.TblContractHandoverEvidences.Add(new() { HandoverEvidenceId = 10400+i, HandoverRecordId = 10100+i,
                FileId = 10300+i, SignDate = new DateTime(2026,9,16+i), UploadedByEmployeeId = EmployeeId, UploadedAt = DateTime.UtcNow });
        }
        await db.SaveChangesAsync();
    }
    private static async Task<ContractAcceptanceRecordResponse> SelectFixtureHandoverScopeAsync(DbDtctechContext db, TrackingStorage storage,
        ContractAcceptanceService service, ContractAcceptanceRecordResponse draft, int lineId = 10200, decimal quantity = 1)
    {
        await SeedSignedHandoverScopeAsync(db, storage);
        return await service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId, new() {
            RowVersion = draft.RowVersion,
            Items = draft.AcceptanceKind == AcceptanceKind.Final ? [new(10200,1), new(10201,1)] : [new(lineId, quantity)]
        }, EmployeeId);
    }

    [Fact]
    public async Task Acceptance_DraftWithoutHandoverAllowed_ButCannotFinalize()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        Assert.Empty(draft.HandoverScope); Assert.NotNull(draft.HandoverBlocker);
        Assert.Equal("AcceptanceHandoverCoverageRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.FinalizeAsync(ContractId,draft.AcceptanceRecordId,new() { RowVersion = draft.RowVersion },EmployeeId))).Code);
        Assert.Empty(storage.SavedKeys);
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Acceptance_ReferencesMultipleSignedHandovers_FinalCoversEntireBaseline(ContractDirection direction)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db,storage,direction:direction);
        var service = CreateService(db,storage); var request = CreateRequest(); request.AcceptanceKind=AcceptanceKind.Final; request.PaymentMilestoneIds=[];
        var draft = await service.CreateAsync(ContractId,request,EmployeeId); await SeedSignedHandoverScopeAsync(db,storage);
        var one = await service.ReplaceHandoverScopeAsync(ContractId,draft.AcceptanceRecordId,new() { RowVersion=draft.RowVersion,Items=[new(10200,1)] },EmployeeId);
        Assert.NotNull(one.HandoverBlocker);
        Assert.Equal("AcceptanceHandoverCoverageRequired",(await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.FinalizeAsync(ContractId,one.AcceptanceRecordId,new(){RowVersion=one.RowVersion},EmployeeId))).Code);
        var all = await SelectFixtureHandoverScopeAsync(db,storage,service,one);
        Assert.Null(all.HandoverBlocker); Assert.Equal(2,all.HandoverScope.Count);
        Assert.Contains(all.References,x=>x.ReferenceType==AcceptanceReferenceType.Handover&&x.TextVi.Contains("ký ngày 16/09/2026"));
        Assert.Contains(all.References,x=>x.ReferenceType==AcceptanceReferenceType.Handover&&x.TextVi.Contains("ký ngày 17/09/2026"));
        var preview = await service.PreviewAsync(ContractId,all.AcceptanceRecordId,"docx",EmployeeId);
        using var doc = WordprocessingDocument.Open(new MemoryStream(preview.Content),false);
        Assert.Contains("BBBG-1",doc.MainDocumentPart!.Document!.InnerText); Assert.Contains("BBBG-2",doc.MainDocumentPart!.Document!.InnerText);
        var finalized=await service.FinalizeAsync(ContractId,all.AcceptanceRecordId,new(){RowVersion=all.RowVersion},EmployeeId);
        Assert.NotNull(finalized.SnapshotHash);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReplaceHandoverScopeAsync(ContractId,all.AcceptanceRecordId,new(){RowVersion=finalized.RowVersion},EmployeeId));
        db.TblContractAcceptanceScopeAllocations.Remove(await db.TblContractAcceptanceScopeAllocations.FirstAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>db.SaveChangesAsync());
    }

    [Fact]
    public async Task Acceptance_ScopeRejectsForeignLine_Excess_Duplicate_Stale()
    {
        await using var db=CreateContext();var storage=new TrackingStorage();await SeedAsync(db,storage);
        var service=CreateService(db,storage);var draft=await service.CreateAsync(ContractId,CreateRequest(),EmployeeId);
        await SeedSignedHandoverScopeAsync(db,storage);
        foreach(var selection in new[] {
            new ContractManagement.API.Domains.DTOs.Requests.Contract.AcceptanceHandoverScopeInput[] {new(999,1)},
            [new(10200,2)], [new(10200,1),new(10200,1)], [new(10200,0)], [new(10200,0.00001m)]
        }) {
            await Assert.ThrowsAnyAsync<Exception>(()=>service.ReplaceHandoverScopeAsync(ContractId,draft.AcceptanceRecordId,
                new(){RowVersion=draft.RowVersion,Items=selection.ToList()},EmployeeId));
        }
        draft=await SelectFixtureHandoverScopeAsync(db,storage,service,draft);
        Assert.Single(await db.TblContractAcceptanceScopeAllocations.ToListAsync());
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>service.ReplaceHandoverScopeAsync(ContractId,draft.AcceptanceRecordId,
            new(){RowVersion=Convert.ToBase64String([9]),Items=[new(10200,1)]},EmployeeId));
    }

    [Fact]
    public async Task Acceptance_PartialMultipleRounds_SameDelivery_ScopeOnlyRoundAfterMilestones()
    {
        await using var db=CreateContext();var storage=new TrackingStorage();await SeedAsync(db,storage);
        var service=CreateService(db,storage);
        var first=await service.CreateAsync(ContractId,CreateRequest(),EmployeeId);
        first=await SelectFixtureHandoverScopeAsync(db,storage,service,first,10200,0.25m);
        var final1=await service.FinalizeAsync(ContractId,first.AcceptanceRecordId,new(){RowVersion=first.RowVersion},EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId,first.AcceptanceRecordId,new(){RowVersion=final1.RowVersion,SignDate=new DateTime(2026,9,20),File=FormFile("signed.pdf","application/pdf","%PDF-signed")},EmployeeId);
        var request=CreateRequest();request.AcceptanceCode="NT-2";request.PaymentMilestoneIds=[MilestoneId+1];
        var second=await service.CreateAsync(ContractId,request,EmployeeId);
        second=await SelectFixtureHandoverScopeAsync(db,storage,service,second,10200,0.75m);
        var final2=await service.FinalizeAsync(ContractId,second.AcceptanceRecordId,new(){RowVersion=second.RowVersion},EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId,second.AcceptanceRecordId,new(){RowVersion=final2.RowVersion,SignDate=new DateTime(2026,9,21),File=FormFile("signed.pdf","application/pdf","%PDF-signed")},EmployeeId);
        request.AcceptanceCode="NT-3";request.PaymentMilestoneIds=[];
        var third=await service.CreateAsync(ContractId,request,EmployeeId);
        third=await SelectFixtureHandoverScopeAsync(db,storage,service,third,10201,1);
        Assert.Empty(third.Milestones);
        var final3=await service.FinalizeAsync(ContractId,third.AcceptanceRecordId,new(){RowVersion=third.RowVersion},EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId,third.AcceptanceRecordId,new(){RowVersion=final3.RowVersion,SignDate=new DateTime(2026,9,22),File=FormFile("signed.pdf","application/pdf","%PDF-signed")},EmployeeId);
        Assert.Equal(new DateTime(2026,9,20), (await db.TblContractPaymentMilestones.SingleAsync(x => x.PaymentMilestoneId == MilestoneId)).AnchorDate);
        Assert.Equal(new DateTime(2026,9,21), (await db.TblContractPaymentMilestones.SingleAsync(x => x.PaymentMilestoneId == MilestoneId + 1)).AnchorDate);
        request.AcceptanceCode="NT-4";
        Assert.Equal("AcceptanceAlreadyCompleted",(await Assert.ThrowsAsync<BusinessRuleException>(()=>service.CreateAsync(ContractId,request,EmployeeId))).Code);
    }

    [Fact]
    public async Task Acceptance_CancelReleasesScope_WithoutDeletingHistoricalAllocations()
    {
        await using var db=CreateContext();var storage=new TrackingStorage();await SeedAsync(db,storage);
        var service=CreateService(db,storage);var draft=await service.CreateAsync(ContractId,CreateRequest(),EmployeeId);
        draft=await SelectFixtureHandoverScopeAsync(db,storage,service,draft);
        var finalized=await service.FinalizeAsync(ContractId,draft.AcceptanceRecordId,new(){RowVersion=draft.RowVersion},EmployeeId);
        await service.CancelAsync(ContractId,draft.AcceptanceRecordId,new(){RowVersion=finalized.RowVersion,Reason="Sai phạm vi"},EmployeeId);
        var request=CreateRequest();request.AcceptanceCode="NT-NEW";var next=await service.CreateAsync(ContractId,request,EmployeeId);
        var options=await service.HandoverOptionsAsync(ContractId,next.AcceptanceRecordId,EmployeeId);
        Assert.Equal(1,options.Items.Single(x=>x.HandoverLineId==10200).AvailableQuantity);
        Assert.Single(await db.TblContractAcceptanceScopeAllocations.ToListAsync());
    }

    [Fact]
    public async Task Acceptance_FinalizeChecksCoverageAgain_IfAnotherRecordReservedIt()
    {
        await using var db=CreateContext();var storage=new TrackingStorage();await SeedAsync(db,storage);
        var service=CreateService(db,storage);var draft=await service.CreateAsync(ContractId,CreateRequest(),EmployeeId);
        draft=await SelectFixtureHandoverScopeAsync(db,storage,service,draft);
        // A persisted conflicting finalized aggregate, representing an external concurrent writer.
        db.TblContractAcceptanceRecords.Add(AcceptanceRecord(999,AcceptanceRecordStatus.Finalized,AcceptanceKind.Partial));
        db.TblContractAcceptanceScopeAllocations.Add(new(){AcceptanceRecordId=999,HandoverLineId=10200,Quantity=1});
        await db.SaveChangesAsync();
        Assert.Equal("AcceptanceHandoverCoverageRequired",(await Assert.ThrowsAsync<BusinessRuleException>(()=>
            service.FinalizeAsync(ContractId,draft.AcceptanceRecordId,new(){RowVersion=draft.RowVersion},EmployeeId))).Code);
        Assert.Empty(storage.SavedKeys);
    }
}
