using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale, AcceptanceKind.Partial)]
    [InlineData(ContractDirection.Purchase, AcceptanceKind.Partial)]
    [InlineData(ContractDirection.Sale, AcceptanceKind.Final)]
    [InlineData(ContractDirection.Purchase, AcceptanceKind.Final)]
    public async Task Acceptance_WithoutHandover_CanPreviewFinalizeAndSign(ContractDirection direction, AcceptanceKind kind)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage, direction: direction);
        var service = CreateService(db, storage);
        var request = CreateRequest(); request.AcceptanceKind = kind;
        if (kind == AcceptanceKind.Final) request.PaymentMilestoneIds = [];
        var draft = await service.CreateAsync(ContractId, request, EmployeeId);
        var preview = await service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId);
        Assert.NotEmpty(preview.Content);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        Assert.Equal(AcceptanceRecordStatus.Finalized, finalized.Status);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, EmployeeId);
        Assert.Equal((byte)AcceptanceRecordStatus.Signed, (await db.TblContractAcceptanceRecords.SingleAsync()).Status);
    }

    [Fact]
    public async Task Acceptance_PartialStillRequiresPaymentMilestone()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var request = CreateRequest(); request.PaymentMilestoneIds = [];
        var service = CreateService(db, storage);
        Assert.Equal("AcceptanceMilestoneRequired", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, request, EmployeeId))).Code);
        Assert.Empty(await db.TblContractAcceptanceRecords.ToListAsync());
    }

    [Fact]
    public async Task Acceptance_NoPaymentMilestones_FinalCompletesWithoutDuplicate()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage, omitMilestones: true);
        var service = CreateService(db, storage); var request = CreateRequest();
        request.PaymentMilestoneIds = []; request.AcceptanceKind = AcceptanceKind.Final;
        var draft = await service.CreateAsync(ContractId, request, EmployeeId);
        var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = draft.RowVersion }, EmployeeId);
        await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
            new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20),
                File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, EmployeeId);
        request.AcceptanceCode = "NT-AFTER";
        Assert.Equal("AcceptanceFinalAlreadySigned", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, request, EmployeeId))).Code);
    }

    [Fact]
    public async Task Acceptance_PartialMultipleRounds_StopsAfterAllMilestones()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage(); await SeedAsync(db, storage);
        var service = CreateService(db, storage);
        var request = CreateRequest();
        for (var index = 0; index < 2; index++)
        {
            request.AcceptanceCode = $"NT-{index + 1}"; request.PaymentMilestoneIds = [MilestoneId + index];
            var draft = await service.CreateAsync(ContractId, request, EmployeeId);
            var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, EmployeeId);
            await service.UploadSignedEvidenceAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = finalized.RowVersion, SignDate = new DateTime(2026, 9, 20 + index),
                    File = FormFile("signed.pdf", "application/pdf", "%PDF-signed") }, EmployeeId);
        }
        Assert.Equal(new DateTime(2026, 9, 20), (await db.TblContractPaymentMilestones.SingleAsync(x => x.PaymentMilestoneId == MilestoneId)).AnchorDate);
        Assert.Equal(new DateTime(2026, 9, 21), (await db.TblContractPaymentMilestones.SingleAsync(x => x.PaymentMilestoneId == MilestoneId + 1)).AnchorDate);
        request.AcceptanceCode = "NT-3"; request.PaymentMilestoneIds = [];
        Assert.Equal("AcceptanceAlreadyCompleted", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, request, EmployeeId))).Code);
    }
}
