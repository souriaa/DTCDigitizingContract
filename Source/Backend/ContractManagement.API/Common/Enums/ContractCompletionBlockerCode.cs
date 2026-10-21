namespace ContractManagement.API.Common.Enums
{
    /// <summary>
    /// Mã nguyên nhân khiến hợp đồng chưa đủ điều kiện chuyển sang Completed.
    ///
    /// Enum này chỉ phục vụ kết quả kiểm tra nghiệp vụ,
    /// không được lưu trực tiếp vào database.
    /// </summary>
    public enum ContractCompletionBlockerCode
    {
        /// <summary>
        /// Hợp đồng chưa hoàn tất quy trình ký.
        /// </summary>
        ContractMustBeSigned = 1,

        /// <summary>
        /// Chưa có biên bản nghiệm thu đã ký cho version hiện hành.
        /// </summary>
        AcceptanceNotStarted = 2,

        /// <summary>
        /// Tổng các khoản thanh toán còn hiệu lực chưa bằng giá trị hợp đồng.
        /// </summary>
        PaymentNotFullyPaid = 3,

        /// <summary>
        /// Đã nghiệm thu một phần nhưng còn đợt chưa được nghiệm thu.
        /// </summary>
        AcceptanceIncomplete = 4,
        HandoverIncomplete = 5,
        ExecutionDocumentInProgress = 6
    }
}
