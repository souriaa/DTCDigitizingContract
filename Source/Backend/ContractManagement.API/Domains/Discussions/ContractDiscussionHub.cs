using ContractManagement.API.Common.Security;
using ContractManagement.Domains.Interfaces.Contract;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.API.Domains.Discussions;

public sealed record DiscussionSubscription(int TenantId, int ContractId, int VersionId, DateTime ExpiresAt);

/// <summary>Uses the HTTP scope: SignalR's hub scope has no resolved tenant.</summary>
public sealed class ContractDiscussionAccessService(
    ICurrentTenant tenant, DbDtctechContext db, ICustomerContractAccessService customerAccess)
{
    public async Task<DiscussionSubscription> ForEmployeeAsync(HttpContext http)
    {
        var employee = await EmployeeSessionValidator.ValidateAsync(http, tenant, db, [RbacPermissions.ContractReadOwn]);
        var contractId = int.Parse(http.Request.RouteValues["contractId"]!.ToString()!);
        var versionId = int.Parse(http.Request.RouteValues["versionId"]!.ToString()!);
        var canReadTenant = employee.Permissions.Contains(RbacPermissions.ContractReadTenant, StringComparer.Ordinal);
        var allowed = await db.TblContracts.AsNoTracking().AnyAsync(x => x.ContractId == contractId
            && (canReadTenant || x.EmployeeId == employee.EmployeeId), http.RequestAborted);
        if (!allowed || !await db.TblContractVersions.AsNoTracking().AnyAsync(x =>
                x.ContractId == contractId && x.VersionId == versionId, http.RequestAborted))
            throw new KeyNotFoundException("Không tìm thấy hợp đồng.");
        return new(tenant.GetRequiredTenant().TenantId, contractId, versionId, DateTime.UtcNow.AddMinutes(15));
    }

    public async Task<DiscussionSubscription> ForCustomerAsync(HttpContext http)
    {
        var access = await customerAccess.GetDiscussionAccessAsync(
            http.Request.Cookies["ContractManagement.CustomerAccess"] ?? string.Empty, http.RequestAborted);
        return new(tenant.GetRequiredTenant().TenantId, access.ContractId, access.VersionId,
            access.ExpiresAt < DateTime.UtcNow.AddMinutes(15) ? access.ExpiresAt : DateTime.UtcNow.AddMinutes(15));
    }
}

/// <summary>No client-invokable chat methods; only content-free invalidations.</summary>
public abstract class ContractDiscussionHubBase : Hub
{
    protected abstract Task<DiscussionSubscription> AuthenticateAsync(ContractDiscussionAccessService access, HttpContext http);

    public static string GroupName(int tenantId, int contractId, int versionId) =>
        $"discussion:tenant:{tenantId}:contract:{contractId}:version:{versionId}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        if (http is null) { Context.Abort(); return; }
        try
        {
            var subscription = await AuthenticateAsync(
                http.RequestServices.GetRequiredService<ContractDiscussionAccessService>(), http);
            await Groups.AddToGroupAsync(Context.ConnectionId,
                GroupName(subscription.TenantId, subscription.ContractId, subscription.VersionId));
            var connection = Context;
            _ = CloseAtExpiryAsync(connection, subscription.ExpiresAt);
            await base.OnConnectedAsync();
        }
        catch (Exception error) when (error is RbacOperationException or UnauthorizedAccessException or KeyNotFoundException)
        {
            Context.Abort();
        }
    }

    private static async Task CloseAtExpiryAsync(HubCallerContext connection, DateTime expiresAt)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(0, (expiresAt - DateTime.UtcNow).TotalMilliseconds)),
                connection.ConnectionAborted);
            connection.Abort();
        }
        catch (OperationCanceledException) { }
    }
}

public sealed class EmployeeContractDiscussionHub : ContractDiscussionHubBase
{
    protected override Task<DiscussionSubscription> AuthenticateAsync(ContractDiscussionAccessService access, HttpContext http) =>
        access.ForEmployeeAsync(http);
}

public sealed class CustomerContractDiscussionHub : ContractDiscussionHubBase
{
    protected override Task<DiscussionSubscription> AuthenticateAsync(ContractDiscussionAccessService access, HttpContext http) =>
        access.ForCustomerAsync(http);
}
