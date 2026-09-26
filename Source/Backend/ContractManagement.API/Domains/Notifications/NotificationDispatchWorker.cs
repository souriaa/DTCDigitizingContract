using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Notifications;

public sealed class NotificationDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<NotificationDispatchWorker> _logger;

    public NotificationDispatchWorker(IServiceScopeFactory scopes, IHubContext<NotificationHub> hub,
        ILogger<NotificationDispatchWorker> logger)
    {
        _scopes = scopes;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var centralScope = _scopes.CreateAsyncScope();
                var central = centralScope.ServiceProvider.GetRequiredService<CentralDbContext>();
                var tenants = await central.Tenants.AsNoTracking()
                    .Where(x => x.Status == TenantStatus.Active)
                    .Select(x => new ResolvedTenant(x.TenantId, x.TenantCode, x.TenantName,
                        x.TenantDatabase.Mode, x.TenantDatabase.ConnectionString))
                    .ToListAsync(stoppingToken);
                foreach (var tenant in tenants)
                {
                    try { await ProcessTenantAsync(tenant, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Notification dispatch tenant {TenantId} failed: {ErrorCode}",
                            tenant.TenantId, ex.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("Notification dispatch scan failed: {ErrorCode}", ex.GetType().Name);
            }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    private async Task ProcessTenantAsync(ResolvedTenant tenant, CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Set(tenant);
        var db = scope.ServiceProvider.GetRequiredService<DbDtctechContext>();
        var now = DateTime.UtcNow;
        var ids = await db.TblNotificationDispatchOutbox.AsNoTracking()
            .Where(x => (x.Status == "Pending" && x.NextAttemptAtUtc <= now)
                || (x.Status == "Leased" && x.LeaseUntilUtc <= now))
            .OrderBy(x => x.NextAttemptAtUtc).Select(x => x.OutboxId)
            .Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            var leaseId = Guid.NewGuid();
            var claimed = await db.TblNotificationDispatchOutbox
                .Where(x => x.OutboxId == id
                    && ((x.Status == "Pending" && x.NextAttemptAtUtc <= now)
                        || (x.Status == "Leased" && x.LeaseUntilUtc <= now)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, "Leased")
                    .SetProperty(x => x.LeaseId, leaseId)
                    .SetProperty(x => x.LeaseUntilUtc, now.AddMinutes(1))
                    .SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1), ct);
            if (claimed == 0) continue;
            var row = await db.TblNotificationDispatchOutbox.AsNoTracking()
                .Where(x => x.OutboxId == id && x.LeaseId == leaseId)
                .Select(x => new { x.NotificationId, x.AttemptCount })
                .SingleAsync(ct);
            try
            {
                var recipients = await db.TblNotificationRecipients.AsNoTracking()
                    .Where(x => x.NotificationId == row.NotificationId
                        && db.TblEmployees.Any(employee => employee.EmployeeId == x.EmployeeId
                            && employee.Status == 1))
                    .Select(x => x.EmployeeId).ToListAsync(ct);
                foreach (var recipient in recipients)
                    await _hub.Clients.Group(NotificationHub.GroupName(tenant.TenantId, recipient))
                        .SendCoreAsync("NotificationsChanged", [], ct);
                await db.TblNotificationDispatchOutbox
                    .Where(x => x.OutboxId == id && x.LeaseId == leaseId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, "Dispatched")
                        .SetProperty(x => x.DispatchedAtUtc, DateTime.UtcNow)
                        .SetProperty(x => x.LeaseId, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var failedAt = DateTime.UtcNow;
                var retry = row.AttemptCount < 8;
                var delay = TimeSpan.FromSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(row.AttemptCount, 6))));
                var errorCode = ex.GetType().Name;
                if (errorCode.Length > 80) errorCode = errorCode[..80];
                await db.TblNotificationDispatchOutbox
                    .Where(x => x.OutboxId == id && x.LeaseId == leaseId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(x => x.Status, retry ? "Pending" : "Failed")
                        .SetProperty(x => x.NextAttemptAtUtc, failedAt.Add(delay))
                        .SetProperty(x => x.LastErrorCode, errorCode)
                        .SetProperty(x => x.LeaseId, (Guid?)null)
                        .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null), ct);
                _logger.LogWarning("Notification dispatch failed. Tenant {TenantId}, outbox {OutboxId}, error {ErrorCode}",
                    tenant.TenantId, id, errorCode);
            }
        }
    }
}
