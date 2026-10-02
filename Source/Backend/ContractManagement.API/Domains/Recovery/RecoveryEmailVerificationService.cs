using System.Security.Cryptography;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContractManagement.Infrastructure.Security;

namespace ContractManagement.API.Domains.Recovery;

public sealed record RecoveryEmailStatus(bool Enabled, DateTime? VerifiedAt, bool Pending, int ResendAfterSeconds);
public sealed record RecoveryVerificationResult(string Message, int ResendAfterSeconds);

public sealed class RecoveryEmailVerificationService(
    IServiceProvider services, IOptions<AccountRecoveryOptions> options,
    AccountRecoveryCryptography crypto, TimeProvider clock, IHttpContextAccessor http)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    public async Task<RecoveryEmailStatus> StatusAsync(RecoveryStore store, int id, CancellationToken ct)
    {
        var account = await RequireAccount(store, id, ct);
        if (!options.Value.Enabled) return new(false, account.VerifiedAt, false, 0);
        var open = await store.Db.Set<AccountRecoveryChallenge>().AsNoTracking().SingleOrDefaultAsync(
            x => x.AccountId == id && x.Purpose == RecoveryPurpose.VerifyRecoveryEmail && x.UsedAtUtc == null && x.RevokedAtUtc == null, ct);
        var pending = open is not null && open.ExpiresAtUtc > Now && account.Matches(open);
        return new(true, account.VerifiedAt, pending, pending ? Cooldown(open!) : 0);
    }
    public Task<RecoveryVerificationResult> RequestAsync(RecoveryStore store, int id, string password, CancellationToken ct) =>
        Execute(store, async () =>
        {
            EnsureEnabled();
            var account = await RequireAccount(store, id, ct);
            account.VerifyPassword(services, password);
            if (!SmtpEmailOptions.IsEmailAddress(account.Email)) throw Error(400, "RecoveryEmailMissing", "Hãy lưu một địa chỉ email hợp lệ trước khi xác minh.");
            if (account.VerifiedAt is not null) return new RecoveryVerificationResult("Email đã được xác minh.", 0);
            var now = Now;
            var rows = store.Db.Set<AccountRecoveryChallenge>();
            var open = await rows.SingleOrDefaultAsync(x => x.AccountId == id && x.Purpose == RecoveryPurpose.VerifyRecoveryEmail
                && x.UsedAtUtc == null && x.RevokedAtUtc == null, ct);
            if (open is not null && (open.ExpiresAtUtc <= now || !account.Matches(open)))
            {
                open.RevokedAtUtc = now;
                await ClearMail(store, open.RecoveryChallengeId, now, ct);
                await store.Db.SaveChangesAsync(ct); // Free the filtered unique slot inside this transaction.
                open = null;
            }
            string token;
            if (open is not null)
            {
                if (Cooldown(open) > 0) throw Error(429, "RecoveryEmailCooldown", $"Vui lòng chờ {Cooldown(open)} giây trước khi gửi lại.");
                if (open.RequestCount >= options.Value.RequestsPerChallenge)
                    throw Error(429, "RecoveryEmailQuota", "Đã đạt giới hạn gửi lại. Vui lòng chờ liên kết hết hạn.");
                var original = await store.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.RecoveryChallengeId == open.RecoveryChallengeId
                    && x.TokenCiphertext != null).OrderBy(x => x.OutboxId).FirstOrDefaultAsync(ct);
                if (original?.TokenCiphertext is null) throw Error(409, "RecoveryEmailUnavailable", "Không thể gửi lại liên kết này. Vui lòng thử lại sau.");
                if (await store.Db.Set<AccountRecoveryEmailOutbox>().AnyAsync(x => x.RecoveryChallengeId == open.RecoveryChallengeId
                    && (x.Status == RecoveryDeliveryStatus.Pending || x.Status == RecoveryDeliveryStatus.Leased), ct))
                    return new RecoveryVerificationResult("Yêu cầu đang chờ gửi email.", options.Value.ResendSeconds);
                token = crypto.Decrypt(original.TokenCiphertext, store.Scope);
                open.RequestCount++; open.LastRequestedAtUtc = now;
            }
            else
            {
                token = crypto.CreateToken();
                var publicId = Guid.NewGuid();
                open = new AccountRecoveryChallenge {
                    PublicId = publicId, AccountId = id, Purpose = RecoveryPurpose.VerifyRecoveryEmail,
                    TokenHash = AccountRecoveryCryptography.HashToken(store.Scope, id, RecoveryPurpose.VerifyRecoveryEmail, publicId, token),
                    EmailHash = AccountRecoveryCryptography.HashEmail(account.Email!), SessionVersionAtIssue = account.SessionVersion,
                    CreatedAtUtc = now, ExpiresAtUtc = now.AddMinutes(options.Value.EmailVerificationMinutes),
                    LastRequestedAtUtc = now, RequestCount = 1
                };
                rows.Add(open);
                account.Touch(now); // Account rowversion also protects concurrent profile/security changes.
                await store.Db.SaveChangesAsync(ct);
            }
            account.Touch(now);
            store.Db.Set<AccountRecoveryEmailOutbox>().Add(new() {
                RecoveryChallengeId = open.RecoveryChallengeId, AccountId = id, TemplateKind = RecoveryEmailKind.VerifyRecoveryEmail,
                RecipientCiphertext = crypto.Encrypt(account.Email!, store.Scope), TokenCiphertext = crypto.Encrypt(token, store.Scope),
                CreatedAtUtc = now, ExpiresAtUtc = open.ExpiresAtUtc, NextAttemptAtUtc = now, Status = RecoveryDeliveryStatus.Pending
            });
            store.Audit(id, AuthorizationAuditActionTypes.RecoveryEmailVerificationRequested, now, http.HttpContext);
            await store.Db.SaveChangesAsync(ct);
            return new RecoveryVerificationResult("Đã tiếp nhận yêu cầu xác minh. Vui lòng kiểm tra email và thư rác.", options.Value.ResendSeconds);
        }, ct);

    public Task<RecoveryVerificationResult> VerifyAsync(RecoveryStore store, int id, Guid requestId, string token, CancellationToken ct) =>
        Execute(store, async () =>
        {
            EnsureEnabled();
            var account = await RequireAccount(store, id, ct);
            var challenge = await store.Db.Set<AccountRecoveryChallenge>().SingleOrDefaultAsync(x => x.PublicId == requestId, ct);
            if (challenge is null || challenge.AccountId != id || challenge.Purpose != RecoveryPurpose.VerifyRecoveryEmail
                || challenge.ExpiresAtUtc <= Now || challenge.UsedAtUtc is not null || challenge.RevokedAtUtc is not null
                || !account.Matches(challenge) || !CryptographicOperations.FixedTimeEquals(challenge.TokenHash,
                    AccountRecoveryCryptography.HashToken(store.Scope, id, RecoveryPurpose.VerifyRecoveryEmail, requestId, token)))
                throw Error(400, "RecoveryEmailLinkInvalid", "Liên kết không còn hợp lệ hoặc không thuộc tài khoản đang đăng nhập. Vui lòng yêu cầu liên kết mới.");
            var now = Now;
            account.Touch(now, verify: true); challenge.UsedAtUtc = now;
            await ClearMail(store, challenge.RecoveryChallengeId, now, ct);
            store.Audit(id, AuthorizationAuditActionTypes.RecoveryEmailVerified, now, http.HttpContext);
            await store.Db.SaveChangesAsync(ct);
            return new RecoveryVerificationResult("Email khôi phục đã được xác minh.", 0);
        }, ct);

    private static async Task ClearMail(RecoveryStore store, long challengeId, DateTime now, CancellationToken ct)
    {
        var rows = await store.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.RecoveryChallengeId == challengeId).ToListAsync(ct);
        foreach (var row in rows) RecoveryEmailInvalidation.Clear(row, now, "ChallengeClosed");
    }
    private int Cooldown(AccountRecoveryChallenge row) => Math.Max(0, (int)Math.Ceiling(
        (row.LastRequestedAtUtc.AddSeconds(options.Value.ResendSeconds) - Now).TotalSeconds));
    private void EnsureEnabled() { if (!options.Value.Enabled) throw Error(503, "AccountRecoveryDisabled", "Xác minh email khôi phục chưa được bật."); }
    private static async Task<RecoveryAccount> RequireAccount(RecoveryStore store, int id, CancellationToken ct)
    {
        var account = await store.LoadAccountAsync(id, ct);
        if (account is null || !account.Eligible) throw Error(403, "RecoveryAccountUnavailable", "Tài khoản không được phép xác minh email. Hãy hoàn tất đổi mật khẩu bắt buộc nếu có.");
        return account;
    }
    private static RbacOperationException Error(int status, string code, string message) => new(status, code, message);
    private static async Task<T> Execute<T>(RecoveryStore store, Func<Task<T>> operation, CancellationToken ct)
    {
        try
        {
            var attempts = 0;
            return await store.Db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
                if (attempts++ > 0) store.Db.ChangeTracker.Clear();
                await using var tx = store.Db.Database.IsRelational() ? await store.Db.Database.BeginTransactionAsync(ct) : null;
                var result = await operation();
                if (tx is not null) await tx.CommitAsync(ct);
                return result;
            });
        }
        catch (DbUpdateException) { throw Error(409, "RecoveryEmailConflict", "Thông tin đã thay đổi. Vui lòng tải lại và thử lại."); }
    }
}
