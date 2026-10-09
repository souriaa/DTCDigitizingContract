namespace ContractManagement.API.Domains.Recovery;
public sealed class AccountRecoveryOptions
{
    public const string SectionName = "AccountRecovery";
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "Smtp";
    public string? EncryptionKey { get; set; }
    public string? TenantFrontendBaseUrl { get; set; }
    public string? SystemAdminFrontendBaseUrl { get; set; }
    public int PasswordResetMinutes { get; set; } = 15;
    public int EmailVerificationMinutes { get; set; } = 30;
    public int ResendSeconds { get; set; } = 60;
    public int RequestsPerChallenge { get; set; } = 3;
    public int RequestsPerIpPerHour { get; set; } = 10;
    public int MaxDeliveryAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 30;
    public bool IsValid(bool development)
    {
        if (!Enabled) return true;
        try
        {
            return Convert.FromBase64String(EncryptionKey ?? "").Length == 32
                && ValidUrl(TenantFrontendBaseUrl, development) && ValidUrl(SystemAdminFrontendBaseUrl, development)
                && (Provider == "Smtp" || (development && Provider == "Test"))
                && PasswordResetMinutes is >= 1 and <= 60 && EmailVerificationMinutes is >= 1 and <= 60
                && ResendSeconds is >= 30 and <= 600 && RequestsPerChallenge is >= 1 and <= 10
                && RequestsPerIpPerHour is >= 1 and <= 100 && MaxDeliveryAttempts is >= 1 and <= 5
                && RetryDelaySeconds is >= 1 and <= 300;
        }
        catch (FormatException) { return false; }
    }
    private static bool ValidUrl(string? value, bool development) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && string.IsNullOrEmpty(uri.UserInfo)
        && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment)
        && (uri.Scheme == "https" || (development && uri.Scheme == "http" && uri.IsLoopback));
}
