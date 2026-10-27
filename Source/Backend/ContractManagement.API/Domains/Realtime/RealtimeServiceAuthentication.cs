using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Realtime;

public sealed class RealtimeServiceAuthentication(IOptions<NodeRealtimeOptions> options)
{
    private readonly ConcurrentDictionary<string, long> _nonces = new();
    public static string Signature(string secret, string method, string path, string body, string timestamp, string nonce) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes($"{timestamp}\n{nonce}\n{method}\n{path}\n{body}")));

    public void Sign(HttpRequestMessage request, string path, string body)
    {
        // Calls are restricted to loopback. Honor the existing HTTPS-terminated proxy setup
        // so self-probes do not redirect to a different port and drop their session context.
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        var nonce = Guid.NewGuid().ToString();
        request.Headers.Add("x-realtime-timestamp", timestamp);
        request.Headers.Add("x-realtime-nonce", nonce);
        request.Headers.Add("x-realtime-signature", Signature(options.Value.ServiceSecret, request.Method.Method, path, body, timestamp, nonce));
    }

    public async Task<bool> ValidateAsync(HttpRequest request)
    {
        if (!options.Value.Enabled) return false;
        var timestamp = request.Headers["x-realtime-timestamp"].ToString();
        var nonce = request.Headers["x-realtime-nonce"].ToString();
        var signature = request.Headers["x-realtime-signature"].ToString();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        foreach (var (key, expires) in _nonces) if (expires <= now) _nonces.TryRemove(key, out _);
        if (timestamp.Length != 13 || !long.TryParse(timestamp, out var issued)
            || issued < now - 30000 || issued > now + 30000 || !Guid.TryParseExact(nonce, "D", out _)
            || signature.Length != 64 || _nonces.Count >= 10000) return false;
        request.EnableBuffering();
        if (request.ContentLength > 16384) return false;
        request.Body.Position = 0;
        // Bounded read also covers chunked requests without Content-Length.
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var buffer = new char[16385];
        var count = await reader.ReadBlockAsync(buffer.AsMemory(), request.HttpContext.RequestAborted);
        request.Body.Position = 0;
        if (count > 16384) return false;
        var body = new string(buffer, 0, count);
        var expected = Signature(options.Value.ServiceSecret, request.Method,
            request.Path + request.QueryString, body, timestamp, nonce);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(signature.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected)))
            return false;
        return _nonces.TryAdd(nonce, now + 60000);
    }
}
