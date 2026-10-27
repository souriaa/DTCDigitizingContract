using ContractManagement.API.Domains.Notifications;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Central;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Realtime;

/// <summary>Node leases/retries are independent of the existing SignalR status/lease columns.</summary>
public sealed class NodeNotificationDispatchWorker(IServiceScopeFactory scopes, NodeRealtimePublisher node,
    ILogger<NodeNotificationDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!node.Enabled) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var central = scope.ServiceProvider.GetRequiredService<CentralDbContext>();
                var tenants = await central.Tenants.AsNoTracking().Where(x => x.Status == TenantStatus.Active)
                    .Select(x => new ResolvedTenant(x.TenantId, x.TenantCode, x.TenantName,
                        x.TenantDatabase.Mode, x.TenantDatabase.ConnectionString)).ToListAsync(stoppingToken);
                foreach (var tenant in tenants)
                {
                    try { await ProcessTenantAsync(tenant, stoppingToken); }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                    catch (Exception error) { logger.LogWarning("Node dispatch tenant {TenantId} failed: {ErrorCode}", tenant.TenantId, error.GetType().Name); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Node dispatch scan failed: {ErrorCode}", error.GetType().Name); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    internal async Task ProcessTenantAsync(ResolvedTenant tenant, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Set(tenant);
        var db = scope.ServiceProvider.GetRequiredService<DbDtctechContext>();
        var now = DateTime.UtcNow;
        var ids = await db.TblNotificationDispatchOutbox.AsNoTracking()
            .Where(x => x.NodeStatus == "Pending" && x.NodeNextAttemptAtUtc <= now
                || x.NodeStatus == "Leased" && x.NodeLeaseUntilUtc <= now)
            .OrderBy(x => x.NodeNextAttemptAtUtc).Select(x => x.OutboxId).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            var lease = Guid.NewGuid();
            var claimed = await db.TblNotificationDispatchOutbox.Where(x => x.OutboxId == id
                && (x.NodeStatus == "Pending" && x.NodeNextAttemptAtUtc <= now
                    || x.NodeStatus == "Leased" && x.NodeLeaseUntilUtc <= now))
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NodeStatus, "Leased").SetProperty(x => x.NodeLeaseId, lease)
                    .SetProperty(x => x.NodeLeaseUntilUtc, now.AddMinutes(1))
                    .SetProperty(x => x.NodeAttemptCount, x => x.NodeAttemptCount + 1), ct);
            if (claimed == 0) continue;
            var row = await db.TblNotificationDispatchOutbox.AsNoTracking()
                .SingleAsync(x => x.OutboxId == id && x.NodeLeaseId == lease, ct);
            try
            {
                var recipients = await db.TblNotificationRecipients.AsNoTracking()
                    .Where(x => x.NotificationId == row.NotificationId
                        && db.TblEmployees.Any(e => e.EmployeeId == x.EmployeeId && e.Status == 1))
                    .Select(x => x.EmployeeId).ToListAsync(ct);
                foreach (var recipient in recipients)
                    await node.PublishAsync(new($"notification:{tenant.TenantId}:{id}:{recipient}", "NotificationsChanged",
                        NotificationHub.GroupName(tenant.TenantId, recipient)), ct);
                await db.TblNotificationDispatchOutbox.Where(x => x.OutboxId == id && x.NodeLeaseId == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.NodeStatus, "Dispatched")
                        .SetProperty(x => x.NodeDispatchedAtUtc, DateTime.UtcNow).SetProperty(x => x.NodeLeaseId, (Guid?)null)
                        .SetProperty(x => x.NodeLeaseUntilUtc, (DateTime?)null).SetProperty(x => x.NodeLastErrorCode, (string?)null), ct);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                var next = DateTime.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(row.NodeAttemptCount, 6))));
                var code = error.GetType().Name;
                if (code.Length > 80) code = code[..80];
                await db.TblNotificationDispatchOutbox.Where(x => x.OutboxId == id && x.NodeLeaseId == lease)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.NodeStatus, row.NodeAttemptCount < 8 ? "Pending" : "Failed")
                        .SetProperty(x => x.NodeNextAttemptAtUtc, next).SetProperty(x => x.NodeLastErrorCode, code)
                        .SetProperty(x => x.NodeLeaseId, (Guid?)null).SetProperty(x => x.NodeLeaseUntilUtc, (DateTime?)null), ct);
                logger.LogWarning("Node notification dispatch failed: tenant {TenantId}, outbox {OutboxId}, error {ErrorCode}", tenant.TenantId, id, code);
            }
        }
    }
}
