using ContractManagement.API.Domains.Email;
namespace ContractManagement.API.Domains.CustomerAccess;
public sealed class SmtpCustomerOtpDeliveryProvider(IEmailTransport transport) : ICustomerOtpDeliveryProvider
{
    public Task DeliverAsync(CustomerOtpDeliveryMessage message, CancellationToken cancellationToken)
    {
        if (!SmtpEmailOptions.IsEmailAddress(message.EmailAddress))
            throw new InvalidOperationException("Customer OTP recipient email is missing or invalid.");
        if (message.ExpiresAt is not { } expiresAt || expiresAt <= DateTime.UtcNow)
            throw new InvalidOperationException("Customer OTP delivery has expired. Request a new code.");
        return transport.SendAsync(new EmailMessage(message.EmailAddress!, CustomerOtpEmailTemplate.Subject,
            CustomerOtpEmailTemplate.BuildPlainTextBody(message.Otp, expiresAt),
            CustomerOtpEmailTemplate.BuildBody(message.Otp, expiresAt), CustomerOtpEmailTemplate.LogoContentId), cancellationToken);
    }
}
