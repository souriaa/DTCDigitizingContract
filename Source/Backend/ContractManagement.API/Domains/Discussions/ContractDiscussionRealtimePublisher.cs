using ContractManagement.Infrastructure.MultiTenancy.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ContractManagement.API.Domains.Discussions;

public interface IContractDiscussionRealtimePublisher
{
    Task PublishAsync(int contractId, int versionId);
}

public sealed class ContractDiscussionRealtimePublisher(
    ICurrentTenant tenant,
    IHubContext<EmployeeContractDiscussionHub> employees,
    IHubContext<CustomerContractDiscussionHub> customers,
    ILogger<ContractDiscussionRealtimePublisher> logger) : IContractDiscussionRealtimePublisher
{
    public async Task PublishAsync(int contractId, int versionId)
    {
        // Called only after commit. A socket failure must never roll back a saved comment.
        try
        {
            var group = ContractDiscussionHubBase.GroupName(tenant.GetRequiredTenant().TenantId, contractId, versionId);
            await Task.WhenAll(
                employees.Clients.Group(group).SendCoreAsync("DiscussionChanged", []),
                customers.Clients.Group(group).SendCoreAsync("DiscussionChanged", []));
        }
        catch (Exception error)
        {
            logger.LogWarning(error, "Could not deliver discussion invalidation for contract {ContractId} version {VersionId}.",
                contractId, versionId);
        }
    }
}
