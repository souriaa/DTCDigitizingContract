using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.DTOs.Responses.Catalog;

namespace ContractManagement.API.Domains.Interfaces.Catalog
{
    /// <summary>
    /// Service quản lý dịch vụ.
    /// </summary>
    public interface IServiceService
    {
        Task<List<ServiceOptionResponse>> GetOptionsAsync(CancellationToken cancellationToken = default);

        Task<PagedResult<ServiceResponse>> GetListAsync(ServiceFilterRequest filter);

        Task<ServiceResponse> GetByIdAsync(int id, bool includeDeleted = false);

        Task<ServiceResponse> CreateAsync(CreateServiceRequest request, int createdBy);

        Task UpdateAsync(int id, UpdateServiceRequest request, int updatedBy);

        Task SetStatusAsync(int id, byte status, int updatedBy);

        Task DeleteAsync(int id, int updatedBy);
        Task RestoreAsync(int id, int updatedBy);
    }
}
