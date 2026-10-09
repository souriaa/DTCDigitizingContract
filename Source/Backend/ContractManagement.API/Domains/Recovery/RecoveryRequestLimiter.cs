using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Recovery;

// Single-instance limiter. Hash keys, bound memory, expire entries; no account names/IPs in metrics.
public sealed class RecoveryRequestLimiter(TimeProvider clock, IOptions<AccountRecoveryOptions> options)
{
    private sealed class Window { public DateTimeOffset Ends; public DateTimeOffset Last; public int Count; }
    private readonly Dictionary<string, Window> windows = new();
    private readonly object gate = new();
    public int CheckIp(string scope, string ip, bool request) => Take($"ip|{scope}|{ip}|{request}",
        request ? options.Value.RequestsPerIpPerHour : 60, request ? TimeSpan.FromHours(1) : TimeSpan.FromMinutes(15), TimeSpan.Zero, out _);
    public bool CheckAccount(string scope, string name) => Take($"account|{scope.ToUpperInvariant()}|{name.Trim().ToUpperInvariant()}",
        options.Value.RequestsPerChallenge, TimeSpan.FromMinutes(options.Value.PasswordResetMinutes),
        TimeSpan.FromSeconds(options.Value.ResendSeconds), out var allowed) == 0 && allowed;
    private int Take(string value, int max, TimeSpan ttl, TimeSpan cooldown, out bool allowed)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
        lock (gate)
        {
            var now = clock.GetUtcNow();
            foreach (var expired in windows.Where(x => x.Value.Ends <= now).Select(x => x.Key).ToArray()) windows.Remove(expired);
            if (!windows.TryGetValue(key, out var row))
            {
                if (windows.Count >= 10_000) { allowed = false; return 60; }
                row = new Window { Ends = now.Add(ttl), Last = DateTimeOffset.MinValue }; windows.Add(key, row);
            }
            if (row.Count >= max) { allowed = false; return Math.Max(1, (int)Math.Ceiling((row.Ends - now).TotalSeconds)); }
            if (row.Last.Add(cooldown) > now) { allowed = false; return Math.Max(1, (int)Math.Ceiling((row.Last.Add(cooldown) - now).TotalSeconds)); }
            row.Count++; row.Last = now; allowed = true; return 0;
        }
    }
}
