using ContractManagement.API.Common.Enums;
using ContractManagement.Infrastructure.Persistence.Application;
using ContractManagement.Infrastructure.Persistence.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Domains.Services.Contract;

internal static class ContractApprovalInboxQuery
{
    public static async Task<IQueryable<TblContractApprovalRequest>> CreateAsync(
        DbDtctechContext dbContext,
        int managerEmployeeId,
        CancellationToken cancellationToken)
    {
        var isAdministrativeReviewer = await dbContext.TblEmployees.AsNoTracking()
            .AnyAsync(x => x.EmployeeId == managerEmployeeId && x.Status == 1
                && x.EmployeeType == (byte)EmployeeType.AdminOfficer, cancellationToken);
        if (isAdministrativeReviewer)
            return dbContext.TblContractApprovalRequests.AsNoTracking().Where(
                x => x.Status == (byte)ApprovalRequestStatus.PendingAdministrativeReview
                    && x.SubmittedByEmployeeId != managerEmployeeId);
        var isManager = await dbContext.TblEmployees.AsNoTracking()
            .AnyAsync(x => x.EmployeeId == managerEmployeeId && x.Status == 1
                && x.EmployeeType == (byte)EmployeeType.Manager, cancellationToken);
        if (!isManager)
            return dbContext.TblContractApprovalRequests.AsNoTracking().Where(_ => false);
        var allowedWorkflowIds = await dbContext.TblApprovalWorkflows
            .AsNoTracking()
            .Where(workflow =>
                workflow.IsActive
                && workflow.ObjectType == "Contract"
                && workflow.StepNo == 1
                && (!workflow.ApproverEmployeeId.HasValue
                    || workflow.ApproverEmployeeId.Value == managerEmployeeId))
            .Select(workflow => workflow.WorkflowId)
            .ToListAsync(cancellationToken);

        return dbContext.TblContractApprovalRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == (byte)ApprovalRequestStatus.Pending
                && request.SubmittedByEmployeeId != managerEmployeeId
                && (!request.WorkflowId.HasValue
                    || allowedWorkflowIds.Contains(request.WorkflowId.Value)));
    }
}
