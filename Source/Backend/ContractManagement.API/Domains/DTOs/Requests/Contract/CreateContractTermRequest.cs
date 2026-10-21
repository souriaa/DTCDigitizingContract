using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;

using System.Text.Json.Serialization;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract;

/// <summary>
/// Snapshot điều khoản được tùy chỉnh ngay trong wizard tạo hợp đồng.
/// SourceTemplateTermId giữ provenance; null nghĩa là điều khoản thêm mới.
/// </summary>
public sealed class CreateContractTermRequest
{
    [EnumDataType(typeof(ContractTermKind))]
    [JsonRequired]
    public ContractTermKind TermKind { get; set; } = ContractTermKind.General;

    [Required, JsonRequired]
    public List<ContractPaymentMilestoneDraftRequest> PaymentMilestones { get; set; } = [];

    [Range(1, int.MaxValue)]
    public int? SourceTemplateTermId { get; set; }

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

    [Range(1, int.MaxValue)]
    public int DisplayOrder { get; set; }
}
