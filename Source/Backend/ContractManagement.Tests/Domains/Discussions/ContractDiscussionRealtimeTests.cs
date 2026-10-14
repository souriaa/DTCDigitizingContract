using ContractManagement.API.Domains.Discussions;
using ContractManagement.Infrastructure.MultiTenancy.Enums;
using ContractManagement.Infrastructure.MultiTenancy.Models;
using ContractManagement.Infrastructure.MultiTenancy.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContractManagement.Tests.Domains.Discussions;

public sealed class ContractDiscussionRealtimeTests
{
    [Fact]
    public async Task Publisher_SendsOnlyInvalidationToBothScopedGroups()
    {
        var employees = new RecordingClients();
        var customers = new RecordingClients();
        await CreatePublisher(employees, customers).PublishAsync(20, 30);
        foreach (var clients in new[] { employees, customers })
        {
            Assert.Equal("discussion:tenant:10:contract:20:version:30", Assert.Single(clients.GroupsJoined));
            Assert.Equal("DiscussionChanged", clients.Method);
            Assert.Empty(clients.Arguments!);
        }
    }

    [Fact]
    public async Task Publisher_TransportFailureDoesNotFailSavedComment()
    {
        var employees = new RecordingClients { FailDelivery = true };
        var customers = new RecordingClients();
        await CreatePublisher(employees, customers).PublishAsync(20, 30);
        Assert.Equal("DiscussionChanged", customers.Method);
    }

    [Fact]
    public void GroupNames_IsolateEveryTenantContractAndVersion()
    {
        var group = ContractDiscussionHubBase.GroupName(10, 20, 30);
        Assert.NotEqual(group, ContractDiscussionHubBase.GroupName(11, 20, 30));
        Assert.NotEqual(group, ContractDiscussionHubBase.GroupName(10, 21, 30));
        Assert.NotEqual(group, ContractDiscussionHubBase.GroupName(10, 20, 31));
    }

    private static ContractDiscussionRealtimePublisher CreatePublisher(RecordingClients employees, RecordingClients customers)
    {
        var tenant = new CurrentTenant();
        tenant.Set(new ResolvedTenant(10, "tenant", "Tenant", TenantDatabaseMode.Dedicated, "InMemory"));
        return new(tenant, new HubContext<EmployeeContractDiscussionHub>(employees),
            new HubContext<CustomerContractDiscussionHub>(customers), NullLogger<ContractDiscussionRealtimePublisher>.Instance);
    }

    private sealed class HubContext<T>(IHubClients clients) : IHubContext<T> where T : Hub
    {
        public IHubClients Clients => clients;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class RecordingClients : IHubClients, IClientProxy
    {
        public List<string> GroupsJoined { get; } = [];
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }
        public bool FailDelivery { get; init; }
        public IClientProxy Group(string groupName) { GroupsJoined.Add(groupName); return this; }
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return FailDelivery ? Task.FromException(new IOException("Socket disconnected")) : Task.CompletedTask;
        }
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();
        public IClientProxy User(string userId) => throw new NotSupportedException();
        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }
}
