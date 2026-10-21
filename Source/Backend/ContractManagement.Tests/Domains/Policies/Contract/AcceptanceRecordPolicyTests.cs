using ContractManagement.API.Common.Enums;
using ContractManagement.Domains.Policies.Contract;

namespace ContractManagement.Tests.Domains.Policies.Contract;

public sealed class AcceptanceRecordPolicyTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("0001-01-01", false)]
    [InlineData("2026-09-17", false)]
    [InlineData("2026-09-18", true)]
    [InlineData("2026-09-20", true)]
    [InlineData("2026-09-21", false)]
    public void SignDate_RequiresDateBetweenAcceptanceAndVietnamBusinessToday(string? value, bool valid)
    {
        var utc = new DateTimeOffset(2026, 9, 19, 17, 5, 0, TimeSpan.Zero);
        var date = value is null ? (DateTime?)null : DateTime.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(valid, AcceptanceRecordPolicy.IsSignDateValid(date, new DateTime(2026, 9, 18), utc));
    }

    [Fact]
    public void SignDate_DoesNotRollToNextBusinessDayBeforeMidnightVietnam()
    {
        Assert.False(AcceptanceRecordPolicy.IsSignDateValid(new DateTime(2026, 9, 20),
            new DateTime(2026, 9, 18), new DateTimeOffset(2026, 9, 19, 16, 59, 59, TimeSpan.Zero)));
    }

    [Theory]
    [InlineData(AcceptanceRecordStatus.Draft, AcceptanceRecordStatus.Finalized)]
    [InlineData(AcceptanceRecordStatus.Draft, AcceptanceRecordStatus.Cancelled)]
    [InlineData(AcceptanceRecordStatus.Finalized, AcceptanceRecordStatus.Signed)]
    [InlineData(AcceptanceRecordStatus.Finalized, AcceptanceRecordStatus.Cancelled)]
    public void CanTransition_AllowsDefinedEdges(
        AcceptanceRecordStatus source,
        AcceptanceRecordStatus target)
    {
        Assert.True(AcceptanceRecordPolicy.CanTransition(source, target));
    }

    [Theory]
    [InlineData(AcceptanceRecordStatus.Draft, AcceptanceRecordStatus.Signed)]
    [InlineData(AcceptanceRecordStatus.Signed, AcceptanceRecordStatus.Cancelled)]
    [InlineData(AcceptanceRecordStatus.Cancelled, AcceptanceRecordStatus.Draft)]
    public void CanTransition_RejectsUndefinedEdges(
        AcceptanceRecordStatus source,
        AcceptanceRecordStatus target)
    {
        Assert.False(AcceptanceRecordPolicy.CanTransition(source, target));
    }

    [Fact]
    public void EnsureCanCreate_RequiresSignedContractAndBusinessSignDate()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AcceptanceRecordPolicy.EnsureCanCreate(true, null));
        AcceptanceRecordPolicy.EnsureCanCreate(true, new DateTime(2026, 9, 16));
    }
}
