using System.ComponentModel.DataAnnotations;

using ContractManagement.API.Common.Enums;
using System.Text.Json.Serialization;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract
{
    /// <summary>
    /// Điều khoản được gửi khi cập nhật Draft.
    /// </summary>
    public class UpdateContractTermRequest
    {
        [EnumDataType(typeof(ContractTermKind))]
        [JsonRequired]
        public ContractTermKind TermKind { get; set; } = ContractTermKind.General;

        [Required, JsonRequired]
        public List<ContractPaymentMilestoneDraftRequest> PaymentMilestones { get; set; } = [];

        [Range(1, int.MaxValue)]
        public int? TermId { get; set; }

        /// <summary>
        /// Bắt buộc khi TermId có giá trị.
        /// </summary>
        public string? RowVersion { get; set; }

        [Required]
        [MaxLength(100)]
        public string TermCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string TermTitle { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? TermTitleEn { get; set; }

        public string? TermContent { get; set; }

        public string? TermContentEn { get; set; }
        [System.Text.Json.Serialization.JsonRequired]
        [System.ComponentModel.DataAnnotations.Range(1, 2)]
        public byte BilingualContentMode { get; set; } = 1;

        public bool IsNegotiable { get; set; }

        [Range(0, int.MaxValue)]
        public int DisplayOrder { get; set; }
    }
}
