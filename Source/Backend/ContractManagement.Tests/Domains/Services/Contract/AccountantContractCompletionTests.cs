using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Services.Contract;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractCompletionServicePhase10Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accountant_CanCompleteReadyContract_ButCannotSign(bool ownsContract)
    {
        await using var db = CreateContext();
        await SeedAsync(db, ContractStatus.Signed, includeAcceptance: true,
            activePayments: [ContractTotal]);
        if (ownsContract)
        {
            (await db.TblContracts.SingleAsync()).EmployeeId = AccountantId;
            await db.SaveChangesAsync();
        }
        var authorization = new ContractResourceAuthorizationService(db);
        await Assert.ThrowsAsync<RbacOperationException>(() =>
            authorization.EnsureCanManageSigningAsync(ContractId, AccountantId));
        var result = await CreateService(db, new TrackingPrivateStorage())
            .CompleteAsync(ContractId, CompleteRequest(), AccountantId);
        Assert.Equal(ContractStatus.Completed, result.ContractStatus);
        Assert.Equal(AccountantId, (await db.TblContracts.SingleAsync()).UpdatedEmployeeId);
    }

    [Fact]
    public async Task Accountant_CannotCompleteContractWithMissingReadinessConditions()
    {
        await using var db = CreateContext();
        await SeedAsync(db, ContractStatus.Signed);
        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(db, new TrackingPrivateStorage())
                .CompleteAsync(ContractId, CompleteRequest(), AccountantId));
        Assert.Equal("ContractNotReadyForCompletion", error.Code);
        Assert.Equal((byte)ContractStatus.Signed, (await db.TblContracts.SingleAsync()).Status);
    }
}
