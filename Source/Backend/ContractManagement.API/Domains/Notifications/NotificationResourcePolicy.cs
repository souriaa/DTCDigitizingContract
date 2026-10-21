using ContractManagement.API.Common.Enums;
using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Notifications;

/// <summary>Register one policy per subject type when another module adopts notifications.</summary>
public interface INotificationResourcePolicy
{
    string SubjectType { get; }
    bool IsActionKeyAllowed(string? actionKey);
    Task<bool> CanReadAsync(int subjectId, int employeeId, CancellationToken cancellationToken);
    IQueryable<long> VisibleNotificationIds(AuthenticatedEmployee employee);
}

public sealed class ContractNotificationResourcePolicy : INotificationResourcePolicy
{
    private readonly DbDtctechContext _db;
    private readonly IContractResourceAuthorizationService _authorization;
    public ContractNotificationResourcePolicy(DbDtctechContext db, IContractResourceAuthorizationService authorization)
    {
        _db = db;
        _authorization = authorization;
    }
    public string SubjectType => NotificationSubjects.Contract;
    private static readonly HashSet<string> AllowedActions =
    ["overview", "negotiation", "terms", "customer-access", "approval", "signature", "closing", "handover", "activity"];
    public bool IsActionKeyAllowed(string? actionKey) =>
        actionKey is null || AllowedActions.Contains(actionKey);

    public async Task<bool> CanReadAsync(int subjectId, int employeeId, CancellationToken cancellationToken)
    {
        try
        {
            await _authorization.EnsureCanReadAsync(subjectId, employeeId, cancellationToken);
            return true;
        }
        catch (RbacOperationException ex) when (ex.StatusCode is 403 or 404) { return false; }
    }

    public IQueryable<long> VisibleNotificationIds(AuthenticatedEmployee employee)
    {
        var canReadTenant = employee.Permissions.Contains(RbacPermissions.ContractReadTenant);
        var canReadExecution = employee.Permissions.Contains(RbacPermissions.ContractExecutionRead);
        return _db.TblNotifications.AsNoTracking()
            .Where(n => n.SubjectType == SubjectType
                && _db.TblContracts.Any(contract => contract.ContractId == n.SubjectId
                    && (canReadTenant || contract.EmployeeId == employee.EmployeeId
                        || (canReadExecution && (contract.Status == (byte)ContractStatus.PendingSignature
                            || contract.Status == (byte)ContractStatus.Signed
                            || contract.Status == (byte)ContractStatus.Completed)))))
            .Select(n => n.NotificationId);
    }
}
