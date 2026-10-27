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
    ILogger<ContractDiscussionRealtimePublisher> logger,
    ContractManagement.API.Domains.Realtime.NodeRealtimePublisher? node = null) : IContractDiscussionRealtimePublisher
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
        // Independent best-effort adapter after commit, matching discussion SignalR semantics.
        // Notifications use their durable per-transport outbox; discussions still resync through REST.
        if (node?.Enabled == true)
        {
            try
            {
                await node.PublishAsync(new(Guid.NewGuid().ToString(), "DiscussionChanged",
                    ContractDiscussionHubBase.GroupName(tenant.GetRequiredTenant().TenantId, contractId, versionId)));
            }
            catch (Exception error)
            {
                logger.LogWarning("Node discussion delivery failed for contract {ContractId}: {ErrorCode}", contractId, error.GetType().Name);
            }
        }
    }
}
