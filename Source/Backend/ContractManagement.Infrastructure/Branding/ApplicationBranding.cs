using System.Net;
using System.Text.Json;

namespace ContractManagement.Infrastructure.Branding;

/// <summary>Public application name embedded from the shared build-time configuration.</summary>
public static class ApplicationBranding
{
    public static string Name { get; } = ReadName();
    public static string HtmlName { get; } = WebUtility.HtmlEncode(Name);

    private static string ReadName()
    {
        using var stream = typeof(ApplicationBranding).Assembly
            .GetManifestResourceStream("ContractManagement.ApplicationBranding.json")
            ?? throw new InvalidOperationException("Shared application branding resource is missing.");
        using var document = JsonDocument.Parse(stream);
        var name = document.RootElement.GetProperty("name").GetString();
        if (string.IsNullOrWhiteSpace(name) || name != name.Trim() || name.Contains('\r') || name.Contains('\n'))
            throw new InvalidOperationException("Shared application name must be nonblank, trimmed and single-line.");
        return name;
    }
}
