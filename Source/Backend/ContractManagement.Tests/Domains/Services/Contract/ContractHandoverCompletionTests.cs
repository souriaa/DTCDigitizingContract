using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Controllers.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Public;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.File;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractCompletionServicePhase10Tests
{
    private static void SeedCompletionScope(DbDtctechContext db, bool accepted, decimal acceptedQuantity)
    {
        db.TblContractHandoverBaselines.Add(new() { HandoverBaselineId = 13000, ContractId = ContractId, ContractVersionId = VersionId,
            ConfirmedByEmployeeId = TechnicalId, ConfirmedAt = DateTime.UtcNow.AddDays(-1) });
        db.TblContractHandoverScopeItems.Add(new() { HandoverScopeItemId = 13001, HandoverBaselineId = 13000,
            SourceContractItemId = 13010, ScopeMode = 1, ItemCode = "SCOPE-1", NameVi = "Phần mềm đã ký", UnitVi = "Bộ",
            SourceQuantity = 2, RequiredQuantity = 2, DisplayOrder = 1 });
        db.TblContractHandoverRecords.Add(new() { HandoverRecordId = 13002, ContractId = ContractId, ContractVersionId = VersionId,
            HandoverBaselineId = 13000, TemplateVersionId = 0, HandoverCode = "BBBG-PHASE67", HandoverDate = DateTime.UtcNow.Date.AddDays(-1),
            ContractSignDate = DateTime.UtcNow.Date.AddDays(-2), Location = "Hà Nội", Status = 2, SnapshotHash = new string('a',64),
            CreatedEmployeeId = TechnicalId, CreatedAt = DateTime.UtcNow.AddDays(-1), RowVersion = InitialRowVersion.ToArray() });
        db.TblContractHandoverLines.Add(new() { HandoverLineId = 13003, HandoverRecordId = 13002,
            HandoverScopeItemId = 13001, Quantity = 2, DisplayOrder = 1 });
        var proof = FileMetadata(13004, "ContractHandoverEvidence", "ban-giao.pdf"); proof.ObjectId = 13002;
        db.TblFileStorages.Add(proof);
        db.TblContractHandoverEvidences.Add(new() { HandoverEvidenceId = 13005, HandoverRecordId = 13002, FileId = 13004,
            SignDate = DateTime.UtcNow.Date.AddDays(-1), UploadedByEmployeeId = TechnicalId, UploadedAt = DateTime.UtcNow });
        if (accepted) db.TblContractAcceptanceScopeAllocations.Add(new() { AcceptanceRecordId = 12012, HandoverLineId = 13003, Quantity = acceptedQuantity });
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Completion_AllMoneyAndSignedAcceptance_StillNeedsSignedHandoverCoverage(ContractDirection direction)
    {
        await using var db = CreateContext(); await SeedAsync(db, ContractStatus.Signed, includeAcceptance:true,
            activePayments:[ContractTotal], direction:direction, includeHandover:false);
        var service = CreateService(db, new TrackingPrivateStorage());
        var ready = await service.GetReadinessAsync(ContractId, ManagerId);
        Assert.False(ready.HandoverCompleted); Assert.False(ready.AcceptanceCompleted); Assert.False(ready.Ready);
        Assert.Contains(ready.Blockers,x=>x.Code=="HANDOVER_INCOMPLETE");
        Assert.Equal("ContractNotReadyForCompletion",(await Assert.ThrowsAsync<BusinessRuleException>(()=>
            service.CompleteAsync(ContractId,CompleteRequest(),ManagerId))).Code);
        Assert.Equal((byte)ContractStatus.Signed,(await db.TblContracts.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Completion_SignedDeliveryAndAllMoney_DoNotReplaceFullAcceptanceScope(ContractDirection direction)
    {
        await using var db = CreateContext(); await SeedAsync(db, ContractStatus.Signed, includeAcceptance:true,
            activePayments:[ContractTotal], direction:direction, acceptedScopeQuantity:1);
        var service = CreateService(db,new TrackingPrivateStorage()); var result=await service.GetReadinessAsync(ContractId,ManagerId);
        Assert.True(result.HandoverCompleted); Assert.False(result.AcceptanceScopeCompleted); Assert.False(result.AcceptanceCompleted);
        Assert.Equal(1,result.DeliveredScopeItemCount); Assert.Equal(0,result.AcceptedScopeItemCount);
        Assert.Equal(1,Assert.Single(result.ScopeItems).Accepted);
        Assert.Contains(result.Blockers,x=>x.Code=="ACCEPTANCE_INCOMPLETE");
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.CompleteAsync(ContractId,CompleteRequest(),ManagerId));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Completion_FullSignedScopesAndPayment_CompletesAndLocksExecution(ContractDirection direction)
    {
        await using var db = CreateContext(); await SeedAsync(db, ContractStatus.Signed, includeAcceptance:true,
            activePayments:[ContractTotal], direction:direction);
        var service=CreateService(db,new TrackingPrivateStorage());
        var ready=await service.GetReadinessAsync(ContractId,ManagerId);
        Assert.True(ready.HandoverCompleted);Assert.True(ready.AcceptanceCompleted);Assert.True(ready.PaymentCompleted);Assert.True(ready.Ready);
        var completed=await service.CompleteAsync(ContractId,CompleteRequest(),ManagerId);
        Assert.Equal(ContractStatus.Completed,completed.ContractStatus);
        Assert.True(completed.Readiness.Signed);Assert.Empty(completed.Readiness.Blockers);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.AddPaymentAsync(ContractId,PaymentRequest(1m,"AFTER-CLOSED"),AccountantId));
    }

    [Theory]
    [InlineData("OTHER-TENANT",true)]
    [InlineData("TENANT-PHASE12",false)]
    public async Task Completion_InvalidHandoverProofCannotCountAsDelivered(string tenantCode,bool hash)
    {
        await using var db=CreateContext();await SeedAsync(db,ContractStatus.Signed,includeAcceptance:true,activePayments:[ContractTotal]);
        var file=await db.TblFileStorages.SingleAsync(x=>x.FileId==13004);file.TenantCode=tenantCode;if(!hash)file.Sha256=null;
        await db.SaveChangesAsync();
        var ready=await CreateService(db,new TrackingPrivateStorage()).GetReadinessAsync(ContractId,ManagerId);
        Assert.False(ready.HandoverCompleted);Assert.False(ready.AcceptanceCompleted);Assert.False(ready.Ready);Assert.NotNull(ready.ScopeIntegrityError);
        Assert.Contains(ready.Blockers,x=>x.Code=="HANDOVER_INCOMPLETE"&&x.Message.Contains("bản ký"));
    }

    [Fact]
    public async Task Completion_CannotCloseWhileAnotherDraftNeedsResolution()
    {
        await using var db=CreateContext();await SeedAsync(db,ContractStatus.Signed,includeAcceptance:true,activePayments:[ContractTotal]);
        db.TblContractHandoverRecords.Add(new(){ContractId=ContractId,ContractVersionId=VersionId,HandoverBaselineId=13000,
            TemplateVersionId=0,HandoverCode="PENDING",HandoverDate=DateTime.UtcNow.Date,ContractSignDate=DateTime.UtcNow.Date.AddDays(-2),
            Location="Hà Nội",Status=0,CreatedEmployeeId=TechnicalId,CreatedAt=DateTime.UtcNow,RowVersion=InitialRowVersion.ToArray()});
        await db.SaveChangesAsync();
        var service=CreateService(db,new TrackingPrivateStorage());var ready=await service.GetReadinessAsync(ContractId,ManagerId);
        Assert.False(ready.Ready);Assert.Contains(ready.Blockers,x=>x.Code=="EXECUTION_DOCUMENT_IN_PROGRESS");
    }

    [Fact]
    public async Task HandoverFiles_UseParentContractAuthorization_AndCannotBeMutatedGenerically()
    {
        await using var db=CreateContext();await SeedAsync(db,ContractStatus.Signed,includeAcceptance:true);
        var auth=new FileResourceAuthorizationService(db,new ContractResourceAuthorizationService(db));
        await auth.EnsureCanReadFileAsync(13004,ManagerId);
        await auth.EnsureCanReadFileAsync(13004,OwnerId);
        await Assert.ThrowsAsync<RbacOperationException>(()=>auth.EnsureCanReadFileAsync(13004,OtherEmployeeId));
        await Assert.ThrowsAsync<RbacOperationException>(()=>auth.EnsureCanDeleteFileAsync(13004,ManagerId));
        await Assert.ThrowsAsync<RbacOperationException>(()=>auth.EnsureCanWriteByObjectAsync("ContractHandoverArtifact",13002,TechnicalId));
        await Assert.ThrowsAsync<RbacOperationException>(()=>auth.EnsureCanReadByObjectAsync("ContractHandoverArtifact",999,ManagerId));
    }

    [Fact]
    public void HandoverIsNotExposedThroughPublicNegotiationDto_AndControllerRequiresEmployeeSession()
    {
        Assert.DoesNotContain(typeof(CustomerSharedContractResponse).GetProperties(),x=>x.Name.Contains("Handover"));
        Assert.NotEmpty(typeof(ContractHandoverController).GetCustomAttributes(typeof(SessionAuthorizeAttribute),true));
    }
}
