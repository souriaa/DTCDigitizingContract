using System.Net;
using System.Text.Json;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Branding;
using ContractManagement.Infrastructure.Persistence.Recovery;

namespace ContractManagement.Tests.Branding;

public sealed class ApplicationBrandingTests
{
    [Fact]
    public void Name_ComesFromEmbeddedSharedFile()
    {
        using var stream = typeof(ApplicationBranding).Assembly
            .GetManifestResourceStream("ContractManagement.ApplicationBranding.json");
        Assert.NotNull(stream);
        using var document = JsonDocument.Parse(stream);
        Assert.Equal(document.RootElement.GetProperty("name").GetString(), ApplicationBranding.Name);
        Assert.False(string.IsNullOrWhiteSpace(ApplicationBranding.Name));
        Assert.Equal(WebUtility.HtmlEncode(ApplicationBranding.Name), ApplicationBranding.HtmlName);
    }

    [Theory]
    [InlineData(RecoveryEmailKind.VerifyRecoveryEmail)]
    [InlineData(RecoveryEmailKind.PasswordReset)]
    [InlineData(RecoveryEmailKind.PasswordChanged)]
    public void RecoveryEmails_UseSharedName(RecoveryEmailKind kind)
    {
        var mail = RecoveryEmailTemplate.Build("recipient@example.test", kind,
            new DateTime(2026, 10, 5, 15, 30, 0, DateTimeKind.Utc), "https://example.test/reset-password");
        Assert.Contains(ApplicationBranding.Name, mail.TextBody);
        Assert.Contains($"alt=\"{ApplicationBranding.HtmlName}\"", mail.HtmlBody);
        Assert.Contains($"<strong>{ApplicationBranding.HtmlName}</strong>", mail.HtmlBody);
        Assert.DoesNotContain("Digitizing Contract", mail.TextBody);
        Assert.DoesNotContain("Digitizing Contract", mail.HtmlBody);
    }

    [Fact]
    public void CustomerOtpAndSmtpSender_UseSharedName()
    {
        var expiry = new DateTime(2026, 10, 5, 15, 30, 0, DateTimeKind.Utc);
        var message = new EmailMessage("recipient@example.test", CustomerOtpEmailTemplate.Subject,
            CustomerOtpEmailTemplate.BuildPlainTextBody("012345", expiry),
            CustomerOtpEmailTemplate.BuildBody("012345", expiry));
        Assert.Contains(ApplicationBranding.Name, message.TextBody);
        Assert.Contains($"alt=\"{ApplicationBranding.HtmlName}\"", message.HtmlBody);
        Assert.Contains($"<strong>{ApplicationBranding.HtmlName}</strong>", message.HtmlBody);
        using var mail = SmtpEmailTransport.BuildMailMessage(message,
            new SmtpEmailOptions { FromAddress = "sender@example.test" }, null);
        Assert.Equal(ApplicationBranding.Name, mail.From!.DisplayName);
    }
}
