using System.ComponentModel.DataAnnotations;

namespace ContractManagement.API.Domains.DTOs.Requests.Catalog
{
    /// <summary>
    /// Request cập nhật dịch vụ.
    /// Status có API riêng để tránh update nhầm trạng thái.
    /// </summary>
    public class UpdateServiceRequest
    {
        [Required]
        [MaxLength(2000)]
        public string ServiceName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ tiếng Anh.")]
        [MaxLength(500)]
        public string ServiceNameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập đơn vị.")]
        [MaxLength(100)]
        public string UnitName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập đơn vị tiếng Anh.")]
        [MaxLength(100)]
        public string UnitNameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn loại dịch vụ.")]
        [Range(1, 255, ErrorMessage = "Loại dịch vụ không hợp lệ.")]
        public byte? ServiceTypeId { get; set; }

        public int? ServiceParentId { get; set; }

        public double? ServicePrice { get; set; }

        public double? SetupPrice { get; set; }

        public double? MaintainPrice { get; set; }

        public int? LangId { get; set; }

        [MaxLength(50)]
        public string? ServiceImageIcon { get; set; }

        public string? ServiceShortDesc { get; set; }

        public string? ServiceContent { get; set; }

        public int? ServiceOrder { get; set; }

        public byte? ServiceRegion { get; set; }

        [MaxLength(300)]
        public string? Rewrite { get; set; }

        [MaxLength(500)]
        public string? TitleBrowser { get; set; }

        [MaxLength(2000)]
        public string? MetaKeyword { get; set; }

        [MaxLength(2000)]
        public string? MetaDescription { get; set; }

        [MaxLength(4000)]
        public string? Others { get; set; }
    }
}
