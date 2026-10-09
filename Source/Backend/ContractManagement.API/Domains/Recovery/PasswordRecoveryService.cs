using System.Security.Cryptography;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Recovery;
using ContractManagement.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Recovery;

public sealed record PasswordRecoveryAccepted(string Message, int ResendAfterSeconds);
public sealed record PasswordRecoverySuccess(string Message);

public sealed class PasswordRecoveryService(
    IServiceProvider services, IOptions<AccountRecoveryOptions> options,
    AccountRecoveryCryptography crypto, TimeProvider clock, IHttpContextAccessor http)
{
    public const string AcceptedMessage = "Nếu thông tin hợp lệ và tài khoản có email khôi phục đã xác minh, hệ thống sẽ gửi liên kết đặt lại mật khẩu. Vui lòng kiểm tra hộp thư và thư rác.";
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public PasswordRecoveryAccepted Accepted => new(AcceptedMessage, options.Value.ResendSeconds);
    public void EnsureEnabled()
    {
        if (!options.Value.Enabled) throw new RbacOperationException(503, "AccountRecoveryDisabled", "Khôi phục mật khẩu chưa được bật. Vui lòng liên hệ người quản trị.");
    }

    // Unknown, duplicate, inactive and unverified accounts intentionally share the public response.
    public Task<PasswordRecoveryAccepted> RequestAsync(RecoveryStore store, string name, CancellationToken ct) => Execute(store, async () =>
    {
        EnsureEnabled();
        name = name.Trim();
        var ids = store.Db is CentralDbContext central
            ? await central.SystemAdmins.Where(x => x.Username == name).Select(x => x.SystemAdminId).Take(2).ToListAsync(ct)
            : await ((DbDtctechContext)store.Db).TblEmployees.Where(x => x.EmployeeAccount == name).Select(x => x.EmployeeId).Take(2).ToListAsync(ct);
        if (ids.Count != 1)
        {
            if (ids.Count > 1)
            {
                store.Audit(ids[0], AuthorizationAuditActionTypes.PasswordRecoveryDenied, Now, http.HttpContext, "DuplicateAccount", actor: false);
                await store.Db.SaveChangesAsync(ct);
            }
            return Accepted;
        }
        var id = ids[0];
        var account = (await store.LoadAccountAsync(id, ct))!;
        if (!account.Active || account.VerifiedAt is null || !SmtpEmailOptions.IsEmailAddress(account.Email)) return Accepted;
        var now = Now;
        var challenges = store.Db.Set<AccountRecoveryChallenge>();
        var open = await challenges.SingleOrDefaultAsync(x => x.AccountId == id && x.Purpose == RecoveryPurpose.PasswordReset
            && x.UsedAtUtc == null && x.RevokedAtUtc == null, ct);
        if (open is not null && (open.ExpiresAtUtc <= now || !account.Matches(open)))
        {
            open.RevokedAtUtc = now;
            await ClearMail(store, open.RecoveryChallengeId, now, ct);
            await store.Db.SaveChangesAsync(ct);
            open = null;
        }
        string token;
        if (open is not null)
        {
            if (open.LastRequestedAtUtc.AddSeconds(options.Value.ResendSeconds) > now || open.RequestCount >= options.Value.RequestsPerChallenge) return Accepted;
            if (await store.Db.Set<AccountRecoveryEmailOutbox>().AnyAsync(x => x.RecoveryChallengeId == open.RecoveryChallengeId
                && (x.Status == RecoveryDeliveryStatus.Pending || x.Status == RecoveryDeliveryStatus.Leased), ct)) return Accepted;
            var original = await store.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.RecoveryChallengeId == open.RecoveryChallengeId && x.TokenCiphertext != null)
                .OrderBy(x => x.OutboxId).FirstOrDefaultAsync(ct);
            if (original?.TokenCiphertext is null)
                throw new RbacOperationException(503, "PasswordRecoveryUnavailable", "Chưa thể xử lý yêu cầu. Vui lòng thử lại sau.");
            token = crypto.Decrypt(original.TokenCiphertext, store.Scope);
            open.LastRequestedAtUtc = now; open.RequestCount++;
        }
        else
        {
            token = crypto.CreateToken();
            var publicId = Guid.NewGuid();
            open = new() { PublicId = publicId, AccountId = id, Purpose = RecoveryPurpose.PasswordReset,
                TokenHash = AccountRecoveryCryptography.HashToken(store.Scope, id, RecoveryPurpose.PasswordReset, publicId, token),
                EmailHash = AccountRecoveryCryptography.HashEmail(account.Email!), SessionVersionAtIssue = account.SessionVersion,
                CreatedAtUtc = now, LastRequestedAtUtc = now, ExpiresAtUtc = now.AddMinutes(options.Value.PasswordResetMinutes), RequestCount = 1 };
            challenges.Add(open); account.Touch(now);
            await store.Db.SaveChangesAsync(ct);
        }
        account.Touch(now); // Rowversion protects a racing email/security change as well as the challenge.
        store.Db.Set<AccountRecoveryEmailOutbox>().Add(new() {
            AccountId = id, RecoveryChallengeId = open.RecoveryChallengeId, TemplateKind = RecoveryEmailKind.PasswordReset,
            RecipientCiphertext = crypto.Encrypt(account.Email!, store.Scope), TokenCiphertext = crypto.Encrypt(token, store.Scope),
            CreatedAtUtc = now, ExpiresAtUtc = open.ExpiresAtUtc, Status = RecoveryDeliveryStatus.Pending, NextAttemptAtUtc = now });
        store.Audit(id, AuthorizationAuditActionTypes.PasswordRecoveryRequested, now, http.HttpContext, actor: false);
        await store.Db.SaveChangesAsync(ct);
        return Accepted;
    }, ct, request: true);

    public async Task<PasswordRecoverySuccess> ValidateAsync(RecoveryStore store, Guid publicId, string token, CancellationToken ct)
    {
        EnsureEnabled();
        await RequireLink(store, publicId, token, ct, readOnly: true);
        return new("Liên kết hợp lệ.");
    }

    public Task<PasswordRecoverySuccess> ResetAsync(RecoveryStore store, Guid publicId, string token, string password, string confirm, CancellationToken ct) => Execute(store, async () =>
    {
        EnsureEnabled();
        var (challenge, account) = await RequireLink(store, publicId, token, ct);
        AccountPasswordPolicy.EnsureValid(password);
        if (!string.Equals(password, confirm, StringComparison.Ordinal))
            throw new RbacOperationException(400, "PasswordConfirmationMismatch", "Mật khẩu nhập lại chưa khớp.");
        var now = Now;
        account.ResetPassword(services, password, now);
        challenge.UsedAtUtc = now;
        // Revoke all remaining purposes, including email verification, atomically with the new password.
        await RecoveryEmailInvalidation.RevokeAsync(store.Db, challenge.AccountId, now, ct);
        store.Audit(challenge.AccountId, AuthorizationAuditActionTypes.PasswordRecoveryCompleted, now, http.HttpContext, actor: false);
        store.Db.Set<AccountRecoveryEmailOutbox>().Add(new() {
            AccountId = challenge.AccountId, TemplateKind = RecoveryEmailKind.PasswordChanged,
            RecipientCiphertext = crypto.Encrypt(account.Email!, store.Scope), CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(24), Status = RecoveryDeliveryStatus.Pending, NextAttemptAtUtc = now });
        await store.Db.SaveChangesAsync(ct);
        return new PasswordRecoverySuccess("Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.");
    }, ct, deniedPublicId: publicId);

    private async Task<(AccountRecoveryChallenge, RecoveryAccount)> RequireLink(RecoveryStore store, Guid id, string token, CancellationToken ct, bool readOnly = false)
    {
        var rows = store.Db.Set<AccountRecoveryChallenge>().AsQueryable();
        if (readOnly) rows = rows.AsNoTracking();
        var challenge = await rows.SingleOrDefaultAsync(x => x.PublicId == id, ct);
        var account = challenge is null ? null : await store.LoadAccountAsync(challenge.AccountId, ct);
        if (challenge is null || account is null || challenge.Purpose != RecoveryPurpose.PasswordReset || challenge.ExpiresAtUtc <= Now
            || challenge.UsedAtUtc is not null || challenge.RevokedAtUtc is not null || !account.Matches(challenge)
            || !CryptographicOperations.FixedTimeEquals(challenge.TokenHash,
                AccountRecoveryCryptography.HashToken(store.Scope, challenge.AccountId, RecoveryPurpose.PasswordReset, id, token)))
            throw InvalidLink();
        return (challenge, account);
    }
    public static RbacOperationException InvalidLink() => new(400, "PasswordRecoveryLinkInvalid", "Liên kết không còn hợp lệ. Vui lòng yêu cầu liên kết mới. Nếu đã tải lại trang, hãy mở lại email.");
    private static async Task ClearMail(RecoveryStore store, long id, DateTime now, CancellationToken ct)
    {
        var rows = await store.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.RecoveryChallengeId == id).ToListAsync(ct);
        foreach (var row in rows) RecoveryEmailInvalidation.Clear(row, now, "ChallengeClosed");
    }
    private async Task<T> Execute<T>(RecoveryStore store, Func<Task<T>> action, CancellationToken ct, bool request = false, Guid? deniedPublicId = null)
    {
        try
        {
            var attempts = 0;
            return await store.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                if (attempts++ > 0) store.Db.ChangeTracker.Clear();
                await using var tx = store.Db.Database.IsRelational() ? await store.Db.Database.BeginTransactionAsync(ct) : null;
                var result = await action();
                if (tx is not null) await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            if (request) return (T)(object)Accepted; // Concurrent issuance winner already holds the public request.
            throw new RbacOperationException(409, "PasswordRecoveryConflict", "Thông tin đã thay đổi. Vui lòng thử lại; nếu liên kết đã dùng, hãy đăng nhập bằng mật khẩu mới.");
        }
        catch (DbUpdateException ex) when (request && ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        { return (T)(object)Accepted; } // Filtered unique issuance race only; other SQL failures are operational errors.
        catch (RbacOperationException ex) when (deniedPublicId is not null && ex.StatusCode == 400)
        {
            // Failed transaction has been disposed/rolled back. Clear staged mutations before recording rejection.
            // Validation remains read-only; this audit is for explicit reset submission only.
            store.Db.ChangeTracker.Clear();
            var id = await store.Db.Set<AccountRecoveryChallenge>().AsNoTracking()
                .Where(x => x.PublicId == deniedPublicId.Value).Select(x => x.AccountId).SingleOrDefaultAsync(ct);
            if (id > 0)
            {
                store.Audit(id, AuthorizationAuditActionTypes.PasswordRecoveryDenied, Now, http.HttpContext, ex.Code, actor: false);
                await store.Db.SaveChangesAsync(ct);
            }
            throw;
        }
    }
}
