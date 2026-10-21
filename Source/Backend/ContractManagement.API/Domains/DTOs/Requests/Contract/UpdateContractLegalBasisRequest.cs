using System.ComponentModel.DataAnnotations;

namespace ContractManagement.API.Domains.DTOs.Requests.Contract;

/// <summary>Chỉ sửa nội dung căn cứ của phiên bản hiện tại; không đổi mã, nguồn hoặc thứ tự.</summary>
public sealed class UpdateContractLegalBasisRequest
{
    [Range(1, int.MaxValue)]
    public int LegalBasisId { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;

    [Required]
    public string ContentVi { get; set; } = string.Empty;

    public string? ContentEn { get; set; }
}
