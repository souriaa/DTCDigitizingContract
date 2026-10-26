using ContractManagement.API.Common.Responses;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;
namespace ContractManagement.API.Domains.Controllers.Contract;
[ApiController]
[Route("api/contracts/{contractId:int}/handover-records")]
[SessionAuthorize]
public sealed class ContractHandoverController(IContractHandoverService service) : ControllerBase
{
    private int EmployeeId => HttpContext.Session.GetInt32("EmployeeId") ?? throw new UnauthorizedAccessException("Cần đăng nhập.");
    [HttpGet]
    public async Task<IActionResult> List(int contractId, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<HandoverRecordSummary>>.Ok(await service.ListAsync(contractId, EmployeeId, ct)));
    [HttpGet("templates")]
    public async Task<IActionResult> Templates(int contractId, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<HandoverTemplateOption>>.Ok(await service.TemplatesAsync(contractId, EmployeeId, ct)));
    [HttpGet("readiness")]
    public async Task<IActionResult> Readiness(int contractId, CancellationToken ct) => Ok(ApiResponse<HandoverReadinessResponse>.Ok(await service.ReadinessAsync(contractId, EmployeeId, ct)));
    [HttpGet("scope/candidates")]
    public async Task<IActionResult> ScopeCandidates(int contractId, CancellationToken ct) => Ok(ApiResponse<IReadOnlyList<HandoverScopeCandidateResponse>>.Ok(await service.ScopeCandidatesAsync(contractId, EmployeeId, ct)));
    [HttpPost("scope/confirm")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> ConfirmScope(int contractId, ConfirmHandoverScopeRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverReadinessResponse>.Ok(await service.ConfirmScopeAsync(contractId, request, EmployeeId, ct)));
    [HttpGet("{recordId:int}")]
    public async Task<IActionResult> Get(int contractId, int recordId, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.GetAsync(contractId, recordId, EmployeeId, ct)));
    [HttpPost]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Create(int contractId, CreateContractHandoverRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.CreateAsync(contractId, request, EmployeeId, ct)));
    [HttpPut("{recordId:int}")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Update(int contractId, int recordId, UpdateContractHandoverRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.UpdateAsync(contractId, recordId, request, EmployeeId, ct)));
    [HttpGet("{recordId:int}/preview/{format}")]
    public async Task<IActionResult> Preview(int contractId, int recordId, string format, CancellationToken ct)
    { var file = await service.PreviewAsync(contractId, recordId, format, EmployeeId, ct); return File(file.Content, file.ContentType, file.FileName); }
    [HttpPost("{recordId:int}/finalize")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Finalize(int contractId, int recordId, HandoverRowVersionRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.FinalizeAsync(contractId, recordId, request, EmployeeId, ct)));
    [HttpPost("{recordId:int}/signed-evidence")]
    [Consumes("multipart/form-data")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Sign(int contractId, int recordId, [FromForm] UploadHandoverSignedRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.SignAsync(contractId, recordId, request, EmployeeId, ct)));
    [HttpPost("{recordId:int}/cancel")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Cancel(int contractId, int recordId, CancelContractHandoverRequest request, CancellationToken ct) => Ok(ApiResponse<HandoverDetailResponse>.Ok(await service.CancelAsync(contractId, recordId, request, EmployeeId, ct)));
    [HttpGet("{recordId:int}/artifacts/{format}")]
    public async Task<IActionResult> Artifact(int contractId, int recordId, string format, CancellationToken ct)
    { var file = await service.DownloadAsync(contractId, recordId, format, false, EmployeeId, ct); return File(file.Content, file.ContentType, file.FileName); }
    [HttpGet("{recordId:int}/signed-evidence")]
    public async Task<IActionResult> Evidence(int contractId, int recordId, CancellationToken ct)
    { var file = await service.DownloadAsync(contractId, recordId, "", true, EmployeeId, ct); return File(file.Content, file.ContentType, file.FileName); }
}
