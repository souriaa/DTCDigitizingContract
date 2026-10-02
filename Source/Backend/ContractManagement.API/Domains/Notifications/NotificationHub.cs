using ContractManagement.API.Common.Security;
using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.AspNetCore.SignalR;

namespace ContractManagement.API.Domains.Notifications;

/// <summary>No client methods: the socket only receives an empty invalidation signal.</summary>
public sealed class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        if (http is null)
        {
            Context.Abort();
            return;
        }
        try
        {
            // SignalR activates hubs in its own scope. TenantResolutionMiddleware
            // populated the HTTP request scope, not the hub activation scope.
            var tenant = http.RequestServices.GetRequiredService<ICurrentTenant>();
            var db = http.RequestServices.GetRequiredService<DbDtctechContext>();
            var employee = await EmployeeSessionValidator.ValidateAsync(
                http, tenant, db, Array.Empty<string>());
            await Groups.AddToGroupAsync(Context.ConnectionId,
                GroupName(tenant.GetRequiredTenant().TenantId, employee.EmployeeId));

            // Force a fresh handshake regularly; revoked long-lived sessions only receive
            // content-free invalidations until then, and REST always rechecks authorization.
            var connection = Context;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(15), connection.ConnectionAborted);
                    connection.Abort();
                }
                catch (OperationCanceledException) { }
            });
            await base.OnConnectedAsync();
        }
        catch (RbacOperationException)
        {
            Context.Abort();
        }
    }

    public static string GroupName(int tenantId, int employeeId) =>
        $"tenant:{tenantId}:employee:{employeeId}";
}
