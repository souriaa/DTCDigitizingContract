using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Enums;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract;

/// <summary>Đợt thanh toán thuộc điều khoản trong phiên bản hợp đồng đang biên soạn.</summary>
public sealed class ContractPaymentMilestoneDraftRequest
{
    [Range(1, int.MaxValue)] public int? PaymentMilestoneId { get; set; }
    public string? RowVersion { get; set; }
    [Range(1, int.MaxValue)] public int? SourceTemplatePaymentMilestoneId { get; set; }
    [Required, MaxLength(100)] public string MilestoneCode { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string TitleVi { get; set; } = string.Empty;
    [MaxLength(500)] public string? TitleEn { get; set; }
    [Range(typeof(decimal), "0.0001", "100")] public decimal PaymentPercent { get; set; }
    [EnumDataType(typeof(PaymentDueAnchor))] public PaymentDueAnchor DueAnchor { get; set; }
    [Range(0, int.MaxValue)] public int DueOffsetDays { get; set; }
    [EnumDataType(typeof(PaymentDayCountMode))] public PaymentDayCountMode DayCountMode { get; set; }
    [MaxLength(2000)] public string? ConditionVi { get; set; }
    [MaxLength(2000)] public string? ConditionEn { get; set; }
    public DateTime? AnchorDate { get; set; }
}
