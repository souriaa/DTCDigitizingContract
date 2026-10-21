using ContractManagement.API.Common.Responses;
using ContractManagement.API.Domains.DTOs.Requests.ContractTemplate;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.Domains.Controllers.ContractTemplate;

public sealed partial class ContractTemplateController
{
    /// <summary>Delete only unused templates that have never been published.</summary>
    [HttpDelete("{templateId:int}")]
    public async Task<IActionResult> Delete(int templateId,
        [FromBody] DeleteContractTemplateRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetEmployeeId(out var employeeId, out var unauthorized)) return unauthorized!;
        try
        {
            await _service.DeleteAsync(templateId, request, employeeId, cancellationToken);
            return Ok(ApiResponse<object>.Ok(new { templateId }, "Đã xóa mẫu tài liệu."));
        }
        catch (UnauthorizedAccessException exception) { return Forbidden(exception); }
    }
}
