using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using ContractManagement.API.Common.Security;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Recovery;

public sealed class TenantPasswordRecoveryRequest
{
    [Required(ErrorMessage = "Hãy nhập mã tổ chức."), StringLength(50), RegularExpression(@"^[A-Za-z0-9-]{3,50}$", ErrorMessage = "Mã tổ chức gồm 3–50 chữ, số hoặc dấu gạch ngang.")] public string TenantCode { get; set; } = "";
    [Required(ErrorMessage = "Hãy nhập tên tài khoản."), StringLength(50, ErrorMessage = "Tên tài khoản tối đa 50 ký tự.")] public string AccountName { get; set; } = "";
}
public sealed class SystemPasswordRecoveryRequest
{
    [Required(ErrorMessage = "Hãy nhập tên tài khoản."), StringLength(100, ErrorMessage = "Tên tài khoản tối đa 100 ký tự.")] public string Username { get; set; } = "";
}
public class PasswordRecoveryLink
{
    public Guid RequestId { get; set; }
    [Required(ErrorMessage = "Liên kết không hợp lệ."), StringLength(43, MinimumLength = 43, ErrorMessage = "Liên kết không hợp lệ."), RegularExpression(@"^[A-Za-z0-9_-]{43}$", ErrorMessage = "Liên kết không hợp lệ.")] public string Token { get; set; } = "";
}
public sealed class TenantPasswordRecoveryLink : PasswordRecoveryLink
{
    [Required(ErrorMessage = "Liên kết thiếu mã tổ chức."), StringLength(50), RegularExpression(@"^[A-Za-z0-9-]{3,50}$", ErrorMessage = "Mã tổ chức không hợp lệ.")] public string TenantCode { get; set; } = "";
}
public class PasswordRecoveryReset : PasswordRecoveryLink
{
    [Required(ErrorMessage = "Hãy nhập mật khẩu mới."), StringLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự."), NewAccountPassword] public string NewPassword { get; set; } = "";
    [Required(ErrorMessage = "Hãy nhập lại mật khẩu mới."), StringLength(100, ErrorMessage = "Mật khẩu tối đa 100 ký tự.")] public string ConfirmPassword { get; set; } = "";
}
public sealed class TenantPasswordRecoveryReset : PasswordRecoveryReset
{
    [Required(ErrorMessage = "Liên kết thiếu mã tổ chức."), StringLength(50), RegularExpression(@"^[A-Za-z0-9-]{3,50}$", ErrorMessage = "Mã tổ chức không hợp lệ.")] public string TenantCode { get; set; } = "";
}

public abstract class PasswordRecoveryControllerBase(
    PasswordRecoveryService recovery, RecoveryRequestLimiter limiter,
    RecoveryTelemetry telemetry, ILogger logger) : ControllerBase
{
    protected PasswordRecoveryService Recovery => recovery;
    protected RecoveryRequestLimiter Limiter => limiter;
    protected async Task<IActionResult> Run(string scope, bool request, Func<Task<object>> action)
    {
        Response.Headers.CacheControl = "no-store"; Response.Headers["Referrer-Policy"] = "no-referrer";
        var timer = Stopwatch.StartNew();
        var outcome = "failed";
        try
        {
            recovery.EnsureEnabled();
            var retry = limiter.CheckIp(scope, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", request);
            if (retry > 0)
            {
                Response.Headers.RetryAfter = retry.ToString();
                outcome = "throttled";
                return StatusCode(429, new { code = "PasswordRecoveryRateLimited", message = "Có quá nhiều yêu cầu. Vui lòng thử lại sau.", resendAfterSeconds = retry });
            }
            var result = await action(); outcome = "accepted";
            return StatusCode(request ? 202 : 200, result);
        }
        catch (RbacOperationException ex)
        {
            outcome = ex.Code == "PasswordRecoveryLinkInvalid" ? "invalid_link" : "rejected";
            return StatusCode(ex.StatusCode, new { code = ex.Code, message = ex.Message });
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // No exception dump: SQL/crypto/request payloads must not appear in logs or public errors.
            logger.LogError("Password recovery unavailable. Scope {Scope}, correlation {CorrelationId}.", scope, HttpContext.TraceIdentifier);
            return StatusCode(503, new { code = "PasswordRecoveryUnavailable", message = "Chưa thể xử lý yêu cầu. Vui lòng thử lại sau.", correlationId = HttpContext.TraceIdentifier });
        }
        finally
        {
            // All request branches have the same response floor, including nonexistent tenant/account.
            // This is mitigation, not a guarantee of identical network/database timing.
            if (request && timer.ElapsedMilliseconds < 250)
                await Task.Delay((int)(250 - timer.ElapsedMilliseconds), HttpContext.RequestAborted);
            var operation = request ? "request" : HttpContext.Request.Path.Value?.TrimEnd('/').EndsWith("/validate", StringComparison.OrdinalIgnoreCase) == true ? "validate" : "reset";
            telemetry.Record(scope, operation, outcome, timer.Elapsed.TotalMilliseconds);
        }
    }
}

[ApiController, Route("api/auth/password-recovery"), AllowWithoutTenant]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TenantPasswordRecoveryController(
    ITenantResolver resolver, ITenantDbContextFactory factory, PasswordRecoveryService recovery,
    RecoveryRequestLimiter limiter, RecoveryTelemetry telemetry, ILogger<TenantPasswordRecoveryController> logger)
    : PasswordRecoveryControllerBase(recovery, limiter, telemetry, logger)
{
    private async Task<object> WithStore(string code, bool request, Func<RecoveryStore, Task<object>> action, CancellationToken ct)
    {
        // Explicit DTO routing only. Never resolve tenant DbContext from DI or change the existing session tenant.
        var tenant = await resolver.ResolveAsync(code, ct);
        if (tenant is null) { if (request) return Recovery.Accepted; throw PasswordRecoveryService.InvalidLink(); }
        await using var db = factory.Create(tenant.ConnectionString);
        return await action(new(db, $"tenant:{tenant.TenantId}", tenant.TenantId, tenant.TenantCode));
    }
    [HttpPost("request")]
    public Task<IActionResult> RequestRecovery(TenantPasswordRecoveryRequest dto, CancellationToken ct) => Run("tenant", true, async () =>
    {
        if (!Limiter.CheckAccount($"tenant:{dto.TenantCode.Trim()}", dto.AccountName)) return Recovery.Accepted;
        return await WithStore(dto.TenantCode, true, async store => await Recovery.RequestAsync(store, dto.AccountName, ct), ct);
    });
    [HttpPost("validate")]
    public Task<IActionResult> Validate(TenantPasswordRecoveryLink dto, CancellationToken ct) => Run("tenant", false,
        () => WithStore(dto.TenantCode, false, async store => await Recovery.ValidateAsync(store, dto.RequestId, dto.Token, ct), ct));
    [HttpPost("reset")]
    public Task<IActionResult> Reset(TenantPasswordRecoveryReset dto, CancellationToken ct) => Run("tenant", false,
        () => WithStore(dto.TenantCode, false, async store => await Recovery.ResetAsync(store, dto.RequestId, dto.Token, dto.NewPassword, dto.ConfirmPassword, ct), ct));
}

[ApiController, Route("api/system-auth/password-recovery"), AllowWithoutTenant]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SystemPasswordRecoveryController(
    CentralDbContext db, PasswordRecoveryService recovery, RecoveryRequestLimiter limiter,
    RecoveryTelemetry telemetry, ILogger<SystemPasswordRecoveryController> logger)
    : PasswordRecoveryControllerBase(recovery, limiter, telemetry, logger)
{
    private RecoveryStore Store => new(db, "central");
    [HttpPost("request")]
    public Task<IActionResult> RequestRecovery(SystemPasswordRecoveryRequest dto, CancellationToken ct) => Run("central", true, async () =>
        Limiter.CheckAccount("central", dto.Username) ? await Recovery.RequestAsync(Store, dto.Username, ct) : Recovery.Accepted);
    [HttpPost("validate")]
    public Task<IActionResult> Validate(PasswordRecoveryLink dto, CancellationToken ct) => Run("central", false,
        async () => await Recovery.ValidateAsync(Store, dto.RequestId, dto.Token, ct));
    [HttpPost("reset")]
    public Task<IActionResult> Reset(PasswordRecoveryReset dto, CancellationToken ct) => Run("central", false,
        async () => await Recovery.ResetAsync(Store, dto.RequestId, dto.Token, dto.NewPassword, dto.ConfirmPassword, ct));
}

[ApiController, Route("api/system-auth/password-recovery/health"), AllowWithoutTenant, SystemAdminAuthorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RecoveryHealthController(RecoveryTelemetry telemetry) : ControllerBase
{
    [HttpGet]
    public object Get() => telemetry.Status();
}
