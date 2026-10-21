namespace ContractManagement.API.Common.Enums
{
    /// <summary>
    /// Xác định bên tham gia ký hợp đồng.
    /// </summary>
    public enum SignerParty : byte
    {
        /// <summary>
        /// Bên mua theo vai trò tài liệu, không xác định Bên A/B hoặc quyền nội bộ/public.
        /// </summary>
        Customer = 0,

        /// <summary>
        /// Bên cung cấp theo vai trò tài liệu; khi mua là đối tác ngoài hệ thống.
        /// </summary>
        Provider = 1
    }
}
