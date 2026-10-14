using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.DTOs.Responses.Catalog;

namespace ContractManagement.API.Domains.Interfaces.Catalog
{
    /// <summary>
    /// Service quản lý sản phẩm.
    /// </summary>
    public interface IProductService
    {
        Task<PagedResult<ProductResponse>> GetListAsync(ProductFilterRequest filter);

        Task<ProductResponse> GetByIdAsync(int id, bool includeDeleted = false);

        Task<ProductResponse> CreateAsync(CreateProductRequest request);

        Task UpdateAsync(int id, UpdateProductRequest request);

        Task SetStatusAsync(int id, byte status);

        Task DeleteAsync(int id);
        Task RestoreAsync(int id);
    }
}
