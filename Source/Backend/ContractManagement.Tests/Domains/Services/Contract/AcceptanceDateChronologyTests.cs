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

}
