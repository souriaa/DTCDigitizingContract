using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ContractManagement.API.Domains.Realtime;

public sealed class NodeRealtimePublisher(IHttpClientFactory clients, IOptions<NodeRealtimeOptions> options,
    RealtimeServiceAuthentication authentication)
{
    public bool Enabled => options.Value.Enabled;
    public async Task PublishAsync(NodeRealtimeEvent message, CancellationToken ct = default)
    {
        if (!Enabled) return;
        const string path = "/internal/realtime/publish";
        var body = JsonSerializer.Serialize(message, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.BaseUrl.TrimEnd('/') + path);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        authentication.Sign(request, path, body);
        using var response = await clients.CreateClient("RealtimeInternal").SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
