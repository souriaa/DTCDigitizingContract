using ContractManagement.API.Common.Enums;
using ContractManagement.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.ContractTemplate;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using static ContractManagement.Tests.ContractRichTextTestData;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task HandoverAcceptancePaymentCompletion_ServiceFlowBothDirections(ContractDirection direction)
    {
        await using var db=CreateContext();var storage=new TrackingStorage();
        var (handover,scope)=await HandoverFixture(db,storage,direction,contractTotal:1_000_000m);
        var create=HandoverRequest(scope.Items[0].HandoverScopeItemId,2);
        create.Lines=[new(scope.Items[0].HandoverScopeItemId,2),new(scope.Items[1].HandoverScopeItemId,1)];
        var draft=await handover.CreateAsync(ContractId,create,EmployeeId);
        var tenant=new CurrentTenant();tenant.Set(new ResolvedTenant(1,"TENANT-PHASE7","Tenant",TenantDatabaseMode.Dedicated,"InMemory"));
        var completion=new ContractCompletionService(db,new TestAuthorization(),new CapturingAuditWriter(),storage,tenant);
        Assert.False((await completion.GetReadinessAsync(ContractId,EmployeeId)).HandoverCompleted);
        var locked=await handover.FinalizeAsync(ContractId,draft.Record.HandoverRecordId,new(){RowVersion=draft.Record.RowVersion},EmployeeId);
        Assert.False((await completion.GetReadinessAsync(ContractId,EmployeeId)).HandoverCompleted);
        var signed=await handover.SignAsync(ContractId,draft.Record.HandoverRecordId,new(){
            RowVersion=locked.Record.RowVersion,File=FormFile("handover.pdf","application/pdf","%PDF-proof"),
            SignDate=new DateTime(2026,9,19),ConfirmBothPartiesSigned=true},EmployeeId);
        var afterHandover=await completion.GetReadinessAsync(ContractId,EmployeeId);
        Assert.True(afterHandover.HandoverCompleted);Assert.False(afterHandover.AcceptanceCompleted);
        Assert.All(await db.TblContractPaymentMilestones.ToListAsync(),m=>Assert.Null(m.AnchorDate));

        var source=CreateTemplateDocx();storage.Add("templates/nt-full.docx",source);
        db.TblFileStorages.Add(new(){FileId=177,ObjectType="ContractTemplateVersion",ObjectId=174,FileName="nt.docx",
            FilePath="",StorageKey="templates/nt-full.docx",TenantCode="TENANT-PHASE7",ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            FileType="docx",FileSize=source.Length,Sha256=Sha(source)});
        db.TblContractTemplates.Add(new(){TemplateId=173,TemplateCode="NT-FULL",TemplateName="Mẫu nghiệm thu",DocumentType=(byte)TemplateDocumentType.AcceptanceRecord,
            LanguageMode=(byte)ContractLanguageMode.Vietnamese,IsActive=true,CurrentPublishedVersionId=174});
        db.TblContractTemplateVersions.Add(new(){TemplateVersionId=174,TemplateId=173,VersionNo=1,ContractDirection=(byte)direction,
            Status=(byte)TemplateVersionStatus.Published,ValidationStatus=(byte)TemplateValidationStatus.Valid,DocumentFileId=177,
            DocumentHash=Sha(source),CreatedEmployeeId=EmployeeId,CreatedDate=DateTime.UtcNow,RowVersion=InitialVersion.ToArray()});
        db.TblContractTemplateTerms.Add(new(){TemplateVersionId=174,TermCode="RESULT",TermTitle="Kết quả",TermContent=RichText("Đã nghiệm thu"),
            DisplayOrder=1,CreatedEmployeeId=EmployeeId,CreatedDate=DateTime.UtcNow,RowVersion=InitialVersion.ToArray()});
        await db.SaveChangesAsync();
        var acceptance=CreateService(db,storage);var req=CreateRequest();req.TemplateVersionId=174;req.AcceptanceKind=AcceptanceKind.Final;req.PaymentMilestoneIds=[];
        var nt=await acceptance.CreateAsync(ContractId,req,EmployeeId);
        var options=await acceptance.HandoverOptionsAsync(ContractId,nt.AcceptanceRecordId,EmployeeId);
        nt=await acceptance.ReplaceHandoverScopeAsync(ContractId,nt.AcceptanceRecordId,new(){
            RowVersion=nt.RowVersion,Items=options.Items.Select(x=>new ContractManagement.API.Domains.DTOs.Requests.Contract.AcceptanceHandoverScopeInput(x.HandoverLineId,x.AvailableQuantity)).ToList()},EmployeeId);
        nt=await acceptance.FinalizeAsync(ContractId,nt.AcceptanceRecordId,new(){RowVersion=nt.RowVersion},EmployeeId);
        Assert.False((await completion.GetReadinessAsync(ContractId,EmployeeId)).AcceptanceCompleted);
        await acceptance.UploadSignedEvidenceAsync(ContractId,nt.AcceptanceRecordId,new(){RowVersion=nt.RowVersion,SignDate=new DateTime(2026,9,20),File=FormFile("nt-signed.pdf","application/pdf","%PDF-accept")},EmployeeId);
        Assert.True((await completion.GetReadinessAsync(ContractId,EmployeeId)).AcceptanceCompleted);
        Assert.All(await db.TblContractPaymentMilestones.ToListAsync(),m=>Assert.NotNull(m.AnchorDate));
        foreach(var milestone in await db.TblContractPaymentMilestones.ToListAsync())
        {
            var state=await completion.GetAsync(ContractId,EmployeeId);
            await completion.CompletePaymentMilestoneAsync(ContractId,VersionId,milestone.PaymentMilestoneId,new(){
                CurrentVersionId=VersionId,ContractRowVersion=state.ContractRowVersion,VersionRowVersion=state.VersionRowVersion,
                MilestoneRowVersion=Convert.ToBase64String(milestone.RowVersion),PaymentDate=DateTime.UtcNow.Date,
                PaymentMethod="CK",ReferenceCode="PAY-"+milestone.PaymentMilestoneId,
                EvidenceFile=FormFile("receipt.pdf","application/pdf","%PDF-receipt")},EmployeeId);
        }
        var ready=await completion.GetAsync(ContractId,EmployeeId);Assert.True(ready.Readiness.Ready);
        var done=await completion.CompleteAsync(ContractId,new(){CurrentVersionId=VersionId,
            ContractRowVersion=ready.ContractRowVersion,VersionRowVersion=ready.VersionRowVersion},EmployeeId);
        Assert.Equal(ContractStatus.Completed,done.ContractStatus);Assert.Empty(done.Readiness.Blockers);
        Assert.True(done.Readiness.Signed);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>handover.CreateAsync(ContractId,create,EmployeeId));
        var preserved=await handover.DownloadAsync(ContractId,signed.Record.HandoverRecordId,"docx",false,EmployeeId);
        Assert.Equal(locked.Artifacts.Single(x=>x.Format=="docx").Sha256,Sha(preserved.Content));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task HandoverEvents_StageOwnerNotificationOutboxInSameWorkflow_WithoutDuplicates(ContractDirection direction)
    {
        await using var db=CreateContext();var storage=new TrackingStorage();
        var (_,scope)=await HandoverFixture(db,storage,direction);
        const int actor=750;db.TblEmployees.Add(new(){EmployeeId=actor,EmployeeFullName="Kỹ thuật bàn giao",Status=1,RowVersion=InitialVersion.ToArray()});await db.SaveChangesAsync();
        var tenant=new CurrentTenant();tenant.Set(new ResolvedTenant(1,"TENANT-PHASE7","Tenant",TenantDatabaseMode.Dedicated,"InMemory"));
        var producer=new ContractNotificationProducer(db,new NotificationPublisher(db,[new ContractNotificationResourcePolicy(db,new TestAuthorization())]));
        var service=new ContractHandoverService(db,new TestAuthorization(),new CapturingAuditWriter(),storage,tenant,
            new ContractTemplatePreviewRenderer(),new FakePdfRenderer(),new ContractPlaceholderSourceRegistry(),producer);
        var draft=await service.CreateAsync(ContractId,HandoverRequest(scope.Items[0].HandoverScopeItemId,1),actor);
        var finalized=await service.FinalizeAsync(ContractId,draft.Record.HandoverRecordId,new(){RowVersion=draft.Record.RowVersion},actor);
        await service.SignAsync(ContractId,draft.Record.HandoverRecordId,new(){RowVersion=finalized.Record.RowVersion,
            File=FormFile("signed.pdf","application/pdf","%PDF-signed"),SignDate=new DateTime(2026,9,19),ConfirmBothPartiesSigned=true},actor);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SignAsync(ContractId,draft.Record.HandoverRecordId,new(){
            RowVersion=finalized.Record.RowVersion,File=FormFile("signed.pdf","application/pdf","%PDF-signed"),SignDate=new DateTime(2026,9,19),ConfirmBothPartiesSigned=true},actor));
        var request=HandoverRequest(scope.Items[0].HandoverScopeItemId,1);request.HandoverCode="NEXT";
        var next=await service.CreateAsync(ContractId,request,actor);
        next=await service.FinalizeAsync(ContractId,next.Record.HandoverRecordId,new(){RowVersion=next.Record.RowVersion},actor);
        await service.CancelAsync(ContractId,next.Record.HandoverRecordId,new(){RowVersion=next.Record.RowVersion,Reason="Sai chứng từ"},actor);
        var notifications=await db.TblNotifications.AsNoTracking().Include(x=>x.Recipients).Include(x=>x.DispatchOutbox).OrderBy(x=>x.NotificationId).ToListAsync();
        Assert.Equal(new[]{"HandoverCreated","HandoverFinalized","HandoverSigned","HandoverCreated","HandoverFinalized","HandoverCancelled"},notifications.Select(x=>x.EventCode));
        Assert.All(notifications,n=>{Assert.Equal("handover",n.ActionKey);Assert.Equal(EmployeeId,Assert.Single(n.Recipients).EmployeeId);
            Assert.Equal("Pending",n.DispatchOutbox!.Status);Assert.Null(n.Body);});
        request.HandoverCode="SELF";await service.CreateAsync(ContractId,request,EmployeeId);
        Assert.Equal(6,await db.TblNotifications.CountAsync());
    }
}
