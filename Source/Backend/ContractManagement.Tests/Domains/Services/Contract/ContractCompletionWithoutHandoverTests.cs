using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Exceptions;
using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Services.Contract;
using ContractManagement.Domains.Services.File;

namespace ContractManagement.Tests.Domains.Services.Contract;

public sealed partial class ContractCompletionServicePhase10Tests
{
    [Theory]
    [InlineData("ContractHandoverArtifact")]
    [InlineData("ContractHandoverEvidence")]
    public async Task OrphanedHandoverFiles_AreNotExposedThroughGenericFileApis(string objectType)
    {
        await using var db = CreateContext(); await SeedAsync(db, ContractStatus.Signed);
        db.TblFileStorages.Add(FileMetadata(13004, objectType, "ban-giao.pdf"));
        await db.SaveChangesAsync();
        var authorization = new FileResourceAuthorizationService(db, new ContractResourceAuthorizationService(db));
        await Assert.ThrowsAsync<RbacOperationException>(() => authorization.EnsureCanReadFileAsync(13004, ManagerId));
        await Assert.ThrowsAsync<RbacOperationException>(() => authorization.EnsureCanDeleteFileAsync(13004, ManagerId));
    }

    [Theory]
    [InlineData(ContractDirection.Sale)]
    [InlineData(ContractDirection.Purchase)]
    public async Task Completion_AllMoneyAndSignedAcceptance_CompletesWithoutHandover(ContractDirection direction)
    {
        await using var db = CreateContext();
        await SeedAsync(db, ContractStatus.Signed, includeAcceptance: true, activePayments: [ContractTotal], direction: direction);
        var service = CreateService(db, new TrackingPrivateStorage());
        var ready = await service.GetReadinessAsync(ContractId, ManagerId);
        Assert.True(ready.AcceptanceCompleted); Assert.True(ready.Ready); Assert.Empty(ready.Blockers);
        var completed = await service.CompleteAsync(ContractId, CompleteRequest(), ManagerId);
        Assert.Equal(ContractStatus.Completed, completed.ContractStatus);
        Assert.True(completed.Readiness.Signed); Assert.Empty(completed.Readiness.Blockers);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AddPaymentAsync(ContractId, PaymentRequest(1m, "AFTER-CLOSED"), AccountantId));
    }
}
