using ContractManagement.API.Common.Enums;
namespace ContractManagement.Domains.Policies.Contract;
public sealed record HandoverCoverage(decimal Required, decimal Delivered, decimal Reserved, decimal Accepted, decimal AcceptanceReserved)
{
    public decimal Remaining => Required - Delivered - Reserved;
    public decimal AvailableForAcceptance => Delivered - Accepted - AcceptanceReserved;
}
public static class HandoverCoveragePolicy
{
    public static void Quantity(decimal quantity, HandoverScopeMode mode)
    {
        if (!Enum.IsDefined(mode) || quantity <= 0 || quantity >= 100000000000000m
            || decimal.Round(quantity, 4) != quantity || mode == HandoverScopeMode.WholePackage && quantity != 1m)
            throw new ArgumentException("Khối lượng phải dương, tối đa 4 số lẻ; nguyên gói chỉ được giao đủ 1 gói.");
    }
    public static HandoverCoverage Evaluate(decimal required,
        IEnumerable<(HandoverRecordStatus Status, decimal Quantity)> handovers,
        IEnumerable<(AcceptanceRecordStatus Status, decimal Quantity)> acceptances)
    {
        var delivered = handovers.Where(x => x.Status == HandoverRecordStatus.Signed).Sum(x => x.Quantity);
        var reserved = handovers.Where(x => x.Status == HandoverRecordStatus.Finalized).Sum(x => x.Quantity);
        var accepted = acceptances.Where(x => x.Status == AcceptanceRecordStatus.Signed).Sum(x => x.Quantity);
        var acceptanceReserved = acceptances.Where(x => x.Status == AcceptanceRecordStatus.Finalized).Sum(x => x.Quantity);
        if (required <= 0 || delivered < 0 || reserved < 0 || accepted < 0 || acceptanceReserved < 0
            || delivered + reserved > required || accepted + acceptanceReserved > delivered)
            throw new InvalidOperationException("Phạm vi bàn giao/nghiệm thu không nhất quán; không được vượt nghĩa vụ đã ký.");
        return new(required, delivered, reserved, accepted, acceptanceReserved);
    }
}
