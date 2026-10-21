namespace ContractManagement.API.Domains.DTOs.Responses.Catalog
{
    /// <summary>
    /// Response thông tin dịch vụ.
    /// </summary>
    public class ServiceResponse
    {
        public int ServiceId { get; set; }

        public string? ServiceName { get; set; }

        public string? ServiceNameEn { get; set; }
        public string? UnitName { get; set; }
        public string? UnitNameEn { get; set; }

        public byte? ServiceTypeId { get; set; }

        public string? ServiceTypeName { get; set; }

        public int? ServiceParentId { get; set; }

        public double? ServicePrice { get; set; }

        public double? SetupPrice { get; set; }

        public double? MaintainPrice { get; set; }

        public byte? Status { get; set; }

        public int? LangId { get; set; }

        public string? ServiceImageIcon { get; set; }

        public string? ServiceShortDesc { get; set; }

        public string? ServiceContent { get; set; }

        public int? ServiceOrder { get; set; }

        public byte? ServiceRegion { get; set; }

        public string? Rewrite { get; set; }

        public string? TitleBrowser { get; set; }

        public string? MetaKeyword { get; set; }

        public string? MetaDescription { get; set; }

        public string? Others { get; set; }

        public int? UserCreated { get; set; }

        public int? UserModified { get; set; }

        public DateTime? DateCreated { get; set; }

        public DateTime? DateModified { get; set; }
    }
}
