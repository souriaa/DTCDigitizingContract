using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.Contract;
namespace ContractManagement.Tests.Domains.Services.Contract;
public sealed class HandoverCoveragePolicyTests
{
    [Fact]
    public void Coverage_CountsOnlySigned_ReservesFinalized_AndReleasesCancelled()
    {
        var result = HandoverCoveragePolicy.Evaluate(10,
            [(HandoverRecordStatus.Draft, 99m), (HandoverRecordStatus.Finalized, 3m), (HandoverRecordStatus.Signed, 4m), (HandoverRecordStatus.Cancelled, 9m)],
            [(AcceptanceRecordStatus.Signed, 1m), (AcceptanceRecordStatus.Finalized, 2m), (AcceptanceRecordStatus.Cancelled, 9m)]);
        Assert.Equal(4m, result.Delivered); Assert.Equal(3m, result.Reserved); Assert.Equal(3m, result.Remaining);
        Assert.Equal(1m, result.AvailableForAcceptance);
    }
    [Fact]
    public void CannotDeliverOrAcceptOverAvailableScope()
    {
        Assert.Throws<InvalidOperationException>(() => HandoverCoveragePolicy.Evaluate(1, [(HandoverRecordStatus.Signed, 2m)], []));
        Assert.Throws<InvalidOperationException>(() => HandoverCoveragePolicy.Evaluate(3, [(HandoverRecordStatus.Signed, 1m)], [(AcceptanceRecordStatus.Signed, 2m)]));
    }
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(0.00001)] [InlineData(100000000000000)]
    public void QuantityRejectsNonPositivePrecisionOrOverflow(double quantity) =>
        Assert.Throws<ArgumentException>(() => HandoverCoveragePolicy.Quantity((decimal)quantity, HandoverScopeMode.Quantity));
    [Fact]
    public void WholePackageIsExactlyOne_NotAPaymentPercentage()
    {
        HandoverCoveragePolicy.Quantity(1m, HandoverScopeMode.WholePackage);
        Assert.Throws<ArgumentException>(() => HandoverCoveragePolicy.Quantity(0.5m, HandoverScopeMode.WholePackage));
    }
}
