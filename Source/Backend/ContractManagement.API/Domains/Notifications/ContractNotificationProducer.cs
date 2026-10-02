using ContractManagement.API.Common.Enums;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Notifications;

/// <summary>Contract event catalog and recipient resolution. Caller owns the transaction.</summary>
public sealed class ContractNotificationProducer
{
    private readonly DbDtctechContext _db;
    private readonly INotificationPublisher _publisher;
    public ContractNotificationProducer(DbDtctechContext db, INotificationPublisher publisher)
    {
        _db = db;
        _publisher = publisher;
    }

    public Task<bool> StageOwnerAsync(int contractId, int ownerEmployeeId, int? actorEmployeeId,
        string eventKey, string eventCode, string title, string actionKey,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default) =>
        StageAsync(contractId, actorEmployeeId, eventKey, eventCode, title, actionKey,
            [ownerEmployeeId], occurredAtUtc, cancellationToken);

    public async Task<bool> StageCurrentOwnerAsync(int contractId, int? actorEmployeeId,
        string eventKey, string eventCode, string title, string actionKey,
        DateTime occurredAtUtc, CancellationToken cancellationToken = default)
    {
        var ownerId = await _db.TblContracts.AsNoTracking()
            .Where(x => x.ContractId == contractId)
            .Select(x => x.EmployeeId).SingleAsync(cancellationToken);
        return await StageOwnerAsync(contractId, ownerId, actorEmployeeId,
            eventKey, eventCode, title, actionKey, occurredAtUtc, cancellationToken);
    }

    public Task<bool> StageAsync(int contractId, int? actorEmployeeId,
        string eventKey, string eventCode, string title, string actionKey,
        IEnumerable<int> recipients, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default) =>
        _publisher.StageAsync(new NotificationEvent(eventKey, "Contract", eventCode,
            NotificationSubjects.Contract, contractId, actorEmployeeId,
            title, null, occurredAtUtc, actionKey), recipients, cancellationToken);

    public async Task<bool> StageApprovalRequestedAsync(int contractId, int requestId,
        int submitterEmployeeId, int? workflowId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var managers = await GetEligibleManagersAsync(workflowId, cancellationToken);
        return await StageAsync(contractId, submitterEmployeeId,
            $"contract:{contractId}:approval-request:{requestId}:submitted",
            "ApprovalSubmitted", "Hợp đồng đang chờ bạn phê duyệt", "approval",
            managers, occurredAtUtc, cancellationToken);
    }

    public async Task<bool> StageApprovalWithdrawnAsync(int contractId, int requestId,
        int submitterEmployeeId, DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var submittedEventKey = $"contract:{contractId}:approval-request:{requestId}:submitted";
        var managers = await _db.TblNotificationRecipients.AsNoTracking()
            .Where(x => _db.TblNotifications.Any(notification =>
                notification.NotificationId == x.NotificationId
                && notification.EventKey == submittedEventKey))
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
        return await StageAsync(contractId, submitterEmployeeId,
            $"contract:{contractId}:approval-request:{requestId}:withdrawn",
            "ApprovalWithdrawn", "Yêu cầu duyệt hợp đồng đã được rút", "approval",
            managers, occurredAtUtc, cancellationToken);
    }

    private async Task<IReadOnlyList<int>> GetEligibleManagersAsync(int? workflowId,
        CancellationToken cancellationToken)
    {
        int? assignedApprover = null;
        if (workflowId.HasValue)
        {
            assignedApprover = await _db.TblApprovalWorkflows.AsNoTracking()
                .Where(x => x.WorkflowId == workflowId && x.IsActive
                    && x.ObjectType == "Contract" && x.StepNo == 1)
                .Select(x => x.ApproverEmployeeId)
                .SingleOrDefaultAsync(cancellationToken);
        }
        // An unassigned workflow is actionable by every active manager in the
        // approval inbox; this is not a tenant-wide broadcast.
        return await _db.TblEmployees.AsNoTracking()
            .Where(x => x.Status == 1 && x.EmployeeType == (byte)EmployeeType.Manager
                && (!assignedApprover.HasValue || x.EmployeeId == assignedApprover.Value))
            .Select(x => x.EmployeeId).ToListAsync(cancellationToken);
    }
}
