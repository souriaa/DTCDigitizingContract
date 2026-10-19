using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Policies.Contract;

namespace ContractManagement.Tests.Domains.Policies.Contract;

public sealed class ContractCompletionPolicyTests
{
    [Theory]
    [InlineData(false, false, ContractCompletionBlockerCode.HandoverIncomplete)]
    [InlineData(true, true, ContractCompletionBlockerCode.ExecutionDocumentInProgress)]
    public void Evaluate_blocks_missing_handover_or_unresolved_execution_document(bool handover, bool pending, ContractCompletionBlockerCode code)
    {
        var result = ContractCompletionPolicy.Evaluate(ContractStatus.Signed, true, true, true, 100m, 100m, handover, pending);
        Assert.False(result.CanComplete);
        Assert.Contains(result.Blockers, x => x.Code == code);
    }

    [Fact]
    public void Evaluate_returns_ready_when_all_phase10_conditions_are_met()
    {
        var result = ContractCompletionPolicy.Evaluate(
            ContractStatus.Signed, true, true, true, 100m, 100m, true, false);
        Assert.True(result.CanComplete);
        Assert.Empty(result.Blockers);
    }

    [Theory]
    [InlineData(ContractStatus.PendingSignature, false, true, true, 100, 100, ContractCompletionBlockerCode.ContractMustBeSigned)]
    [InlineData(ContractStatus.Signed, true, false, false, 100, 100, ContractCompletionBlockerCode.AcceptanceNotStarted)]
    [InlineData(ContractStatus.Signed, true, false, true, 100, 100, ContractCompletionBlockerCode.AcceptanceIncomplete)]
    [InlineData(ContractStatus.Signed, true, true, true, 100, 99, ContractCompletionBlockerCode.PaymentNotFullyPaid)]
    public void Evaluate_returns_stable_blocker(ContractStatus status, bool signed,
        bool acceptanceCompleted, bool hasSignedAcceptance, decimal total,
        decimal paid, ContractCompletionBlockerCode expected)
    {
        var result = ContractCompletionPolicy.Evaluate(status, signed,
            acceptanceCompleted, hasSignedAcceptance, total, paid, true, false);
        Assert.False(result.CanComplete);
        Assert.Contains(result.Blockers, blocker => blocker.Code == expected);
    }

    [Fact]
    public void Evaluate_reports_all_missing_conditions()
    {
        var result = ContractCompletionPolicy.Evaluate(
            ContractStatus.PendingSignature, false, false, false, 100m, 0m, false, false);
        Assert.Equal(4, result.Blockers.Count);
    }

    [Fact]
    public void Evaluate_completed_contract_is_not_ready_again()
    {
        var result = ContractCompletionPolicy.Evaluate(
            ContractStatus.Completed,
            hasActiveSignedEvidence: true,
            acceptanceCompleted: true,
            hasSignedAcceptance: true,
            totalAmount: 100m,
            paidAmount: 100m, handoverCompleted: true, executionDocumentInProgress: false);

        Assert.False(result.CanComplete);
        Assert.Contains(
            result.Blockers,
            blocker => blocker.Code ==
                ContractCompletionBlockerCode.ContractMustBeSigned);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, -1)]
    [InlineData(100, 101)]
    public void Evaluate_rejects_invalid_amounts(decimal total, decimal paid)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ContractCompletionPolicy.Evaluate(
                ContractStatus.Signed, true, true, true, total, paid, true, false));
    }
}
