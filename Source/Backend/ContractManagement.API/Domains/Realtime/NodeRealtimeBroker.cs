using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Realtime;

/// <summary>
/// One-use tickets and bounded leases for the existing single-instance, memory-session API.
/// Browser credentials stay inside .NET. Node receives only an opaque lease and server-defined room.
/// Restart drops all tickets/leases and requires fresh authentication; never use multiple API instances here.
/// </summary>
public sealed class NodeRealtimeBroker(IHttpClientFactory clients, IOptions<NodeRealtimeOptions> options,
    RealtimeServiceAuthentication authentication)
{
    private sealed record Grant(string Cookie, string ProbePath, string Origin, NodeSubscription Scope, DateTime TicketExpiresAt);
    private readonly Dictionary<string, Grant> _tickets = [];
    private readonly Dictionary<string, Grant> _leases = [];
    private readonly object _sync = new();
    private static string Token() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public string Issue(string cookie, string probePath, string origin, NodeSubscription scope)
    {
        var ticket = Token();
        lock (_sync)
        {
            Prune();
            if (_tickets.Count + _leases.Count >= 10000) throw new InvalidOperationException("Realtime capacity reached.");
            _tickets.Add(Hash(ticket), new(cookie, probePath, origin, scope, DateTime.UtcNow.AddSeconds(30)));
        }
        return ticket;
    }

    public async Task<NodeSubscription?> ConsumeAsync(string ticket, string origin, CancellationToken ct)
    {
        if (ticket.Length != 43) return null;
        Grant? grant;
        lock (_sync)
        {
            Prune();
            if (!_tickets.Remove(Hash(ticket), out grant) || grant.Origin != origin) return null;
        }
        var lease = Token();
        var result = await ProbeAsync(grant, lease, ct);
        if (result is null) return null;
        lock (_sync) _leases.Add(lease, grant with { Scope = result });
        return result;
    }

    public async Task<NodeSubscription?> ValidateAsync(string lease, CancellationToken ct)
    {
        Grant? grant;
        lock (_sync) { Prune(); _leases.TryGetValue(lease, out grant); }
        if (grant is null) return null;
        var result = await ProbeAsync(grant, lease, ct);
        if (result is null) Release(lease);
        return result;
    }

    public void Release(string lease) { lock (_sync) _leases.Remove(lease); }

    private async Task<NodeSubscription?> ProbeAsync(Grant grant, string lease, CancellationToken ct)
    {
        if (grant.Scope.ExpiresAtUtc <= DateTime.UtcNow) return null;
        // A NEW HTTP session load is essential: a copied ISession snapshot would survive logout.
        // Target/path are fixed by the server, not a client-provided URL; cookies never leave this host.
        using var request = new HttpRequestMessage(HttpMethod.Get, options.Value.ApiBaseUrl.TrimEnd('/') + grant.ProbePath);
        request.Headers.Add("Cookie", grant.Cookie);
        authentication.Sign(request, grant.ProbePath, "");
        using var response = await clients.CreateClient("RealtimeInternal").SendAsync(request, ct);
        if (!response.IsSuccessStatusCode) return null;
        var live = await response.Content.ReadFromJsonAsync<NodeSubscription>(ct);
        if (live is null || live.Room != grant.Scope.Room || live.ActorKey != grant.Scope.ActorKey) return null;
        // SQL datetime2 expiries have UTC ticks but Kind=Unspecified. Always put Z on the wire;
        // otherwise Node running in UTC+7 would parse that expiry as local time.
        var expires = live.ExpiresAtUtc < grant.Scope.ExpiresAtUtc ? live.ExpiresAtUtc : grant.Scope.ExpiresAtUtc;
        return live with { LeaseId = lease, ExpiresAtUtc = DateTime.SpecifyKind(expires, DateTimeKind.Utc) };
    }

    private void Prune()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _tickets.Where(x => x.Value.TicketExpiresAt <= now || x.Value.Scope.ExpiresAtUtc <= now).Select(x => x.Key).ToArray()) _tickets.Remove(key);
        foreach (var key in _leases.Where(x => x.Value.Scope.ExpiresAtUtc <= now).Select(x => x.Key).ToArray()) _leases.Remove(key);
    }
}
