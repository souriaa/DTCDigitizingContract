using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    private static ContractAuditWriter RealAcceptanceAudit(DbDtctechContext db)
    {
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(1, "TENANT-PHASE7", "Tenant Phase 7", TenantDatabaseMode.Dedicated, "InMemory"));
        return new ContractAuditWriter(db, tenant,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task SignedEvidence_RealAuditWriterPersistsSignDateAndAnchorsPayment(ContractDirection direction)
    {
        await using var db = CreateContext();
        var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: direction);
        var service = CreateService(db, storage, RealAcceptanceAudit(db));
        var draft = await service.CreateAsync(ContractId, CreateRequest(), EmployeeId);
        draft = await SelectFixtureHandoverScopeAsync(db, storage, service, draft);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        var signDate = new DateTime(2026, 9, 20);
        var evidence = await service.UploadSignedEvidenceAsync(ContractId, finalized.AcceptanceRecordId,
            new() { SignDate = signDate, RowVersion = finalized.RowVersion,
                File = FormFile("signed.pdf", "application/pdf", "%PDF-sign") }, EmployeeId);

        db.ChangeTracker.Clear();
        var record = await service.GetAsync(ContractId, finalized.AcceptanceRecordId, EmployeeId);
        Assert.Equal(AcceptanceRecordStatus.Signed, record.Status);
        Assert.Equal(signDate, record.SignedEvidence!.SignDate);
        var audit = await db.TblContractAudits.AsNoTracking().Include(x => x.Values)
            .SingleAsync(x => x.ActionType == ContractAuditActionTypes.AcceptanceEvidenceUploaded);
        Assert.Equal(ContractAuditSubjectTypes.AcceptanceEvidence, audit.SubjectType);
        Assert.Equal(evidence.AcceptanceEvidenceId, audit.SubjectId);
        var value = Assert.Single(audit.Values, x => x.FieldCode == (short)ContractAuditFieldCode.SignDate);
        Assert.Equal(AuditValueSide.New, value.ValueSide);
        Assert.Equal(AuditScalarValueKind.DateTime, value.ValueKind);
        Assert.Equal(signDate, value.DateTimeValue);
        Assert.Equal(evidence.UploadedAt, audit.OccurredAt);
        var milestone = await db.TblContractPaymentMilestones.SingleAsync(x => x.PaymentMilestoneId == MilestoneId);
        Assert.Equal(signDate, milestone.AnchorDate);
        Assert.Equal(signDate.AddDays(5), milestone.DueDate);
    }

    [Fact]
    public async Task SignedEvidenceAudit_StillRejectsUnrelatedFields()
    {
        await using var db = CreateContext();
        var writer = RealAcceptanceAudit(db);
        Assert.Throws<InvalidOperationException>(() => writer.StageEmployeeAudits([new(
            ContractId, VersionId, EmployeeId, ContractAuditActionTypes.AcceptanceEvidenceUploaded,
            ContractAuditResults.Succeeded, DateTime.UtcNow,
            SubjectType: ContractAuditSubjectTypes.AcceptanceEvidence, SubjectId: 1,
            NewValues: ContractAuditValues.Create(("Amount", 100m)))]));
        Assert.Empty(db.TblContractAudits.Local);
    }
}
