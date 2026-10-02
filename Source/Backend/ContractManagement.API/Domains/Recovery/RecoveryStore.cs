using ContractManagement.API.Common.Security;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Recovery;
using ContractManagement.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Recovery;

// Resolving a Central store never resolves the tenant DbContext.
public sealed record RecoveryStore(DbContext Db, string Scope, int? TenantId = null, string? TenantCode = null)
{
    public async Task<RecoveryAccount?> LoadAccountAsync(int id, CancellationToken ct)
    {
        if (Db is CentralDbContext central)
        {
            var admin = await central.SystemAdmins.SingleOrDefaultAsync(x => x.SystemAdminId == id, ct);
            return admin is null ? null : new RecoveryAccount(admin);
        }
        var employee = await ((DbDtctechContext)Db).TblEmployees.SingleOrDefaultAsync(x => x.EmployeeId == id, ct);
        return employee is null ? null : new RecoveryAccount(employee);
    }

    public void Audit(int id, string action, DateTime now, HttpContext? http = null, string? failure = null, bool actor = true)
    {
        var result = failure is null ? AuthorizationAuditResultTypes.Success
            : action == AuthorizationAuditActionTypes.PasswordRecoveryDenied ? AuthorizationAuditResultTypes.Denied : AuthorizationAuditResultTypes.Failed;
        var correlation = http?.TraceIdentifier ?? Guid.NewGuid().ToString("N");
        if (Db is CentralDbContext central)
            central.SecurityAudits.Add(AuthorizationAuditRecordFactory.CreateCentral(
                actor ? id : null, null, null, action, result, "AccountRecovery", id.ToString(),
                null, null, null, null, failure, now, http?.Connection.RemoteIpAddress?.ToString(), null, correlation));
        else
            ((DbDtctechContext)Db).TblAuthorizationAudits.Add(AuthorizationAuditRecordFactory.CreateTenant(
                TenantId!.Value, actor ? id : null, actor ? "Employee" : "System", action, result,
                "AccountRecovery", id.ToString(), null, null, null, null, failure, now,
                http?.Connection.RemoteIpAddress?.ToString(), null, correlation));
    }
}

public sealed class RecoveryAccount(object entity)
{
    public string? Email => entity is SystemAdmin a ? a.Email : ((TblEmployee)entity).EmployeeEmail;
    public int SessionVersion => entity is SystemAdmin a ? a.SessionVersion : ((TblEmployee)entity).SessionVersion;
    public bool Active => entity is SystemAdmin a ? a.IsActive : ((TblEmployee)entity).Status == 1;
    public bool Eligible => entity is SystemAdmin a ? a.IsActive && !a.MustChangePassword
        : ((TblEmployee)entity).Status == 1 && !((TblEmployee)entity).MustChangePassword;
    public DateTime? VerifiedAt => entity is SystemAdmin a ? a.RecoveryEmailVerifiedAt : ((TblEmployee)entity).RecoveryEmailVerifiedAt;
    public void VerifyPassword(IServiceProvider services, string password)
    {
        if (entity is SystemAdmin a)
            AccountPasswordPolicy.VerifyCurrentPassword(services.GetRequiredService<IPasswordHasher<SystemAdmin>>(), a, a.PasswordHash, password);
        else
        {
            var e = (TblEmployee)entity;
            AccountPasswordPolicy.VerifyCurrentPassword(services.GetRequiredService<IPasswordHasher<TblEmployee>>(), e, e.EmployeePassword, password);
        }
    }
    public void Touch(DateTime now, bool verify = false)
    {
        if (entity is SystemAdmin a) { a.UpdatedAt = now; if (verify) a.RecoveryEmailVerifiedAt = now; }
        else { var e = (TblEmployee)entity; e.DateModified = now; if (verify) e.RecoveryEmailVerifiedAt = now; }
    }
    public void ResetPassword(IServiceProvider services, string password, DateTime now)
    {
        if (entity is SystemAdmin a)
        {
            var hasher = services.GetRequiredService<IPasswordHasher<SystemAdmin>>();
            AccountPasswordPolicy.EnsureNotReused(hasher, a, a.PasswordHash, password);
            a.PasswordHash = hasher.HashPassword(a, password);
            a.PasswordChangedAt = now; a.MustChangePassword = false; a.SessionVersion = checked(a.SessionVersion + 1);
        }
        else
        {
            var e = (TblEmployee)entity;
            var hasher = services.GetRequiredService<IPasswordHasher<TblEmployee>>();
            AccountPasswordPolicy.EnsureNotReused(hasher, e, e.EmployeePassword, password);
            e.EmployeePassword = hasher.HashPassword(e, password);
            e.PasswordChangedAt = now; e.MustChangePassword = false; e.SessionVersion = checked(e.SessionVersion + 1);
        }
        Touch(now);
    }
    public bool Matches(AccountRecoveryChallenge challenge) =>
        (challenge.Purpose == RecoveryPurpose.PasswordReset ? Active && VerifiedAt is not null : Eligible) && Email is not null
        && challenge.SessionVersionAtIssue == SessionVersion
        && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            AccountRecoveryCryptography.HashEmail(Email), challenge.EmailHash);
}

public static class RecoveryEmailInvalidation
{
    // Caller saves alongside the account email change: a single atomic SaveChanges.
    public static async Task RevokeAsync(DbContext db, int id, DateTime now, CancellationToken ct)
    {
        var challenges = await db.Set<AccountRecoveryChallenge>()
            .Where(x => x.AccountId == id && x.UsedAtUtc == null && x.RevokedAtUtc == null).ToListAsync(ct);
        foreach (var challenge in challenges)
            if (challenge.UsedAtUtc is null) challenge.RevokedAtUtc = now;
        var mail = await db.Set<AccountRecoveryEmailOutbox>()
            .Where(x => x.AccountId == id && (x.TokenCiphertext != null || x.RecipientCiphertext != null)).ToListAsync(ct);
        foreach (var row in mail) Clear(row, now, "AccountChanged");
    }
    public static void Clear(AccountRecoveryEmailOutbox row, DateTime now, string code)
    {
        row.TokenCiphertext = null; row.RecipientCiphertext = null;
        if (row.Status is RecoveryDeliveryStatus.Pending or RecoveryDeliveryStatus.Leased)
        {
            row.Status = RecoveryDeliveryStatus.Failed; row.FailedAtUtc = now; row.FailureCode = code;
        }
        row.LeaseId = null; row.LeaseUntilUtc = null;
    }
}
