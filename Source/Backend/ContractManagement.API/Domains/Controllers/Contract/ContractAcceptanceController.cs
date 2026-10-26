using ContractManagement.API.Common.Responses;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.DTOs.Requests.Contract;
using ContractManagement.API.Domains.DTOs.Responses.Contract;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Filter;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Controllers.Contract;

[ApiController]
[Route("api/contracts/{contractId:int}/acceptance-records")]
[SessionAuthorize]
public sealed class ContractAcceptanceController : ControllerBase
{
    private readonly IContractAcceptanceService _service;
    public ContractAcceptanceController(IContractAcceptanceService service) =>
        _service = service;

    [HttpGet("{acceptanceRecordId:int}/handover-scope")]
    public async Task<IActionResult> HandoverScope(int contractId, int acceptanceRecordId, CancellationToken ct) =>
        Ok(ApiResponse<AcceptanceHandoverOptionsResponse>.Ok(await _service.HandoverOptionsAsync(contractId, acceptanceRecordId, EmployeeId(), ct)));

    [HttpPut("{acceptanceRecordId:int}/handover-scope")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> ReplaceHandoverScope(int contractId, int acceptanceRecordId,
        ReplaceAcceptanceHandoverScopeRequest request, CancellationToken ct) =>
        Ok(ApiResponse<ContractAcceptanceRecordResponse>.Ok(await _service.ReplaceHandoverScopeAsync(contractId, acceptanceRecordId, request, EmployeeId(), ct)));

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(int contractId,
        CancellationToken ct) => Ok(
        ApiResponse<IReadOnlyList<ContractAcceptanceTemplateOptionResponse>>.Ok(
            await _service.GetTemplatesAsync(contractId, EmployeeId(), ct)));

    [HttpGet]
    public async Task<IActionResult> List(int contractId,
        CancellationToken ct) => Ok(
        ApiResponse<IReadOnlyList<ContractAcceptanceRecordSummaryResponse>>.Ok(
            await _service.ListAsync(contractId, EmployeeId(), ct)));

    [HttpGet("{acceptanceRecordId:int}")]
    public async Task<IActionResult> Get(int contractId,
        int acceptanceRecordId, CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.GetAsync(contractId, acceptanceRecordId,
                EmployeeId(), ct)));

    [HttpPost]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Create(int contractId,
        [FromBody] CreateContractAcceptanceRecordRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.CreateAsync(
                contractId, request, EmployeeId(), ct),
            "Đã tạo bản nháp biên bản nghiệm thu."));

    [HttpPut("{acceptanceRecordId:int}")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Update(int contractId,
        int acceptanceRecordId,
        [FromBody] UpdateContractAcceptanceRecordRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.UpdateAsync(contractId, acceptanceRecordId,
                request, EmployeeId(), ct)));

    [HttpPut("{acceptanceRecordId:int}/references")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> References(int contractId,
        int acceptanceRecordId,
        [FromBody] ReplaceContractAcceptanceReferencesRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.ReplaceReferencesAsync(contractId,
                acceptanceRecordId, request, EmployeeId(), ct)));

    [HttpPut("{acceptanceRecordId:int}/sections")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Sections(int contractId,
        int acceptanceRecordId,
        [FromBody] ReplaceContractAcceptanceSectionsRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.ReplaceSectionsAsync(contractId,
                acceptanceRecordId, request, EmployeeId(), ct)));

    [HttpPut("{acceptanceRecordId:int}/milestones")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Milestones(int contractId,
        int acceptanceRecordId,
        [FromBody] ReplaceContractAcceptanceMilestonesRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.ReplaceMilestonesAsync(contractId,
                acceptanceRecordId, request, EmployeeId(), ct)));

    [HttpGet("{acceptanceRecordId:int}/preview/{format}")]
    public async Task<IActionResult> Preview(int contractId,
        int acceptanceRecordId, string format, CancellationToken ct)
    {
        var document = await _service.PreviewAsync(contractId,
            acceptanceRecordId, format, EmployeeId(), ct);
        return File(document.Content, document.ContentType, document.FileName);
    }

    [HttpPost("{acceptanceRecordId:int}/finalize")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Finalize(int contractId,
        int acceptanceRecordId,
        [FromBody] FinalizeContractAcceptanceRecordRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.FinalizeAsync(contractId, acceptanceRecordId,
                request, EmployeeId(), ct),
            "Đã chốt biên bản nghiệm thu."));

    [HttpPost("{acceptanceRecordId:int}/signed-evidence")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SignedEvidence(int contractId,
        int acceptanceRecordId,
        [FromForm] UploadContractAcceptanceSignedEvidenceRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceEvidenceResponse>.Ok(
            await _service.UploadSignedEvidenceAsync(contractId,
                acceptanceRecordId, request, EmployeeId(), ct),
            "Đã lưu bản ký biên bản nghiệm thu."));

    [HttpPost("{acceptanceRecordId:int}/cancel")]
    [SessionAuthorize(RbacPermissions.ContractAcceptanceManage, RbacPermissions.ContractAcceptanceManageOwn, RequireAnyPermission = true)]
    public async Task<IActionResult> Cancel(int contractId,
        int acceptanceRecordId,
        [FromBody] CancelContractAcceptanceRecordRequest request,
        CancellationToken ct) => Ok(
        ApiResponse<ContractAcceptanceRecordResponse>.Ok(
            await _service.CancelAsync(contractId, acceptanceRecordId,
                request, EmployeeId(), ct),
            "Đã hủy biên bản nghiệm thu."));

    [HttpGet("{acceptanceRecordId:int}/artifacts/{format}")]
    public async Task<IActionResult> Artifact(int contractId,
        int acceptanceRecordId, string format, CancellationToken ct)
    {
        var document = await _service.DownloadArtifactAsync(contractId,
            acceptanceRecordId, format, EmployeeId(), ct);
        return File(document.Content, document.ContentType, document.FileName);
    }

    private int EmployeeId() => HttpContext.Session.GetInt32("EmployeeId")
        ?? throw new UnauthorizedAccessException(
            "Employee login is required.");
}
