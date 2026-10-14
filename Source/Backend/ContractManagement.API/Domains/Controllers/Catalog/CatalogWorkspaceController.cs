using ContractManagement.API.Common.Responses;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Catalog;
using ContractManagement.API.Domains.Interfaces.Catalog;
using ContractManagement.Domains.Services.Catalog;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;
namespace ContractManagement.API.Domains.Controllers.Catalog;

[ApiController]
[Route("api/catalog/workspace")]
[SessionAuthorize(RbacPermissions.CatalogRead)]
public sealed class CatalogWorkspaceController(CatalogWorkspaceService service, IProductService products, IServiceService services) : ControllerBase
{
    [HttpGet("categories/tree")]
    public async Task<IActionResult> Categories([FromQuery] CatalogWorkspaceFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await service.GetCategoriesAsync(filter, ct)));
    [HttpGet("service-types/groups")]
    public async Task<IActionResult> ServiceTypes([FromQuery] CatalogWorkspaceFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await service.GetServiceTypesAsync(filter, ct)));
    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] CatalogWorkspaceFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await service.GetProductsAsync(filter, ct)));
    [HttpGet("services")]
    public async Task<IActionResult> Services([FromQuery] CatalogWorkspaceFilter filter, CancellationToken ct) =>
        Ok(ApiResponse<object>.Ok(await service.GetServicesAsync(filter, ct)));
    [HttpGet("products/{id:int}")]
    public async Task<IActionResult> Product(int id) => Ok(ApiResponse<object>.Ok(await products.GetByIdAsync(id, includeDeleted: true)));
    [HttpGet("services/{id:int}")]
    public async Task<IActionResult> Service(int id) => Ok(ApiResponse<object>.Ok(await services.GetByIdAsync(id, includeDeleted: true)));
}
