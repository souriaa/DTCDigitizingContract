using ContractManagement.API.Common.Security;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Notifications;

public sealed record NotificationEvent(
    string EventKey, string ModuleCode, string EventCode, string SubjectType,
    int SubjectId, int? ActorEmployeeId, string Title, string? Body,
    DateTime OccurredAtUtc, string? ActionKey);

public interface INotificationPublisher
{
    Task<bool> StageAsync(NotificationEvent notification, IEnumerable<int> recipientEmployeeIds,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Adds all three rows to the caller's DbContext. The business transaction owner commits;
/// this service never saves or sends a SignalR message.
/// </summary>
public sealed class NotificationPublisher : INotificationPublisher
{
    private readonly DbDtctechContext _db;
    private readonly IReadOnlyList<INotificationResourcePolicy> _policies;

    private readonly bool _nodeEnabled;
    public NotificationPublisher(DbDtctechContext db, IEnumerable<INotificationResourcePolicy> policies,
        Microsoft.Extensions.Options.IOptions<ContractManagement.API.Domains.Realtime.NodeRealtimeOptions>? nodeOptions = null)
    {
        _db = db;
        _policies = policies.ToList();
        _nodeEnabled = nodeOptions?.Value.Enabled == true;
    }

    public async Task<bool> StageAsync(NotificationEvent notification,
        IEnumerable<int> recipientEmployeeIds, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notification.EventKey);
        if (notification.EventKey.Length > 200 || notification.Title.Length is 0 or > 300
            || notification.Body?.Length > 1000 || notification.SubjectId <= 0)
            throw new ArgumentException("Notification metadata is invalid.", nameof(notification));

        // No compatibility path for old notification rows. A repeated business event is idempotent.
        if (_db.ChangeTracker.Entries<TblNotification>().Any(e => e.Entity.EventKey == notification.EventKey)
            || await _db.TblNotifications.AnyAsync(x => x.EventKey == notification.EventKey, cancellationToken))
            return false;

        var policy = _policies.SingleOrDefault(x => x.SubjectType == notification.SubjectType)
            ?? throw new ArgumentException("Notification subject policy is not registered.", nameof(notification));
        if (!policy.IsActionKeyAllowed(notification.ActionKey))
            throw new ArgumentException("Notification action is not registered.", nameof(notification));
        var requested = recipientEmployeeIds.Distinct()
            .Where(id => id > 0 && id != notification.ActorEmployeeId).ToArray();
        var active = await _db.TblEmployees.AsNoTracking()
            .Where(x => requested.Contains(x.EmployeeId) && x.Status == 1)
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        var recipients = new List<int>();
        foreach (var employeeId in active)
        {
            if (await policy.CanReadAsync(notification.SubjectId, employeeId, cancellationToken))
                recipients.Add(employeeId);
        }
        if (recipients.Count == 0) return false;

        var now = DateTime.UtcNow;
        var entity = new TblNotification
        {
            EventKey = notification.EventKey, ModuleCode = notification.ModuleCode,
            EventCode = notification.EventCode, SubjectType = notification.SubjectType,
            SubjectId = notification.SubjectId, ActorEmployeeId = notification.ActorEmployeeId,
            Title = notification.Title, Body = notification.Body,
            OccurredAtUtc = notification.OccurredAtUtc, CreatedAtUtc = now,
            ActionKey = notification.ActionKey,
            Recipients = recipients.Select(id => new TblNotificationRecipient
                { EmployeeId = id, CreatedAtUtc = now }).ToList(),
            DispatchOutbox = new TblNotificationDispatchOutbox
                { Status = "Pending", NextAttemptAtUtc = now,
                    NodeStatus = _nodeEnabled ? "Pending" : "Disabled",
                    NodeNextAttemptAtUtc = _nodeEnabled ? now : null }
        };
        _db.TblNotifications.Add(entity);
        return true;
    }
}

public static class NotificationSubjects
{
    public const string Contract = "Contract";
}
