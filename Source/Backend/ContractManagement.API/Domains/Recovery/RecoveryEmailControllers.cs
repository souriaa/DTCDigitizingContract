using System.ComponentModel.DataAnnotations;
using ContractManagement.API.Common.Security;
using ContractManagement.Attributes;
using ContractManagement.Filter;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.AspNetCore.Mvc;

namespace ContractManagement.API.Domains.Recovery;

public sealed class RequestRecoveryEmailVerification
{
    [Required(ErrorMessage = "Hãy nhập mật khẩu hiện tại."), StringLength(1000)] public string CurrentPassword { get; set; } = "";
}
public sealed class VerifyRecoveryEmail
{
    public Guid RequestId { get; set; }
    [Required(ErrorMessage = "Liên kết xác minh không hợp lệ."), StringLength(64, MinimumLength = 43, ErrorMessage = "Liên kết xác minh không hợp lệ.")]
    public string Token { get; set; } = "";
}

[ApiController, Route("api/auth/recovery-email"), SessionAuthorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TenantRecoveryEmailController(
    DbDtctechContext db, ICurrentTenant tenant, RecoveryEmailVerificationService verification) : ControllerBase
{
    private RecoveryStore Store => new(db, $"tenant:{tenant.GetRequiredTenant().TenantId}",
        tenant.GetRequiredTenant().TenantId, tenant.GetRequiredTenant().TenantCode);
    private int AccountId => HttpContext.Session.GetInt32(AccountSessionKeys.EmployeeId)!.Value;
    [HttpGet("status")]
    public Task<RecoveryEmailStatus> Status(CancellationToken ct) => verification.StatusAsync(Store, AccountId, ct);
    [HttpPost("request-verification")]
    public Task<RecoveryVerificationResult> RequestVerification(RequestRecoveryEmailVerification request, CancellationToken ct) =>
        verification.RequestAsync(Store, AccountId, request.CurrentPassword, ct);
    [HttpPost("verify")]
    public Task<RecoveryVerificationResult> Verify(VerifyRecoveryEmail request, CancellationToken ct) =>
        verification.VerifyAsync(Store, AccountId, request.RequestId, request.Token, ct);
}

[ApiController, Route("api/system-auth/recovery-email"), AllowWithoutTenant, SystemAdminAuthorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SystemAdminRecoveryEmailController(
    CentralDbContext db, RecoveryEmailVerificationService verification) : ControllerBase
{
    private RecoveryStore Store => new(db, "central");
    private int AccountId => HttpContext.Session.GetInt32(AccountSessionKeys.SystemAdminId)!.Value;
    [HttpGet("status")]
    public Task<RecoveryEmailStatus> Status(CancellationToken ct) => verification.StatusAsync(Store, AccountId, ct);
    [HttpPost("request-verification")]
    public Task<RecoveryVerificationResult> RequestVerification(RequestRecoveryEmailVerification request, CancellationToken ct) =>
        verification.RequestAsync(Store, AccountId, request.CurrentPassword, ct);
    [HttpPost("verify")]
    public Task<RecoveryVerificationResult> Verify(VerifyRecoveryEmail request, CancellationToken ct) =>
        verification.VerifyAsync(Store, AccountId, request.RequestId, request.Token, ct);
}
