using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractAcceptanceServicePhase7Tests
{
    [Fact]
    public async Task Acceptance_CreateAndUpdateRejectDateBeforeContractSignature()
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage); var service = CreateService(db, storage);
        var request = CreateRequest(); request.AcceptanceDate = new DateTime(2026, 9, 11);
        Assert.Equal("AcceptanceDateInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(ContractId, request, EmployeeId))).Code);
        Assert.Empty(db.TblContractAcceptanceRecords);
        request.AcceptanceDate = new DateTime(2026, 9, 12);
        var draft = await service.CreateAsync(ContractId, request, EmployeeId);
        Assert.Equal(new DateTime(2026, 9, 12), draft.ContractSignDate);
        Assert.Equal("AcceptanceDateInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateAsync(ContractId, draft.AcceptanceRecordId, new() {
                AcceptanceCode = draft.AcceptanceCode, AcceptanceDate = new DateTime(2026,9,11), Location = draft.Location,
                AcceptanceKind = draft.AcceptanceKind, RowVersion = draft.RowVersion }, EmployeeId))).Code);
        Assert.Equal(new DateTime(2026,9,12), (await service.GetAsync(ContractId, draft.AcceptanceRecordId, EmployeeId)).AcceptanceDate);
    }

    [Theory]
    [InlineData(false, 0, 13, false)]
    [InlineData(false, 0, 14, true)]
    [InlineData(false, 0, 15, true)]
    [InlineData(false, 1, 14, false)]
    [InlineData(false, 1, 15, true)]
    [InlineData(true, 0, 14, false)]
    [InlineData(true, 0, 15, true)]
    public async Task Acceptance_FinalizeChecksDatesOfEverySelectedSignedHandover(
        bool final, int lineOffset, int day, bool valid)
    {
        await using var db = CreateContext(); var storage = new TrackingStorage();
        await SeedAsync(db, storage); var service = CreateService(db, storage);
        var request = CreateRequest(); request.AcceptanceDate = new DateTime(2026, 9, day);
        if (final) { request.AcceptanceKind = AcceptanceKind.Final; request.PaymentMilestoneIds = []; }
        var draft = await service.CreateAsync(ContractId, request, EmployeeId);
        await SeedSignedHandoverScopeAsync(db, storage);
        draft = await service.ReplaceHandoverScopeAsync(ContractId, draft.AcceptanceRecordId, new() {
            RowVersion = draft.RowVersion,
            Items = final ? [new(10200, 1), new(10201, 1)] : [new(10200 + lineOffset, 1)] }, EmployeeId);
        var saves = storage.SavedKeys.Count;
        if (valid)
        {
            Assert.Null(draft.HandoverBlocker);
            var finalized = await service.FinalizeAsync(ContractId, draft.AcceptanceRecordId,
                new() { RowVersion = draft.RowVersion }, EmployeeId);
            Assert.Equal(AcceptanceRecordStatus.Finalized, finalized.Status);
        }
        else
        {
            Assert.Contains("Ngày nghiệm thu", draft.HandoverBlocker);
            Assert.Equal("AcceptanceDateInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.PreviewAsync(ContractId, draft.AcceptanceRecordId, "docx", EmployeeId))).Code);
            Assert.Equal("AcceptanceDateInvalid", (await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.FinalizeAsync(ContractId, draft.AcceptanceRecordId, new() { RowVersion = draft.RowVersion }, EmployeeId))).Code);
            Assert.Equal(saves, storage.SavedKeys.Count);
            Assert.Equal((byte)AcceptanceRecordStatus.Draft, (await db.TblContractAcceptanceRecords.SingleAsync()).Status);
            Assert.Empty(db.TblFileStorages.Where(x => x.ObjectType == "ContractAcceptanceArtifact"));
        }
    }
}
