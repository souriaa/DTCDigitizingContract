namespace ContractManagement.API.Domains.Realtime;

public sealed class NodeRealtimeOptions
{
    public const string Section = "Realtime:NodeWebSocket";
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "http://127.0.0.1:4000";
    public string ApiBaseUrl { get; set; } = "http://127.0.0.1:5005";
    public string ServiceSecret { get; set; } = "";

    public bool IsValid() => !Enabled || ServiceSecret.Length >= 32 && Loopback(BaseUrl) && Loopback(ApiBaseUrl);
    private static bool Loopback(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.IsLoopback && uri.Scheme is "http" or "https" && string.IsNullOrEmpty(uri.UserInfo)
        && uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
}

public sealed record NodeSubscription(string LeaseId, string Room, string ActorKey, DateTime ExpiresAtUtc);
public sealed record NodeRealtimeEvent(string EventId, string EventName, string Room);
