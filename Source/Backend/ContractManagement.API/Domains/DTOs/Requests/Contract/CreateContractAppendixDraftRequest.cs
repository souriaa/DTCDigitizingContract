using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract;

/// <summary>Phụ lục và điều khoản được lưu nguyên tử cùng hợp đồng mới.</summary>
public sealed class CreateContractAppendixDraftRequest
{
    [Range(1, int.MaxValue)] public int? SourceTemplateAppendixId { get; set; }
    [Required, MaxLength(50)] public string AppendixCode { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string AppendixName { get; set; } = string.Empty;
    [MaxLength(500)] public string? AppendixNameEn { get; set; }
    [MaxLength(2000)] public string? AppendixDescription { get; set; }
    [Required, MinLength(1)] public List<CreateContractAppendixTermDraftRequest> Terms { get; set; } = [];
}

public sealed class CreateContractAppendixTermDraftRequest
{
    [Range(1, int.MaxValue)] public int? SourceTemplateAppendixTermId { get; set; }
    [Required, MaxLength(100)] public string TermCode { get; set; } = string.Empty;
    [Required, MaxLength(1000)] public string TermTitle { get; set; } = string.Empty;
    [MaxLength(1000)] public string? TermTitleEn { get; set; }
    public string? TermContent { get; set; }
    public string? TermContentEn { get; set; }
    [JsonRequired, Range(1, 2)] public byte BilingualContentMode { get; set; } = 1;
}
