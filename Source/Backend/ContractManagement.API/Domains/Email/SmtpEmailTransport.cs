using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using ContractManagement.Infrastructure.Branding;
using Microsoft.Extensions.Options;
namespace ContractManagement.API.Domains.Email;
public sealed class SmtpEmailTransport(IOptions<SmtpEmailOptions> options) : IEmailTransport
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        if (!smtp.IsConfigured() || !SmtpEmailOptions.IsEmailAddress(message.Recipient))
            throw new InvalidOperationException("Email SMTP configuration or recipient is invalid.");
        var logoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Email", "logo_light.png");
        var logo = message.LogoContentId is not null && File.Exists(logoPath)
            ? await File.ReadAllBytesAsync(logoPath, cancellationToken) : null;
        using var mail = BuildMailMessage(message, smtp, logo);
        using var client = new SmtpClient(smtp.Host, smtp.Port)
        {
            EnableSsl = true, UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtp.Username, smtp.AppPassword), DeliveryMethod = SmtpDeliveryMethod.Network
        };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(smtp.TimeoutSeconds));
        try { await client.SendMailAsync(mail, timeout.Token); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new TimeoutException("SMTP delivery timed out."); }
    }
    internal static MailMessage BuildMailMessage(EmailMessage message, SmtpEmailOptions smtp, byte[]? logo)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(smtp.FromAddress!, ApplicationBranding.Name, Encoding.UTF8), Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8, Body = message.TextBody, BodyEncoding = Encoding.UTF8, IsBodyHtml = false
        };
        mail.To.Add(new MailAddress(message.Recipient));
        var html = AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html);
        if (logo is { Length: > 0 } && message.LogoContentId is not null)
        {
            var resource = new LinkedResource(new MemoryStream(logo, writable: false), MediaTypeNames.Image.Png)
            { ContentId = message.LogoContentId, ContentLink = new Uri($"cid:{message.LogoContentId}"), TransferEncoding = TransferEncoding.Base64 };
            resource.ContentType.Name = null; html.LinkedResources.Add(resource);
        }
        mail.AlternateViews.Add(html); return mail;
    }
}
