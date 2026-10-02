using System.Globalization;
using System.Net;
using ContractManagement.Infrastructure.Branding;
using ContractManagement.API.Domains.CustomerAccess;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Persistence.Recovery;
using Microsoft.AspNetCore.WebUtilities;

namespace ContractManagement.API.Domains.Recovery;

/// <summary>
/// Email template for account recovery workflows:
/// - Verifying recovery email
/// - Resetting password
/// - Notification when password has changed
/// Provides both rich, responsive HTML email layout and plain text fallback.
/// </summary>
public static class RecoveryEmailTemplate
{
    public const string LogoContentId = CustomerOtpEmailTemplate.LogoContentId;

    public static EmailMessage Build(string recipient, RecoveryEmailKind kind, DateTime? expiry, string? link)
    {
        var subject = GetSubject(kind);
        var textBody = BuildPlainTextBody(kind, expiry, link);
        var htmlBody = BuildHtmlBody(kind, expiry, link);

        return new EmailMessage(recipient, subject, textBody, htmlBody, LogoContentId);
    }

    public static string GetSubject(RecoveryEmailKind kind) => kind switch
    {
        RecoveryEmailKind.VerifyRecoveryEmail => "Xác minh email khôi phục",
        RecoveryEmailKind.PasswordReset => "Đặt lại mật khẩu",
        _ => "Mật khẩu đã được thay đổi"
    };

    public static string BuildPlainTextBody(RecoveryEmailKind kind, DateTime? expiry, string? link)
    {
        var (vnTime, utcTime) = FormatTimestamps(expiry);

        return kind switch
        {
            RecoveryEmailKind.VerifyRecoveryEmail => $"""
                XÁC MINH EMAIL KHÔI PHỤC TÀI KHOẢN
                Hệ thống Quản lý & Số hóa Hợp đồng Điện tử

                Xin chào Quý khách,

                Bạn vừa thực hiện yêu cầu xác minh email này làm địa chỉ khôi phục tài khoản trên hệ thống {ApplicationBranding.Name}.
                Vui lòng truy cập liên kết sau để hoàn tất việc xác minh:
                {link}

                Thời hạn hiệu lực: {vnTime} GMT+7 / {utcTime} UTC.

                LƯU Ý AN TOÀN BẢO MẬT:
                - Liên kết xác minh chỉ sử dụng được 01 lần duy nhất và sẽ tự động vô hiệu sau thời gian trên.
                - Tuyệt đối không chia sẻ liên kết này cho bất kỳ ai.
                - Nếu bạn không yêu cầu thao tác này, vui lòng bỏ qua thư này.

                Trân trọng cảm ơn,
                Ban Quản trị Hệ thống Hợp đồng Điện tử
                """,

            RecoveryEmailKind.PasswordReset => $"""
                ĐẶT LẠI MẬT KHẨU TÀI KHOẢN
                Hệ thống Quản lý & Số hóa Hợp đồng Điện tử

                Xin chào Quý khách,

                Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn trên hệ thống {ApplicationBranding.Name}.
                Vui lòng truy cập liên kết sau để tiến hành thiết lập mật khẩu mới:
                {link}

                Thời hạn hiệu lực: {vnTime} GMT+7 / {utcTime} UTC.

                LƯU Ý AN TOÀN BẢO MẬT:
                - Liên kết chỉ sử dụng được 01 lần duy nhất và sẽ tự động vô hiệu sau thời gian trên.
                - Tuyệt đối không cung cấp liên kết này cho bất kỳ ai để bảo vệ an toàn cho tài khoản của bạn.
                - Nếu bạn không yêu cầu thao tác này, vui lòng liên hệ ngay Quản trị viên hệ thống để kiểm tra an toàn tài khoản.

                Trân trọng cảm ơn,
                Ban Quản trị Hệ thống Hợp đồng Điện tử
                """,

            _ => $"""
                THÔNG BÁO THAY ĐỔI MẬT KHẨU
                Hệ thống Quản lý & Số hóa Hợp đồng Điện tử

                Xin chào Quý khách,

                Mật khẩu tài khoản của bạn trên hệ thống {ApplicationBranding.Name} vừa được thay đổi thành công.
                Thời gian thực hiện: {vnTime} GMT+7 / {utcTime} UTC.

                CẢNH BÁO AN TOÀN BẢO MẬT:
                - Nếu bạn chính là người thực hiện đổi mật khẩu, vui lòng bỏ qua thông báo này.
                - NẾU BẠN KHÔNG THỰC HIỆN: Tài khoản của bạn có thể đã bị xâm nhập trái phép. Hãy LIÊN HỆ NGAY LẬP TỨC với Quản trị viên hệ thống để được hỗ trợ khóa và bảo vệ tài khoản kịp thời.

                Trân trọng cảm ơn,
                Ban Quản trị Hệ thống Hợp đồng Điện tử
                """
        };
    }

    public static string BuildHtmlBody(RecoveryEmailKind kind, DateTime? expiry, string? link)
    {
        var subject = GetSubject(kind);
        var (vnTime, utcTime) = FormatTimestamps(expiry);
        var year = DateTime.UtcNow.Year;
        var safeLink = WebUtility.HtmlEncode(link ?? string.Empty);

        var (bannerTitle, contentHtml) = kind switch
        {
            RecoveryEmailKind.VerifyRecoveryEmail => (
                "XÁC MINH EMAIL KHÔI PHỤC",
                $$"""
                <p style="margin: 0 0 20px 0; font-size: 14px; line-height: 1.6; color: #475569;">
                  Bạn vừa thực hiện yêu cầu xác minh email này làm địa chỉ khôi phục tài khoản trên hệ thống {{ApplicationBranding.HtmlName}}. Vui lòng bấm vào nút bên dưới để hoàn tất việc xác nhận:
                </p>

                <!-- Action Box -->
                <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 26px 20px; text-align: center; margin: 24px 0;">
                  <div style="font-size: 12px; font-weight: 700; color: #64748b; letter-spacing: 1.5px; text-transform: uppercase; margin-bottom: 16px;">
                    LIÊN KẾT XÁC MINH EMAIL
                  </div>
                  <table role="presentation" border="0" cellpadding="0" cellspacing="0" align="center" style="margin: 0 auto;">
                    <tr>
                      <td align="center" style="border-radius: 8px; background: linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%);">
                        <a href="{{safeLink}}" target="_blank" style="display: inline-block; padding: 14px 32px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 15px; font-weight: 600; color: #ffffff; text-decoration: none; border-radius: 8px; letter-spacing: 0.3px;">
                          Xác minh email ngay
                        </a>
                      </td>
                    </tr>
                  </table>
                  <div style="font-size: 12px; color: #64748b; margin-top: 18px; line-height: 1.5;">
                    ⏱️ Liên kết có hiệu lực đến:<br>
                    <strong style="color: #1e293b;">{{vnTime}} (GMT+7)</strong>
                    <span style="font-size: 11px; color: #94a3b8;"> / {{utcTime}} UTC</span>
                  </div>
                </div>

                <!-- Fallback URL Link Box -->
                <div style="background-color: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 14px 16px; margin: 20px 0; word-break: break-all;">
                  <div style="font-size: 12px; font-weight: 600; color: #64748b; margin-bottom: 6px;">
                    Nếu nút bấm trên không hoạt động, bạn vui lòng sao chép và dán liên kết sau vào trình duyệt:
                  </div>
                  <a href="{{safeLink}}" target="_blank" style="font-size: 12px; color: #2563eb; text-decoration: underline; line-height: 1.5; word-break: break-all;">
                    {{safeLink}}
                  </a>
                </div>

                <!-- Security Warning Box -->
                <div style="background-color: #fffbeb; border-left: 4px solid #f59e0b; border-radius: 8px; padding: 14px 16px; margin: 24px 0;">
                  <div style="font-size: 13px; font-weight: 700; color: #92400e; margin-bottom: 6px;">
                    Lưu ý an toàn bảo mật:
                  </div>
                  <ul style="margin: 0; padding-left: 18px; font-size: 13px; color: #78350f; line-height: 1.6;">
                    <li style="margin-bottom: 4px;">Liên kết này chỉ có hiệu lực sử dụng <strong>01 lần duy nhất</strong> và sẽ tự động vô hiệu sau khi hết hạn.</li>
                    <li style="margin-bottom: 4px;">Tuyệt đối <strong>không chia sẻ liên kết này</strong> với bất kỳ ai để bảo vệ an toàn cho tài khoản.</li>
                    <li>Nếu bạn <strong>không yêu cầu thao tác này</strong>, vui lòng bỏ qua thư hoặc liên hệ Quản trị viên để kiểm tra an toàn tài khoản.</li>
                  </ul>
                </div>
                """
            ),

            RecoveryEmailKind.PasswordReset => (
                "ĐẶT LẠI MẬT KHẨU",
                $$"""
                <p style="margin: 0 0 20px 0; font-size: 14px; line-height: 1.6; color: #475569;">
                  Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản của bạn trên hệ thống {{ApplicationBranding.HtmlName}}. Vui lòng bấm vào nút bên dưới để tiến hành thiết lập mật khẩu mới:
                </p>

                <!-- Action Box -->
                <div style="background-color: #f8fafc; border: 1px solid #e2e8f0; border-radius: 12px; padding: 26px 20px; text-align: center; margin: 24px 0;">
                  <div style="font-size: 12px; font-weight: 700; color: #64748b; letter-spacing: 1.5px; text-transform: uppercase; margin-bottom: 16px;">
                    LIÊN KẾT ĐẶT LẠI MẬT KHẨU
                  </div>
                  <table role="presentation" border="0" cellpadding="0" cellspacing="0" align="center" style="margin: 0 auto;">
                    <tr>
                      <td align="center" style="border-radius: 8px; background: linear-gradient(135deg, #1e3a8a 0%, #2563eb 100%);">
                        <a href="{{safeLink}}" target="_blank" style="display: inline-block; padding: 14px 32px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; font-size: 15px; font-weight: 600; color: #ffffff; text-decoration: none; border-radius: 8px; letter-spacing: 0.3px;">
                          Đặt lại mật khẩu ngay
                        </a>
                      </td>
                    </tr>
                  </table>
                  <div style="font-size: 12px; color: #64748b; margin-top: 18px; line-height: 1.5;">
                    ⏱️ Liên kết có hiệu lực đến:<br>
                    <strong style="color: #1e293b;">{{vnTime}} (GMT+7)</strong>
                    <span style="font-size: 11px; color: #94a3b8;"> / {{utcTime}} UTC</span>
                  </div>
                </div>

                <!-- Fallback URL Link Box -->
                <div style="background-color: #f8fafc; border: 1px dashed #cbd5e1; border-radius: 8px; padding: 14px 16px; margin: 20px 0; word-break: break-all;">
                  <div style="font-size: 12px; font-weight: 600; color: #64748b; margin-bottom: 6px;">
                    Nếu nút bấm trên không hoạt động, bạn vui lòng sao chép và dán liên kết sau vào trình duyệt:
                  </div>
                  <a href="{{safeLink}}" target="_blank" style="font-size: 12px; color: #2563eb; text-decoration: underline; line-height: 1.5; word-break: break-all;">
                    {{safeLink}}
                  </a>
                </div>

                <!-- Security Warning Box -->
                <div style="background-color: #fffbeb; border-left: 4px solid #f59e0b; border-radius: 8px; padding: 14px 16px; margin: 24px 0;">
                  <div style="font-size: 13px; font-weight: 700; color: #92400e; margin-bottom: 6px;">
                    Lưu ý an toàn bảo mật:
                  </div>
                  <ul style="margin: 0; padding-left: 18px; font-size: 13px; color: #78350f; line-height: 1.6;">
                    <li style="margin-bottom: 4px;">Liên kết này chỉ có hiệu lực sử dụng <strong>01 lần duy nhất</strong> và sẽ tự động vô hiệu sau khi hết hạn.</li>
                    <li style="margin-bottom: 4px;">Tuyệt đối <strong>không cung cấp liên kết này</strong> cho bất kỳ ai để bảo vệ an toàn cho tài khoản của bạn.</li>
                    <li>Nếu bạn <strong>không yêu cầu đặt lại mật khẩu</strong>, vui lòng bỏ qua email này hoặc liên hệ ngay với Quản trị viên để kiểm tra an toàn tài khoản.</li>
                  </ul>
                </div>
                """
            ),

            _ => (
                "THAY ĐỔI MẬT KHẨU THÀNH CÔNG",
                $$"""
                <p style="margin: 0 0 20px 0; font-size: 14px; line-height: 1.6; color: #475569;">
                  Mật khẩu tài khoản của bạn trên hệ thống <strong>{{ApplicationBranding.HtmlName}}</strong> vừa được thay đổi thành công.
                </p>

                <!-- Status Box -->
                <div style="background-color: #f0fdf4; border: 1px solid #bbf7d0; border-radius: 12px; padding: 20px 16px; text-align: center; margin: 24px 0;">
                  <div style="font-size: 13px; font-weight: 700; color: #166534; letter-spacing: 1px; text-transform: uppercase; margin-bottom: 6px;">
                    ✓ CẬP NHẬT MẬT KHẨU THÀNH CÔNG
                  </div>
                  <div style="font-size: 13px; color: #15803d; line-height: 1.5;">
                    Thời gian ghi nhận: <strong>{{vnTime}} (GMT+7)</strong>
                    <span style="font-size: 11px; color: #86efac;"> / {{utcTime}} UTC</span>
                  </div>
                </div>

                <!-- Security Alert Box -->
                <div style="background-color: #fef2f2; border-left: 4px solid #ef4444; border-radius: 8px; padding: 14px 16px; margin: 24px 0;">
                  <div style="font-size: 13px; font-weight: 700; color: #991b1b; margin-bottom: 6px;">
                    Cảnh báo an toàn bảo mật:
                  </div>
                  <ul style="margin: 0; padding-left: 18px; font-size: 13px; color: #7f1d1d; line-height: 1.6;">
                    <li style="margin-bottom: 4px;">Nếu bạn chính là người thực hiện đổi mật khẩu, bạn có thể hoàn toàn an tâm bỏ qua thông báo này.</li>
                    <li><strong>Nếu bạn KHÔNG thực hiện thay đổi này:</strong> Tài khoản của bạn có nguy cơ bị xâm nhập. Vui lòng <strong>liên hệ ngay lập tức với Quản trị viên hệ thống</strong> để kịp thời khóa tài khoản và khôi phục quyền truy cập.</li>
                  </ul>
                </div>
                """
            )
        };

        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <title>{{subject}}</title>
              <style>
                body, table, td, a { -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }
                table, td { mso-table-lspace: 0pt; mso-table-rspace: 0pt; }
                img { -ms-interpolation-mode: bicubic; border: 0; outline: none; text-decoration: none; }
              </style>
            </head>
            <body style="margin: 0; padding: 0; background-color: #f1f5f9; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased;">
              <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="background-color: #f1f5f9; padding: 32px 12px;">
                <tr>
                  <td align="center">
                    <table role="presentation" border="0" cellpadding="0" cellspacing="0" width="100%" style="max-width: 580px; background-color: #ffffff; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.05), 0 2px 4px -2px rgba(0, 0, 0, 0.05); border: 1px solid #e2e8f0;">
                      
                      <!-- Header Banner -->
                      <tr>
                        <td style="background: linear-gradient(135deg, #0f172a 0%, #1e3a8a 100%); padding: 36px 28px; text-align: center;">
                          <img src="cid:{{LogoContentId}}" width="72" height="72" alt="{{ApplicationBranding.HtmlName}}" style="display: inline-block; width: 72px; height: 72px; object-fit: contain; margin-bottom: 14px; border: 0; outline: none; text-decoration: none;">
                          <h1 style="margin: 0; font-size: 20px; font-weight: 700; color: #ffffff; letter-spacing: 0.5px; text-transform: uppercase;">
                            {{bannerTitle}}
                          </h1>
                          <p style="margin: 8px 0 0 0; font-size: 13px; color: #93c5fd; font-weight: 400;">
                            Hệ thống Quản lý &amp; Số hóa Hợp đồng Điện tử
                          </p>
                        </td>
                      </tr>

                      <!-- Main Content -->
                      <tr>
                        <td style="padding: 36px 32px 28px 32px;">
                          <p style="margin: 0 0 16px 0; font-size: 15px; line-height: 1.6; color: #334155;">
                            Xin chào <strong>Quý khách</strong>,
                          </p>
                          {{contentHtml}}
                          <p style="margin: 28px 0 0 0; font-size: 14px; line-height: 1.6; color: #475569;">
                            Trân trọng cảm ơn,<br>
                            <strong style="color: #1e293b;">Ban Quản trị Hệ thống Hợp đồng Điện tử</strong>
                          </p>
                        </td>
                      </tr>

                      <!-- Footer -->
                      <tr>
                        <td style="background-color: #f8fafc; padding: 22px 28px; text-align: center; border-top: 1px solid #e2e8f0;">
                          <p style="margin: 0; font-size: 12px; color: #64748b; line-height: 1.6;">
                            Email này được gửi tự động từ hệ thống <strong>{{ApplicationBranding.HtmlName}}</strong>.<br>
                            Vui lòng không trả lời trực tiếp email này.
                          </p>
                          <p style="margin: 10px 0 0 0; font-size: 11px; color: #94a3b8;">
                            &copy; {{year}} DTC.,Ltd. All rights reserved.
                          </p>
                        </td>
                      </tr>

                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private static (string vnTime, string utcTime) FormatTimestamps(DateTime? expiry)
    {
        var time = expiry ?? DateTime.UtcNow;
        var utcTime = time.Kind == DateTimeKind.Utc ? time : DateTime.SpecifyKind(time, DateTimeKind.Utc);
        var vnTime = utcTime.AddHours(7).ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture);
        var utcTimeStr = utcTime.ToString("HH:mm:ss dd/MM/yyyy", CultureInfo.InvariantCulture);
        return (vnTime, utcTimeStr);
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
