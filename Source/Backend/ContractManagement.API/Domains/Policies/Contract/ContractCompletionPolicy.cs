using ContractManagement.API.Common.Enums;
using ContractManagement.API.Domains.Models.Contract;

namespace ContractManagement.API.Domains.Policies.Contract
{
    /// <summary>
    /// Đánh giá hợp đồng có đủ điều kiện chuyển từ Signed sang Completed hay không.
    ///
    /// Policy này không tự cập nhật database.
    /// Nó chỉ kiểm tra và trả về toàn bộ các điều kiện còn thiếu.
    /// </summary>
    public static class ContractCompletionPolicy
    {
        /// <summary>
        /// Kiểm tra toàn bộ điều kiện hoàn thành hợp đồng.
        /// </summary>
        /// <param name="contractStatus">
        /// Trạng thái lifecycle hiện tại của hợp đồng.
        /// </param>
        /// <param name="hasActiveSignedEvidence">Có bản scan đã ký đang hiệu lực.</param>
        /// <param name="acceptanceCompleted">Đã nghiệm thu đủ phạm vi.</param>
        /// <param name="hasSignedAcceptance">Có ít nhất một biên bản đã ký hợp lệ.</param>
        /// <param name="totalAmount">Giá trị contract version đã ký.</param>
        /// <param name="paidAmount">Tổng thanh toán chưa bị void.</param>
        /// <param name="handoverCompleted">Đã giao đủ baseline bằng chứng từ ký hợp lệ.</param>
        /// <param name="executionDocumentInProgress">Còn bản nháp hoặc chờ ký cần giải quyết.</param>
        public static ContractCompletionEvaluation Evaluate(
            ContractStatus contractStatus,
            bool hasActiveSignedEvidence,
            bool acceptanceCompleted,
            bool hasSignedAcceptance,
            decimal totalAmount,
            decimal paidAmount,
            bool handoverCompleted,
            bool executionDocumentInProgress)
        {
            EnsureDefined(contractStatus, nameof(contractStatus));
            if (totalAmount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalAmount));
            }
            if (paidAmount < 0 || paidAmount > totalAmount)
            {
                throw new ArgumentOutOfRangeException(nameof(paidAmount));
            }

            var blockers = new List<ContractCompletionBlocker>();

            /*
             * Điều kiện 1:
             * Contract lifecycle phải đang ở Signed.
             *
             * Chỉ Signed mới được phép đi tiếp sang Completed.
             */
            if (contractStatus != ContractStatus.Signed
                || !hasActiveSignedEvidence)
            {
                blockers.Add(new ContractCompletionBlocker(
                    ContractCompletionBlockerCode.ContractMustBeSigned));
            }
            if (!handoverCompleted)
                blockers.Add(new ContractCompletionBlocker(ContractCompletionBlockerCode.HandoverIncomplete));
            if (executionDocumentInProgress)
                blockers.Add(new ContractCompletionBlocker(ContractCompletionBlockerCode.ExecutionDocumentInProgress));
            if (!acceptanceCompleted)
            {
                blockers.Add(new ContractCompletionBlocker(
                    hasSignedAcceptance
                        ? ContractCompletionBlockerCode.AcceptanceIncomplete
                        : ContractCompletionBlockerCode.AcceptanceNotStarted));
            }
            if (paidAmount != totalAmount)
            {
                blockers.Add(new ContractCompletionBlocker(
                    ContractCompletionBlockerCode.PaymentNotFullyPaid));
            }

            return new ContractCompletionEvaluation(blockers);
        }

        /// <summary>
        /// Kiểm tra một giá trị có thực sự được định nghĩa trong enum hay không.
        /// </summary>
        private static void EnsureDefined<TEnum>(
            TEnum value,
            string parameterName)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(typeof(TEnum), value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    value,
                    $"Unsupported {typeof(TEnum).Name} value.");
            }
        }
    }
}
