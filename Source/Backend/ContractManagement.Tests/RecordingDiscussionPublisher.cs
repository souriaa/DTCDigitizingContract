using ContractManagement.API.Domains.Discussions;
using ContractManagement.Infrastructure.Persistence.Application;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests;

internal sealed class RecordingDiscussionPublisher(DbDtctechContext context) : IContractDiscussionRealtimePublisher
{
    public List<(int ContractId, int VersionId)> Publications { get; } = [];

    public async Task PublishAsync(int contractId, int versionId)
    {
        // The notification must observe persisted comments, not pending tracked inserts.
        Assert.DoesNotContain(context.ChangeTracker.Entries(), entry =>
            entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted);
        Assert.True(await context.TblContractNegotiationComments.AsNoTracking()
            .AnyAsync(x => x.ContractId == contractId && x.VersionId == versionId));
        Publications.Add((contractId, versionId));
    }
}
