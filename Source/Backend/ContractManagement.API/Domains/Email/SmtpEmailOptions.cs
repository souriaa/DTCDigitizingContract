using System.Net.Mail;
namespace ContractManagement.API.Domains.Email;
public sealed class SmtpEmailOptions
{
    public const string SectionName = "Email:Smtp";
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? AppPassword { get; set; }
    public string? FromAddress { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public bool IsConfigured() => !string.IsNullOrWhiteSpace(Host) && Port == 587
        && IsEmailAddress(Username) && IsEmailAddress(FromAddress)
        && !string.IsNullOrWhiteSpace(AppPassword) && TimeoutSeconds is >= 1 and <= 45;
    public static bool IsEmailAddress(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.Contains('\r') && !value.Contains('\n') && MailAddress.TryCreate(value, out var address)
        && string.Equals(address.Address, value, StringComparison.Ordinal);
}
public sealed record EmailMessage(string Recipient, string Subject, string TextBody, string HtmlBody, string? LogoContentId = null);
public interface IEmailTransport { Task SendAsync(EmailMessage message, CancellationToken cancellationToken); }
