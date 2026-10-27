using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Discussions;
using ContractManagement.API.Domains.Notifications;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace ContractManagement.API.Domains.Realtime;

[ApiController]
public sealed class NodeRealtimeController(NodeRealtimeBroker broker, RealtimeServiceAuthentication authentication,
    IOptions<NodeRealtimeOptions> options, IConfiguration configuration) : ControllerBase
{
    [HttpPost("api/realtime/notifications/ticket"), SessionAuthorize]
    [HttpGet("api/realtime/notifications/probe")]
    public async Task<IActionResult> Notifications()
    {
        if (await CheckAccessAsync() is { } denied) return denied;
        var tenant = HttpContext.RequestServices.GetRequiredService<ICurrentTenant>();
        var employee = await EmployeeSessionValidator.ValidateAsync(HttpContext, tenant,
            HttpContext.RequestServices.GetRequiredService<DbDtctechContext>(), []);
        return Respond(new("", NotificationHub.GroupName(tenant.GetRequiredTenant().TenantId, employee.EmployeeId),
            $"employee:{tenant.GetRequiredTenant().TenantId}:{employee.EmployeeId}", DateTime.UtcNow.AddMinutes(15)));
    }

    [HttpPost("api/contracts/{contractId:int}/versions/{versionId:int}/discussion-hub/ticket"), SessionAuthorize]
    [HttpGet("api/contracts/{contractId:int}/versions/{versionId:int}/discussion-hub/probe")]
    public async Task<IActionResult> EmployeeDiscussion()
    {
        if (await CheckAccessAsync() is { } denied) return denied;
        var scope = await HttpContext.RequestServices.GetRequiredService<ContractDiscussionAccessService>().ForEmployeeAsync(HttpContext);
        var id = HttpContext.Session.GetInt32("EmployeeId");
        return Respond(new("", ContractDiscussionHubBase.GroupName(scope.TenantId, scope.ContractId, scope.VersionId),
            $"employee:{scope.TenantId}:{id}", scope.ExpiresAt));
    }

    [HttpPost("public/contracts/{tenantCode}/discussion-hub/ticket"), PublicCustomerAccess]
    [HttpGet("public/contracts/{tenantCode}/discussion-hub/probe")]
    public async Task<IActionResult> CustomerDiscussion()
    {
        if (await CheckAccessAsync() is { } denied) return denied;
        var scope = await HttpContext.RequestServices.GetRequiredService<ContractDiscussionAccessService>().ForCustomerAsync(HttpContext);
        var cookie = Request.Cookies["ContractManagement.CustomerAccess"] ?? "";
        var actor = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cookie)));
        return Respond(new("", ContractDiscussionHubBase.GroupName(scope.TenantId, scope.ContractId, scope.VersionId),
            $"customer:{scope.TenantId}:{actor}", scope.ExpiresAt));
    }

    private async Task<IActionResult?> CheckAccessAsync()
    {
        Response.Headers.CacheControl = "no-store";
        if (!options.Value.Enabled)
            return StatusCode(403, new { code = "NodeRealtimeDisabled",
                message = "Realtime Node.js chưa được bật trên API. Kiểm tra cấu hình Realtime:NodeWebSocket:Enabled và khởi động lại API." });
        if (HttpMethods.IsGet(Request.Method))
            return await authentication.ValidateAsync(Request) ? null
                : StatusCode(403, new { code = "RealtimeServiceAuthenticationInvalid", message = "Xác thực dịch vụ realtime không hợp lệ." });
        var origin = Request.Headers.Origin.ToString();
        // The API reverse proxy shares its browser origin even when Next runs on port 3000.
        // A missing/null Origin never authorizes a cookie request.
        var sameOrigin = $"{Request.Scheme}://{Request.Host}";
        var allowed = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "http://localhost:3001", "http://localhost:8081"];
        if (string.Equals(origin, sameOrigin, StringComparison.Ordinal)
            || allowed.Contains(origin, StringComparer.Ordinal)) return null;
        return StatusCode(403, new { code = "RealtimeOriginNotAllowed",
            message = "Địa chỉ trang đang mở chưa được phép kết nối realtime. Kiểm tra origin trong cấu hình API và BackendNode." });
    }

    private IActionResult Respond(NodeSubscription scope)
    {
        if (HttpMethods.IsGet(Request.Method)) return Ok(scope);
        // Capture only the cookie needed by the matching authorization path; never return it to Node.
        var name = Request.Path.StartsWithSegments("/public") ? "ContractManagement.CustomerAccess" : "ContractManagement.Session";
        var cookie = Request.Cookies[name];
        if (string.IsNullOrEmpty(cookie)) return Unauthorized();
        var probe = Request.Path.Value![..^"ticket".Length] + "probe";
        var ticket = broker.Issue($"{name}={Uri.EscapeDataString(cookie)}", probe, Request.Headers.Origin.ToString(), scope);
        return Ok(new { ticket, expiresInSeconds = 30 });
    }
}

[ApiController, AllowWithoutTenant, RequestSizeLimit(16384)]
[Route("internal/realtime")]
public sealed class NodeRealtimeInternalController(NodeRealtimeBroker broker, RealtimeServiceAuthentication authentication) : ControllerBase
{
    public sealed record ConsumeRequest(string Ticket, string Origin);
    public sealed record LeaseRequest(string LeaseId);
    [HttpPost("consume")]
    public async Task<IActionResult> Consume([FromBody] ConsumeRequest request, CancellationToken ct)
    {
        if (!await authentication.ValidateAsync(Request)) return Unauthorized();
        var scope = await broker.ConsumeAsync(request.Ticket, request.Origin, ct);
        return scope is null ? Unauthorized() : Ok(scope);
    }
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateLease([FromBody] LeaseRequest request, CancellationToken ct)
    {
        if (!await authentication.ValidateAsync(Request)) return Unauthorized();
        var scope = await broker.ValidateAsync(request.LeaseId, ct);
        return scope is null ? Unauthorized() : Ok(scope);
    }
    [HttpPost("release")]
    public async Task<IActionResult> Release([FromBody] LeaseRequest request)
    {
        if (!await authentication.ValidateAsync(Request)) return Unauthorized();
        broker.Release(request.LeaseId);
        return Ok(new { released = true });
    }
}
