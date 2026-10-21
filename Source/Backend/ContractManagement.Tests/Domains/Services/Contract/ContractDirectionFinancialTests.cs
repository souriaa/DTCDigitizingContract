using ContractManagement.API.Common.Enums;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractCompletionServicePhase10Tests
{
    [Theory]
    [InlineData(ContractDirection.Sale, true)]
    [InlineData(ContractDirection.Purchase, false)]
    public async Task Readiness_ResolvesMoneyReceiverFromFrozenParties_WithoutNegatingAmounts(
        ContractDirection direction, bool receives)
    {
        await using var db = CreateContext();
        await SeedAsync(db, ContractStatus.Signed, includeAcceptance: true, activePayments: [400m], direction: direction);
        var parties = await db.TblContractVersionPartySnapshots.AsNoTracking().ToListAsync();
        var receiver = parties.Single(x => x.PartyRole == (receives ? 1 : 2));
        var payer = parties.Single(x => x.PartyRole == (receives ? 2 : 1));
        var result = await CreateService(db, new TrackingPrivateStorage()).GetReadinessAsync(ContractId, ManagerId);
        Assert.Equal(direction, result.ContractDirection);
        Assert.Equal(receives, result.TenantReceivesPayment);
        Assert.Equal(receiver.LegalName, result.PayeeName);
        Assert.Equal(receiver.BankName, result.PayeeBankName);
        Assert.Equal(payer.LegalName, result.PayerName);
        Assert.Equal(ContractTotal, result.TotalAmount);
        Assert.Equal(400m, result.PaidAmount);
        Assert.Equal(ContractTotal - 400m, result.RemainingAmount);
    }
}
