using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Recovery;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContractManagement.Tests.Domains.Recovery;

public sealed class RecoveryEmailTemplateTests
{
    [Fact]
    public void Build_VerifyRecoveryEmail_GeneratesRichHtmlAndPlainTextFallback()
    {
        var recipient = "user@example.test";
        var expiry = new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc);
        var link = "https://admin.example.com/verify-recovery-email?requestId=test#token=xyz";

        var mail = RecoveryEmailTemplate.Build(recipient, RecoveryEmailKind.VerifyRecoveryEmail, expiry, link);

        Assert.Equal(recipient, mail.Recipient);
        Assert.Equal("Xác minh email khôi phục", mail.Subject);
        Assert.Equal(CustomerOtpEmailTemplate.LogoContentId, mail.LogoContentId);

        // Plain text assertions
        Assert.Contains(link, mail.TextBody);
        Assert.Contains("19:00:00 05/10/2026 GMT+7", mail.TextBody);
        Assert.Contains("12:00:00 05/10/2026 UTC", mail.TextBody);
        Assert.Contains("XÁC MINH EMAIL KHÔI PHỤC TÀI KHOẢN", mail.TextBody);
        Assert.Contains("LƯU Ý AN TOÀN BẢO MẬT", mail.TextBody);

        // HTML assertions
        Assert.Contains($"cid:{CustomerOtpEmailTemplate.LogoContentId}", mail.HtmlBody);
        Assert.Contains("XÁC MINH EMAIL KHÔI PHỤC", mail.HtmlBody);
        Assert.Contains("Xác minh email ngay", mail.HtmlBody);
        Assert.Contains(link, mail.HtmlBody);
        Assert.Contains("19:00:00 05/10/2026 (GMT+7)", mail.HtmlBody);
        Assert.Contains("Hệ thống Quản lý &amp; Số hóa Hợp đồng Điện tử", mail.HtmlBody);
        Assert.Contains("Lưu ý an toàn bảo mật:", mail.HtmlBody);
    }

    [Fact]
    public void Build_PasswordReset_GeneratesRichHtmlAndPlainTextFallback()
    {
        var recipient = "admin@example.test";
        var expiry = new DateTime(2026, 10, 5, 15, 30, 0, DateTimeKind.Utc);
        var link = "https://app.example.com/reset-password?requestId=123#token=abc";

        var mail = RecoveryEmailTemplate.Build(recipient, RecoveryEmailKind.PasswordReset, expiry, link);

        Assert.Equal(recipient, mail.Recipient);
        Assert.Equal("Đặt lại mật khẩu", mail.Subject);
        Assert.Equal(CustomerOtpEmailTemplate.LogoContentId, mail.LogoContentId);

        // Plain text assertions
        Assert.Contains(link, mail.TextBody);
        Assert.Contains("22:30:00 05/10/2026 GMT+7", mail.TextBody);
        Assert.Contains("ĐẶT LẠI MẬT KHẨU TÀI KHOẢN", mail.TextBody);

        // HTML assertions
        Assert.Contains($"cid:{CustomerOtpEmailTemplate.LogoContentId}", mail.HtmlBody);
        Assert.Contains("ĐẶT LẠI MẬT KHẨU", mail.HtmlBody);
        Assert.Contains("Đặt lại mật khẩu ngay", mail.HtmlBody);
        Assert.Contains(link, mail.HtmlBody);
        Assert.Contains("22:30:00 05/10/2026 (GMT+7)", mail.HtmlBody);
        Assert.Contains("Lưu ý an toàn bảo mật:", mail.HtmlBody);
    }

    [Fact]
    public void Build_PasswordChanged_GeneratesSecurityNotice()
    {
        var recipient = "user@example.test";

        var mail = RecoveryEmailTemplate.Build(recipient, RecoveryEmailKind.PasswordChanged, null, null);

        Assert.Equal(recipient, mail.Recipient);
        Assert.Equal("Mật khẩu đã được thay đổi", mail.Subject);
        Assert.Equal(CustomerOtpEmailTemplate.LogoContentId, mail.LogoContentId);

        // Plain text assertions
        Assert.Contains("THÔNG BÁO THAY ĐỔI MẬT KHẨU", mail.TextBody);
        Assert.Contains("CẢNH BÁO AN TOÀN BẢO MẬT:", mail.TextBody);
        Assert.Contains("LIÊN HỆ NGAY LẬP TỨC", mail.TextBody);

        // HTML assertions
        Assert.Contains($"cid:{CustomerOtpEmailTemplate.LogoContentId}", mail.HtmlBody);
        Assert.Contains("THAY ĐỔI MẬT KHẨU THÀNH CÔNG", mail.HtmlBody);
        Assert.Contains("CẬP NHẬT MẬT KHẨU THÀNH CÔNG", mail.HtmlBody);
        Assert.Contains("Cảnh báo an toàn bảo mật:", mail.HtmlBody);
        Assert.Contains("liên hệ ngay lập tức với Quản trị viên hệ thống", mail.HtmlBody);
    }

    [Fact]
    public void Link_BuildsExpectedUrl()
    {
        var options = new AccountRecoveryOptions
        {
            SystemAdminFrontendBaseUrl = "https://admin.example.com",
            TenantFrontendBaseUrl = "https://tenant.example.com"
        };
        var challengeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var challenge = new AccountRecoveryChallenge
        {
            PublicId = challengeId,
            Purpose = RecoveryPurpose.PasswordReset
        };

        var store = new RecoveryStore(null!, "tenant:1", 1, "acme");
        var url = RecoveryEmailTemplate.Link(options, store, challenge, "my-secret-token");

        Assert.Equal(
            "https://tenant.example.com/reset-password?requestId=11111111-2222-3333-4444-555555555555&tenantCode=acme#token=my-secret-token",
            url);
    }
}
