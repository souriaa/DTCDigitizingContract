using System.Diagnostics.Metrics;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Recovery;

// Export with the application's Meter listener/OpenTelemetry; no tenant/account/token labels.
public sealed class RecoveryTelemetry : IDisposable
{
    private readonly Meter meter = new("ContractManagement.AccountRecovery", "1.0");
    private readonly IOptions<AccountRecoveryOptions> options;
    private readonly Counter<long> requests;
    private readonly Counter<long> deliveries;
    private readonly Histogram<double> duration;
    private long cycleTicks;
    private long failures;
    public RecoveryTelemetry(IOptions<AccountRecoveryOptions> options)
    {
        this.options = options;
        requests = meter.CreateCounter<long>("recovery.requests");
        deliveries = meter.CreateCounter<long>("recovery.deliveries");
        duration = meter.CreateHistogram<double>("recovery.request.duration", "ms");
    }
    public void Record(string scope, string operation, string outcome, double ms)
    {
        requests.Add(1,
            new KeyValuePair<string, object?>("scope", scope), new("operation", operation), new("outcome", outcome));
        duration.Record(ms,
            new KeyValuePair<string, object?>("scope", scope), new("operation", operation));
    }
    public void Delivery(bool sent) => deliveries.Add(1,
        new KeyValuePair<string, object?>("outcome", sent ? "sent" : "failed"));
    public void Cycle() => Interlocked.Exchange(ref cycleTicks, DateTime.UtcNow.Ticks);
    public void Failure() => Interlocked.Increment(ref failures);
    public object Status() => new { enabled = options.Value.Enabled,
        workerRecentlyActive = cycleTicks != 0 && DateTime.UtcNow - new DateTime(Interlocked.Read(ref cycleTicks), DateTimeKind.Utc) < TimeSpan.FromMinutes(2),
        cycleFailures = Interlocked.Read(ref failures), probe = "Worker heartbeat only; not an SMTP delivery check." };
    public void Dispose() => meter.Dispose();
}
