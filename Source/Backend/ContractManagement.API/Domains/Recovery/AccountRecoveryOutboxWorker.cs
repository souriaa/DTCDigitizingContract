using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ContractManagement.Infrastructure.Security;

namespace ContractManagement.API.Domains.Recovery;

public sealed class AccountRecoveryOutboxWorker(
    IServiceScopeFactory scopes, IOptions<AccountRecoveryOptions> options,
    AccountRecoveryCryptography crypto, IEmailTransport transport, RecoveryTestMailbox mailbox,
    TimeProvider clock, ILogger<AccountRecoveryOutboxWorker> logger, RecoveryTelemetry? telemetry = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!options.Value.Enabled) return;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var central = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
                await ProcessSafely(new(central, "central"), ct);
                var tenants = await central.Tenants.AsNoTracking().Where(x => x.Status == TenantStatus.Active)
                    .Select(x => new ResolvedTenant(x.TenantId, x.TenantCode, x.TenantName, x.TenantDatabase.Mode, x.TenantDatabase.ConnectionString)).ToListAsync(ct);
                foreach (var tenant in tenants)
                {
                    try
                    {
                        await using var tenantScope = scopes.CreateAsyncScope();
                        tenantScope.ServiceProvider.GetRequiredService<ICurrentTenant>().Set(tenant);
                        var db = tenantScope.ServiceProvider.GetRequiredService<DbDtctechContext>();
                        await ProcessSafely(new(db, $"tenant:{tenant.TenantId}", tenant.TenantId, tenant.TenantCode), ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                    catch (Exception) { telemetry?.Failure(); logger.LogError("Cannot open recovery store for tenant {TenantId}; continuing other tenants.", tenant.TenantId); }
                }
                telemetry?.Cycle();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception) { telemetry?.Failure(); logger.LogError("Recovery outbox cycle failed. Check database/schema availability; no email secrets are logged."); }
            try { await Task.Delay(TimeSpan.FromSeconds(10), ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
        }
    }
    private async Task ProcessSafely(RecoveryStore store, CancellationToken ct)
    {
        try { await ProcessAsync(store, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { telemetry?.Failure(); logger.LogError("Recovery outbox failed for scope {Scope}. Verify migrations and configuration.", store.Scope); }
    }
    public async Task ProcessAsync(RecoveryStore store, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var db = store.Db;
        // Erase envelopes after expiry/use/revocation, including already sent mail.
        var closed = await db.Set<AccountRecoveryEmailOutbox>().Where(x => x.RecipientCiphertext != null &&
            ((x.ExpiresAtUtc != null && x.ExpiresAtUtc <= now) || (x.RecoveryChallengeId != null &&
            db.Set<AccountRecoveryChallenge>().Any(c => c.RecoveryChallengeId == x.RecoveryChallengeId &&
                (c.UsedAtUtc != null || c.RevokedAtUtc != null))))).Take(100).ToListAsync(ct);
        foreach (var row in closed) RecoveryEmailInvalidation.Clear(row, now, "ChallengeClosed");
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        var ids = await db.Set<AccountRecoveryEmailOutbox>().AsNoTracking().Where(x =>
            (x.Status == RecoveryDeliveryStatus.Pending && x.NextAttemptAtUtc <= now) ||
            (x.Status == RecoveryDeliveryStatus.Leased && x.LeaseUntilUtc <= now))
            .OrderBy(x => x.OutboxId).Select(x => x.OutboxId).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var row = await db.Set<AccountRecoveryEmailOutbox>().SingleAsync(x => x.OutboxId == id, ct);
            now = clock.GetUtcNow().UtcDateTime;
            if (row.Status == RecoveryDeliveryStatus.Pending && row.NextAttemptAtUtc > now ||
                row.Status == RecoveryDeliveryStatus.Leased && row.LeaseUntilUtc > now ||
                row.Status is RecoveryDeliveryStatus.Sent or RecoveryDeliveryStatus.Failed) continue;
            row.Status = RecoveryDeliveryStatus.Leased; row.LeaseId = Guid.NewGuid(); row.LeaseUntilUtc = now.AddMinutes(2);
            row.AttemptCount++;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { continue; }
            var account = await store.LoadAccountAsync(row.AccountId, ct);
            var challenge = row.RecoveryChallengeId is null ? null : await db.Set<AccountRecoveryChallenge>()
                .SingleOrDefaultAsync(x => x.RecoveryChallengeId == row.RecoveryChallengeId, ct);
            if (row.AttemptCount > options.Value.MaxDeliveryAttempts || account is null || !account.Active
                || (row.TemplateKind == RecoveryEmailKind.VerifyRecoveryEmail && !account.Eligible)
                || row.RecipientCiphertext is null || row.ExpiresAtUtc <= now
                || (row.TemplateKind != RecoveryEmailKind.PasswordChanged && (challenge is null || challenge.AccountId != row.AccountId
                    || challenge.UsedAtUtc is not null || challenge.RevokedAtUtc is not null || challenge.ExpiresAtUtc <= now || !account.Matches(challenge))))
            {
                RecoveryEmailInvalidation.Clear(row, now, "ChallengeInvalid");
            }
            else if ((row.TemplateKind == RecoveryEmailKind.VerifyRecoveryEmail && challenge?.Purpose != RecoveryPurpose.VerifyRecoveryEmail)
                || (row.TemplateKind == RecoveryEmailKind.PasswordReset && (challenge?.Purpose != RecoveryPurpose.PasswordReset || account.VerifiedAt is null))
                || (row.TemplateKind == RecoveryEmailKind.PasswordChanged && challenge is not null))
            {
                RecoveryEmailInvalidation.Clear(row, now, "MessageInvalid");
            }
            else
            {
                try
                {
                    var recipient = crypto.Decrypt(row.RecipientCiphertext, store.Scope);
                    if (!string.Equals(recipient.Trim(), account.Email?.Trim(), StringComparison.Ordinal))
                        throw new InvalidOperationException("Recovery recipient changed.");
                    var link = challenge is null ? null : RecoveryEmailTemplate.Link(options.Value, store, challenge,
                        crypto.Decrypt(row.TokenCiphertext ?? throw new InvalidOperationException("Missing envelope."), store.Scope));
                    var mail = RecoveryEmailTemplate.Build(recipient, row.TemplateKind, row.ExpiresAtUtc, link);
                    if (options.Value.Provider == "Test") mailbox.Add(mail);
                    else await transport.SendAsync(mail, ct);
                    telemetry?.Delivery(true);
                    row.Status = RecoveryDeliveryStatus.Sent; row.SentAtUtc = clock.GetUtcNow().UtcDateTime;
                    if (challenge is null) { row.RecipientCiphertext = null; row.TokenCiphertext = null; }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    telemetry?.Delivery(false);
                    row.FailureCode = "DeliveryFailed";
                    if (row.AttemptCount >= options.Value.MaxDeliveryAttempts || row.ExpiresAtUtc <= clock.GetUtcNow().UtcDateTime)
                    { row.Status = RecoveryDeliveryStatus.Failed; row.FailedAtUtc = clock.GetUtcNow().UtcDateTime; }
                    else { row.Status = RecoveryDeliveryStatus.Pending; row.NextAttemptAtUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(options.Value.RetryDelaySeconds); }
                    if (row.TemplateKind == RecoveryEmailKind.PasswordChanged && row.Status == RecoveryDeliveryStatus.Failed)
                    { row.RecipientCiphertext = null; row.TokenCiphertext = null; }
                    store.Audit(row.AccountId, AuthorizationAuditActionTypes.PasswordRecoveryDeliveryFailed, clock.GetUtcNow().UtcDateTime, failure: "DeliveryFailed", actor: false);
                    logger.LogWarning("Recovery email delivery failed for scope {Scope}, outbox {OutboxId}, attempt {Attempt}.", store.Scope, id, row.AttemptCount);
                }
            }
            row.LeaseId = null; row.LeaseUntilUtc = null;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { /* Account/challenge cleanup or another worker won; do not overwrite. */ }
        }
    }
}
