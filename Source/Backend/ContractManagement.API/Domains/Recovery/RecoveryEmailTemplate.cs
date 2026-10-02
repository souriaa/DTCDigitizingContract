using System.Net;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.AspNetCore.WebUtilities;

namespace ContractManagement.API.Domains.Recovery;

public static class RecoveryEmailTemplate
{
    public static EmailMessage Build(string recipient, RecoveryEmailKind kind, DateTime? expiry, string? link)
    {
        var title = kind switch {
            RecoveryEmailKind.VerifyRecoveryEmail => "Xác minh email khôi phục",
            RecoveryEmailKind.PasswordReset => "Đặt lại mật khẩu",
            _ => "Mật khẩu đã được thay đổi"
        };
        var text = kind == RecoveryEmailKind.PasswordChanged
            ? "Mật khẩu tài khoản của bạn vừa được thay đổi. Nếu không phải bạn thực hiện, hãy liên hệ người quản trị ngay."
            : $"{title}: {link}\nLiên kết hết hạn lúc {expiry:HH:mm dd/MM/yyyy} UTC. Nếu không phải bạn yêu cầu, hãy bỏ qua thư này.";
        var action = kind == RecoveryEmailKind.PasswordChanged ? "" :
            $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{title}</a></p><p>Hết hạn: {expiry:HH:mm dd/MM/yyyy} UTC.</p>";
        return new(recipient, title, text,
            $"<html><body><img alt=\"DTC\" width=\"120\" src=\"cid:{CustomerOtpEmailTemplate.LogoContentId}\"><h2>{title}</h2>{action}<p>{WebUtility.HtmlEncode(kind == RecoveryEmailKind.PasswordChanged ? text : "Nếu không phải bạn yêu cầu, hãy bỏ qua thư này. Không chia sẻ liên kết cho người khác.")}</p></body></html>",
            CustomerOtpEmailTemplate.LogoContentId);
    }
    public static string Link(AccountRecoveryOptions options, RecoveryStore store, AccountRecoveryChallenge challenge, string token)
    {
        var root = store.TenantId is null ? options.SystemAdminFrontendBaseUrl! : options.TenantFrontendBaseUrl!;
        var path = challenge.Purpose == RecoveryPurpose.VerifyRecoveryEmail ? "verify-recovery-email" : "reset-password";
        var url = QueryHelpers.AddQueryString($"{root.TrimEnd('/')}/{path}", "requestId", challenge.PublicId.ToString("D"));
        if (store.TenantCode is not null) url = QueryHelpers.AddQueryString(url, "tenantCode", store.TenantCode);
        return $"{url}#token={Uri.EscapeDataString(token)}";
    }
}
