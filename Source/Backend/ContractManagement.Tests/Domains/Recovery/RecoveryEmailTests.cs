using System.Security.Cryptography;
using ContractManagement.API.Common.Security;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Central.Entities;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Domains.Recovery;

public sealed class RecoveryEmailTests
{
    [Theory, InlineData(false), InlineData(true)]
    public async Task PasswordReset_IsReadOnlyUntilSubmit_ChangesOnlySecurity_ConsumesOnce(bool central)
    {
        await using var f = await Fixture.Create(central);
        await f.VerifyEmail();
        var service = f.PasswordService;
        var account = (await f.Store.LoadAccountAsync(1, default))!;
        var version = account.SessionVersion;
        await service.RequestAsync(f.Store, central ? "admin" : "user", default);
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
        var row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset);
        var token = f.Crypto.Decrypt(row.TokenCiphertext!, f.Store.Scope);
        Assert.Equal(version, account.SessionVersion);
        await service.ValidateAsync(f.Store, challenge.PublicId, token, default);
        Assert.Null(challenge.UsedAtUtc); Assert.Equal(version, account.SessionVersion);
        Assert.Equal("PasswordPolicyViolation", (await Assert.ThrowsAsync<RbacOperationException>(() => service.ResetAsync(f.Store, challenge.PublicId, token, "short1", "short1", default))).Code);
        Assert.Equal("PasswordConfirmationMismatch", (await Assert.ThrowsAsync<RbacOperationException>(() => service.ResetAsync(f.Store, challenge.PublicId, token, "ResetPassword123!", "different", default))).Code);
        Assert.Equal("PasswordReuseNotAllowed", (await Assert.ThrowsAsync<RbacOperationException>(() => service.ResetAsync(f.Store, challenge.PublicId, token, "Password123!", "Password123!", default))).Code);
        Assert.Null(challenge.UsedAtUtc);
        await service.ResetAsync(f.Store, challenge.PublicId, token, "ResetPassword123!", "ResetPassword123!", default);
        account = (await f.Store.LoadAccountAsync(1, default))!;
        challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
        row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset);
        Assert.Equal(version + 1, account.SessionVersion);
        account.VerifyPassword(f.Services, "ResetPassword123!");
        Assert.NotNull(challenge.UsedAtUtc); Assert.Null(row.TokenCiphertext);
        Assert.Equal("PasswordRecoveryLinkInvalid", (await Assert.ThrowsAsync<RbacOperationException>(() => service.ResetAsync(f.Store, challenge.PublicId, token, "AnotherPassword1!", "AnotherPassword1!", default))).Code);
        var notice = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordChanged);
        Assert.Null(notice.RecoveryChallengeId); Assert.Null(notice.TokenCiphertext);
        Assert.Equal(8, central ? await ((CentralDbContext)f.Db).SecurityAudits.CountAsync() : await ((DbDtctechContext)f.Db).TblAuthorizationAudits.CountAsync());
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task PasswordRequest_NoEnumeration_AndResendKeepsLinkAndExpiry(bool central)
    {
        await using var f = await Fixture.Create(central);
        var service = f.PasswordService;
        var unknown = await service.RequestAsync(f.Store, "does-not-exist", default);
        var unverified = await service.RequestAsync(f.Store, central ? "admin" : "user", default);
        Assert.Equal(unknown, unverified); Assert.Empty(await f.Db.Set<AccountRecoveryChallenge>().ToListAsync());
        await f.VerifyEmail();
        var accepted = await service.RequestAsync(f.Store, central ? "admin" : "user", default);
        Assert.Equal(unknown, accepted);
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
        var row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset);
        var token = f.Crypto.Decrypt(row.TokenCiphertext!, f.Store.Scope); var expiry = challenge.ExpiresAtUtc;
        await service.RequestAsync(f.Store, central ? "admin" : "user", default);
        Assert.Equal(1, challenge.RequestCount);
        row.Status = RecoveryDeliveryStatus.Sent; await f.Db.SaveChangesAsync();
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        await service.RequestAsync(f.Store, central ? "admin" : "user", default);
        var rows = await f.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.TemplateKind == RecoveryEmailKind.PasswordReset).ToListAsync();
        Assert.Equal(2, rows.Count); Assert.Equal(expiry, challenge.ExpiresAtUtc);
        Assert.All(rows, x => Assert.Equal(token, f.Crypto.Decrypt(x.TokenCiphertext!, f.Store.Scope)));
    }

    [Theory, InlineData("scope"), InlineData("purpose"), InlineData("email"), InlineData("version"), InlineData("expired"), InlineData("unverified"), InlineData("inactive")]
    public async Task PasswordReset_RechecksEveryConditionAfterValidate(string change)
    {
        await using var f = await Fixture.Create(true); await f.VerifyEmail();
        await f.PasswordService.RequestAsync(f.Store, "admin", default);
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
        var row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset);
        var token = f.Crypto.Decrypt(row.TokenCiphertext!, f.Store.Scope);
        await f.PasswordService.ValidateAsync(f.Store, challenge.PublicId, token, default);
        var account = await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync(); var store = f.Store;
        switch (change) {
            case "scope": store = new(f.Db, "tenant:9", 9, "other"); break;
            case "purpose": challenge.Purpose = RecoveryPurpose.VerifyRecoveryEmail; break;
            case "email": account.Email = "new@example.test"; break;
            case "version": account.SessionVersion++; break;
            case "expired": f.Clock.Advance(TimeSpan.FromMinutes(15)); break;
            case "unverified": account.RecoveryEmailVerifiedAt = null; break;
            case "inactive": account.IsActive = false; break;
        }
        await f.Db.SaveChangesAsync(); var hash = account.PasswordHash;
        Assert.Equal("PasswordRecoveryLinkInvalid", (await Assert.ThrowsAsync<RbacOperationException>(() => f.PasswordService.ResetAsync(store, challenge.PublicId, token, "ResetPassword123!", "ResetPassword123!", default))).Code);
        Assert.Equal(hash, account.PasswordHash); Assert.Null(challenge.UsedAtUtc);
        Assert.Empty(await f.Db.Set<AccountRecoveryEmailOutbox>().Where(x => x.TemplateKind == RecoveryEmailKind.PasswordChanged).ToListAsync());
    }

    [Fact]
    public async Task PasswordReset_AllowsPreviouslyVerifiedForcedChange_AndNoticeHasNoToken()
    {
        await using var f = await Fixture.Create(true); await f.VerifyEmail();
        (await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync()).MustChangePassword = true; await f.Db.SaveChangesAsync();
        await f.PasswordService.RequestAsync(f.Store, "admin", default);
        var mailbox = new RecoveryTestMailbox(); var worker = f.Worker(mailbox, new ThrowingTransport());
        await worker.ProcessAsync(f.Store, default);
        Assert.Single(mailbox.Messages);
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync(x => x.Purpose == RecoveryPurpose.PasswordReset);
        var row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.TemplateKind == RecoveryEmailKind.PasswordReset);
        var token = f.Crypto.Decrypt(row.TokenCiphertext!, f.Store.Scope);
        await f.PasswordService.ResetAsync(f.Store, challenge.PublicId, token, "ResetPassword123!", "ResetPassword123!", default);
        Assert.False((await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync()).MustChangePassword);
        await worker.ProcessAsync(f.Store, default);
        Assert.Equal(2, mailbox.Messages.Count);
        Assert.DoesNotContain(token, mailbox.Messages.Last().TextBody);
        Assert.DoesNotContain("ResetPassword123!", mailbox.Messages.Last().HtmlBody);
    }

    [Fact]
    public void Limiter_HandlesUnknownNamesSameAsKnown_CasingCooldownQuotaAndIpAreBounded()
    {
        var clock = new ManualClock(); var settings = Fixture.Settings();
        var limiter = new RecoveryRequestLimiter(clock, Options.Create(settings));
        Assert.True(limiter.CheckAccount("tenant:ABC", "admin"));
        Assert.False(limiter.CheckAccount("tenant:abc", "ADMIN"));
        Assert.True(limiter.CheckAccount("tenant:abc", "unknown"));
        Assert.False(limiter.CheckAccount("tenant:abc", "unknown"));
        clock.Advance(TimeSpan.FromSeconds(61)); Assert.True(limiter.CheckAccount("tenant:abc", "ADMIN"));
        clock.Advance(TimeSpan.FromSeconds(61)); Assert.True(limiter.CheckAccount("tenant:abc", "ADMIN"));
        clock.Advance(TimeSpan.FromSeconds(61)); Assert.False(limiter.CheckAccount("tenant:abc", "ADMIN"));
        for (var i = 0; i < 10; i++) Assert.Equal(0, limiter.CheckIp("tenant", "127.0.0.1", true));
        Assert.True(limiter.CheckIp("tenant", "127.0.0.1", true) > 0);
        Assert.Equal(0, limiter.CheckIp("central", "127.0.0.1", true));
        clock.Advance(TimeSpan.FromHours(1)); Assert.Equal(0, limiter.CheckIp("tenant", "127.0.0.1", true));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Verify_IsBoundToAccount_ConsumesOnce_AndErasesEnvelopes(bool central)
    {
        await using var f = await Fixture.Create(central);
        await f.Request();
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        var mail = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        var token = f.Crypto.Decrypt(mail.TokenCiphertext!, f.Store.Scope);
        Assert.Equal(43, token.Length);
        Assert.DoesNotContain(token, Convert.ToBase64String(challenge.TokenHash));
        Assert.Null((await f.Store.LoadAccountAsync(1, default))!.VerifiedAt);
        await f.Service.VerifyAsync(f.Store, 1, challenge.PublicId, token, default);
        Assert.Equal(f.Clock.Now, (await f.Store.LoadAccountAsync(1, default))!.VerifiedAt);
        Assert.NotNull(challenge.UsedAtUtc);
        Assert.Null(mail.TokenCiphertext); Assert.Null(mail.RecipientCiphertext);
        Assert.Equal(RecoveryDeliveryStatus.Failed, mail.Status);
        await f.Invalid(() => f.Service.VerifyAsync(f.Store, 1, challenge.PublicId, token, default));
        Assert.Equal(2, central ? await ((CentralDbContext)f.Db).SecurityAudits.CountAsync()
            : await ((DbDtctechContext)f.Db).TblAuthorizationAudits.CountAsync());
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Request_RejectsWrongPassword_DisabledAndForcedChange(bool central)
    {
        await using var f = await Fixture.Create(central);
        var error = await Assert.ThrowsAsync<RbacOperationException>(() => f.Service.RequestAsync(f.Store, 1, "wrong", default));
        Assert.Equal("CurrentPasswordIncorrect", error.Code);
        Assert.Empty(await f.Db.Set<AccountRecoveryChallenge>().ToListAsync());
        f.Options.Enabled = false;
        error = await Assert.ThrowsAsync<RbacOperationException>(() => f.Request());
        Assert.Equal("AccountRecoveryDisabled", error.Code);
        f.Options.Enabled = true;
        if (central) (await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync()).MustChangePassword = true;
        else (await ((DbDtctechContext)f.Db).TblEmployees.SingleAsync()).MustChangePassword = true;
        await f.Db.SaveChangesAsync();
        error = await Assert.ThrowsAsync<RbacOperationException>(() => f.Request());
        Assert.Equal(403, error.StatusCode);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Verify_RejectsAnotherAuthenticatedAccountEvenWithSameEmail(bool central)
    {
        await using var f = await Fixture.Create(central);
        await f.Request();
        if (central) f.Db.Add(new SystemAdmin { SystemAdminId = 2, Username = "other", FullName = "Other", Email = "test@example.test", IsActive = true, SessionVersion = 1, PasswordHash = "test-only", RowVersion = new byte[8] });
        else f.Db.Add(new TblEmployee { EmployeeId = 2, EmployeeAccount = "other", EmployeeEmail = "test@example.test", Status = 1, SessionVersion = 1, RowVersion = new byte[8] });
        await f.Db.SaveChangesAsync();
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        var token = f.Crypto.Decrypt((await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).TokenCiphertext!, f.Store.Scope);
        await f.Invalid(() => f.Service.VerifyAsync(f.Store, 2, challenge.PublicId, token, default));
        Assert.Null(challenge.UsedAtUtc);
        Assert.Null((await f.Store.LoadAccountAsync(2, default))!.VerifiedAt);
        await f.Service.VerifyAsync(f.Store, 1, challenge.PublicId, token, default);
    }

    [Fact]
    public async Task Worker_RecoversExpiredLeaseAndDoesNotSendAfterSecurityChange()
    {
        await using var f = await Fixture.Create(true);
        await f.Request();
        var row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        row.Status = RecoveryDeliveryStatus.Leased; row.LeaseId = Guid.NewGuid();
        row.LeaseUntilUtc = f.Clock.Now.AddSeconds(-1); row.AttemptCount = 1;
        await f.Db.SaveChangesAsync();
        var mailbox = new RecoveryTestMailbox(); var worker = f.Worker(mailbox, new ThrowingTransport());
        await worker.ProcessAsync(f.Store, default);
        row = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        Assert.Equal(RecoveryDeliveryStatus.Sent, row.Status); Assert.Equal(2, row.AttemptCount);
        Assert.Single(mailbox.Messages);
        f.Clock.Advance(TimeSpan.FromSeconds(61)); await f.Request();
        (await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync()).SessionVersion++;
        await f.Db.SaveChangesAsync();
        await worker.ProcessAsync(f.Store, default);
        Assert.Single(mailbox.Messages);
        var invalid = await f.Db.Set<AccountRecoveryEmailOutbox>().OrderByDescending(x => x.OutboxId).FirstAsync();
        Assert.Equal(RecoveryDeliveryStatus.Failed, invalid.Status); Assert.Null(invalid.TokenCiphertext);
    }

    [Theory, InlineData("scope"), InlineData("token"), InlineData("purpose"), InlineData("email"), InlineData("version"), InlineData("expired"), InlineData("revoked")]
    public async Task Verify_FailsClosedAfterSecurityChanges(string change)
    {
        await using var f = await Fixture.Create(true);
        await f.Request();
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        var mail = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        var token = f.Crypto.Decrypt(mail.TokenCiphertext!, f.Store.Scope);
        var store = f.Store;
        var account = await ((CentralDbContext)f.Db).SystemAdmins.SingleAsync();
        switch (change)
        {
            case "scope": store = new(f.Db, "tenant:99"); break;
            case "token": token = new string('x', 43); break;
            case "purpose": challenge.Purpose = RecoveryPurpose.PasswordReset; break;
            case "email": account.Email = "other@example.test"; break;
            case "version": account.SessionVersion++; break;
            case "expired": f.Clock.Advance(TimeSpan.FromMinutes(30)); break;
            case "revoked": challenge.RevokedAtUtc = f.Clock.Now; break;
        }
        await f.Db.SaveChangesAsync();
        await f.Invalid(() => f.Service.VerifyAsync(store, 1, challenge.PublicId, token, default));
        Assert.Null(account.RecoveryEmailVerifiedAt); Assert.Null(challenge.UsedAtUtc);
    }

    [Fact]
    public async Task Resend_ReusesLinkAndOriginalExpiry_EnforcesCooldownQuota()
    {
        await using var f = await Fixture.Create(true);
        await f.Request();
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        var original = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        var token = f.Crypto.Decrypt(original.TokenCiphertext!, f.Store.Scope);
        var expires = challenge.ExpiresAtUtc;
        var cooldown = await Assert.ThrowsAsync<RbacOperationException>(() => f.Request());
        Assert.Equal(429, cooldown.StatusCode);
        for (var request = 2; request <= 3; request++)
        {
            original.Status = RecoveryDeliveryStatus.Sent;
            await f.Db.SaveChangesAsync();
            f.Clock.Advance(TimeSpan.FromSeconds(61));
            await f.Request();
            original = await f.Db.Set<AccountRecoveryEmailOutbox>().OrderByDescending(x => x.OutboxId).FirstAsync();
            Assert.Equal(token, f.Crypto.Decrypt(original.TokenCiphertext!, f.Store.Scope));
            Assert.Equal(expires, challenge.ExpiresAtUtc);
        }
        f.Clock.Advance(TimeSpan.FromSeconds(61));
        var quota = await Assert.ThrowsAsync<RbacOperationException>(() => f.Request());
        Assert.Equal("RecoveryEmailQuota", quota.Code);
        Assert.Single(await f.Db.Set<AccountRecoveryChallenge>().ToListAsync());
    }

    [Fact]
    public async Task ExpiredChallenge_IsRevokedBeforeReplacement_NoAutoVerification()
    {
        await using var f = await Fixture.Create(false);
        await f.Request();
        var first = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        f.Clock.Advance(TimeSpan.FromMinutes(31));
        await f.Request();
        Assert.NotNull(first.RevokedAtUtc);
        Assert.Equal(2, await f.Db.Set<AccountRecoveryChallenge>().CountAsync());
        Assert.Null((await f.Db.Set<AccountRecoveryEmailOutbox>().OrderBy(x => x.OutboxId).FirstAsync()).TokenCiphertext);
        Assert.Null((await f.Store.LoadAccountAsync(1, default))!.VerifiedAt);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task EmailChange_RevokesOpenChallengeAndClearsAllMail(bool central)
    {
        await using var f = await Fixture.Create(central);
        await f.Request();
        var challenge = await f.Db.Set<AccountRecoveryChallenge>().SingleAsync();
        var mail = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        var token = f.Crypto.Decrypt(mail.TokenCiphertext!, f.Store.Scope);
        await RecoveryEmailInvalidation.RevokeAsync(f.Db, 1, f.Clock.Now, default);
        await f.Db.SaveChangesAsync();
        Assert.NotNull(challenge.RevokedAtUtc);
        Assert.Null(mail.TokenCiphertext); Assert.Null(mail.RecipientCiphertext);
        await f.Invalid(() => f.Service.VerifyAsync(f.Store, 1, challenge.PublicId, token, default));
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Worker_SendsTestMail_RetainsForResend_ThenCleansExpired(bool central)
    {
        await using var f = await Fixture.Create(central);
        await f.Request();
        var mailbox = new RecoveryTestMailbox();
        var worker = f.Worker(mailbox, new ThrowingTransport());
        await worker.ProcessAsync(f.Store, default);
        var message = Assert.Single(mailbox.Messages);
        Assert.Contains("#token=", message.TextBody); Assert.Contains("verify-recovery-email?requestId=", message.TextBody);
        Assert.Contains(central ? "localhost:3001" : "localhost:3000", message.TextBody);
        if (!central) Assert.Contains("tenantCode=test", message.TextBody);
        var sent = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        Assert.Equal(RecoveryDeliveryStatus.Sent, sent.Status);
        Assert.NotNull(sent.TokenCiphertext); Assert.Null(sent.LeaseId);
        f.Clock.Advance(TimeSpan.FromMinutes(31));
        await worker.ProcessAsync(f.Store, default);
        f.Db.ChangeTracker.Clear();
        Assert.Null((await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).TokenCiphertext);
    }

    [Fact]
    public async Task Worker_RetriesWithLimitsAndSafeFailureCode()
    {
        await using var f = await Fixture.Create(true);
        f.Options.Provider = "Smtp";
        await f.Request();
        var transport = new ThrowingTransport();
        var worker = f.Worker(new(), transport);
        for (var i = 0; i < 3; i++) { await worker.ProcessAsync(f.Store, default); f.Clock.Advance(TimeSpan.FromSeconds(31)); }
        var failed = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        Assert.Equal(3, transport.Calls);
        Assert.Equal(RecoveryDeliveryStatus.Failed, failed.Status);
        Assert.Equal("DeliveryFailed", failed.FailureCode);
        Assert.Null(failed.LeaseId); Assert.Null(failed.LeaseUntilUtc);
    }

    [Fact]
    public async Task Worker_RestartWithSameKey_ResumesPendingEnvelope_AndRedactsTransportError()
    {
        await using var f = await Fixture.Create(true);
        await f.Request();
        var envelope = await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync();
        var token = f.Crypto.Decrypt(envelope.TokenCiphertext!, f.Store.Scope);
        var logs = new CaptureLogger();
        f.Options.Provider = "Smtp";
        var restartedCrypto = new AccountRecoveryCryptography(Options.Create(f.Options));
        Assert.Equal(token, restartedCrypto.Decrypt(envelope.TokenCiphertext!, f.Store.Scope));
        var transport = new ThrowingTransport();
        var restarted = new AccountRecoveryOutboxWorker(f.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(f.Options), restartedCrypto, transport, new(), f.Clock, logs);
        await restarted.ProcessAsync(f.Store, default);
        Assert.Equal(1, transport.Calls);
        var output = string.Join("\n", logs.Messages);
        Assert.Contains("Recovery email delivery failed", output);
        Assert.DoesNotContain(token, output);
        Assert.DoesNotContain("test@example.test", output);
        Assert.DoesNotContain("Never persist", output);
        Assert.Equal(RecoveryDeliveryStatus.Pending, (await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).Status);
        f.Clock.Advance(TimeSpan.FromSeconds(31));
        f.Options.Provider = "Test";
        var mailbox = new RecoveryTestMailbox();
        var retry = new AccountRecoveryOutboxWorker(f.Services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(f.Options), restartedCrypto, transport, mailbox, f.Clock, logs);
        await retry.ProcessAsync(f.Store, default);
        Assert.Contains(token, Assert.Single(mailbox.Messages).TextBody);
        Assert.Equal(RecoveryDeliveryStatus.Sent, (await f.Db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).Status);
    }

    [Fact]
    public void Crypto_IsDomainBound_AndRejectsTampering()
    {
        var crypto = new AccountRecoveryCryptography(Options.Create(Fixture.Settings()));
        var token = crypto.CreateToken(); var id = Guid.NewGuid();
        var hash = AccountRecoveryCryptography.HashToken("tenant:1", 1, RecoveryPurpose.VerifyRecoveryEmail, id, token);
        Assert.NotEqual(hash, AccountRecoveryCryptography.HashToken("tenant:2", 1, RecoveryPurpose.VerifyRecoveryEmail, id, token));
        Assert.NotEqual(hash, AccountRecoveryCryptography.HashToken("tenant:1", 2, RecoveryPurpose.VerifyRecoveryEmail, id, token));
        Assert.NotEqual(hash, AccountRecoveryCryptography.HashToken("tenant:1", 1, RecoveryPurpose.PasswordReset, id, token));
        var encrypted = crypto.Encrypt(token, "tenant:1");
        Assert.Throws<AuthenticationTagMismatchException>(() => crypto.Decrypt(encrypted, "central"));
        encrypted[^1] ^= 1;
        Assert.Throws<AuthenticationTagMismatchException>(() => crypto.Decrypt(encrypted, "tenant:1"));
    }

    [Theory, InlineData("NoNumbersAtAll", false), InlineData("short1", false), InlineData("GoodPassword1", true), InlineData("123456789012", false),
        InlineData("alllowercase123", false), InlineData("ALLUPPERCASE123", false)]
    public void PasswordPolicy_RequiresLengthAsciiDigitUpperAndLower(string password, bool valid)
    {
        if (valid) AccountPasswordPolicy.EnsureValid(password);
        else Assert.Equal("PasswordPolicyViolation", Assert.Throws<RbacOperationException>(() => AccountPasswordPolicy.EnsureValid(password)).Code);
        Assert.Throws<RbacOperationException>(() => AccountPasswordPolicy.EnsureValid(new string('1', 101)));
        AccountPasswordPolicy.EnsureValid("Aa" + new string('1', 98));
    }

    [Fact]
    public void Settings_RejectProductionTestProviderAndUntrustedUrls()
    {
        var options = Fixture.Settings();
        Assert.True(options.IsValid(true)); Assert.False(options.IsValid(false));
        options.Provider = "Smtp";
        options.TenantFrontendBaseUrl = "https://app.example.test"; options.SystemAdminFrontendBaseUrl = "https://admin.example.test";
        Assert.True(options.IsValid(false));
        options.TenantFrontendBaseUrl += "?redirect=evil";
        Assert.False(options.IsValid(false));
    }

    public sealed class ManualClock : TimeProvider
    {
        public DateTime Now { get; private set; } = new(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
        public override DateTimeOffset GetUtcNow() => new(Now);
        public void Advance(TimeSpan value) => Now += value;
    }
    private sealed class ThrowingTransport : IEmailTransport
    {
        public int Calls { get; private set; }
        public Task SendAsync(EmailMessage message, CancellationToken ct) { Calls++; throw new Exception("Never persist this exception containing recipient/token."); }
    }
    private sealed class CaptureLogger : ILogger<AccountRecoveryOutboxWorker>
    {
        public List<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception); // Never attach a transport exception that could contain a secret.
            Messages.Add(formatter(state, exception));
        }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        public required DbContext Db; public required RecoveryStore Store; public required ServiceProvider Services;
        public required RecoveryEmailVerificationService Service; public required AccountRecoveryCryptography Crypto;
        public AccountRecoveryOptions Options = Settings(); public ManualClock Clock = new();
        public static AccountRecoveryOptions Settings() => new() { Enabled = true, Provider = "Test",
            EncryptionKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            TenantFrontendBaseUrl = "http://localhost:3000", SystemAdminFrontendBaseUrl = "http://localhost:3001" };
        public static async Task<Fixture> Create(bool central)
        {
            var services = new ServiceCollection().AddSingleton<IPasswordHasher<TblEmployee>, PasswordHasher<TblEmployee>>()
                .AddSingleton<IPasswordHasher<SystemAdmin>, PasswordHasher<SystemAdmin>>().BuildServiceProvider();
            DbContext db = central ? new CentralDbContext(new DbContextOptionsBuilder<CentralDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options)
                : new DbDtctechContext(new DbContextOptionsBuilder<DbDtctechContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var settings = Settings(); var clock = new ManualClock(); var crypto = new AccountRecoveryCryptography(Microsoft.Extensions.Options.Options.Create(settings));
            if (central)
            {
                var admin = new SystemAdmin { SystemAdminId = 1, Username = "admin", FullName = "Admin", Email = "test@example.test", IsActive = true, SessionVersion = 1, RowVersion = new byte[8] };
                admin.PasswordHash = services.GetRequiredService<IPasswordHasher<SystemAdmin>>().HashPassword(admin, "Password123!"); db.Add(admin);
            }
            else
            {
                var employee = new TblEmployee { EmployeeId = 1, EmployeeAccount = "user", EmployeeEmail = "test@example.test", Status = 1, SessionVersion = 1, RowVersion = new byte[8] };
                employee.EmployeePassword = services.GetRequiredService<IPasswordHasher<TblEmployee>>().HashPassword(employee, "Password123!"); db.Add(employee);
            }
            await db.SaveChangesAsync();
            return new Fixture { Db = db, Store = new(db, central ? "central" : "tenant:1", central ? null : 1, central ? null : "test"),
                Services = services, Options = settings, Clock = clock, Crypto = crypto,
                Service = new(services, Microsoft.Extensions.Options.Options.Create(settings), crypto, clock, new HttpContextAccessor()) };
        }
        public Task<RecoveryVerificationResult> Request() => Service.RequestAsync(Store, 1, "Password123!", default);
        public PasswordRecoveryService PasswordService => new(Services, Microsoft.Extensions.Options.Options.Create(Options), Crypto, Clock, new HttpContextAccessor());
        public async Task VerifyEmail()
        {
            await Request();
            var challenge = await Db.Set<AccountRecoveryChallenge>().SingleAsync();
            var token = Crypto.Decrypt((await Db.Set<AccountRecoveryEmailOutbox>().SingleAsync()).TokenCiphertext!, Store.Scope);
            await Service.VerifyAsync(Store, 1, challenge.PublicId, token, default);
        }
        public async Task Invalid(Func<Task<RecoveryVerificationResult>> action) => Assert.Equal("RecoveryEmailLinkInvalid", (await Assert.ThrowsAsync<RbacOperationException>(action)).Code);
        public AccountRecoveryOutboxWorker Worker(RecoveryTestMailbox mailbox, IEmailTransport transport) => new(Services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(Options), Crypto, transport, mailbox, Clock, NullLogger<AccountRecoveryOutboxWorker>.Instance);
        public async ValueTask DisposeAsync() { await Db.DisposeAsync(); await Services.DisposeAsync(); }
    }
}
