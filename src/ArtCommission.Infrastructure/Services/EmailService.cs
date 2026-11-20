using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Services;

public class EmailService : IEmailService
{
    private static readonly HttpClient ResendHttpClient = new() { Timeout = TimeSpan.FromSeconds(10) };
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Logo được lưu ở domain frontend (host tĩnh /weblogo.png) vì email client tải ảnh qua URL tuyệt đối, không dùng được đường dẫn tương đối hay asset cục bộ.</summary>
    private const string LogoUrl = "https://dil-frontend.onrender.com/weblogo.png";

    /// <summary>
    /// Khung email nhất quán cho mọi loại thông báo (mã OTP, cảnh báo bảo mật, ...): header có logo,
    /// card kính tối theo đúng bảng màu Digital Etherealism của dự án, và footer liên hệ hỗ trợ.
    /// </summary>
    private static string BuildBrandedEmailHtml(string preheader, string bodyHtml, string footerNote)
    {
        return $@"
<!DOCTYPE html>
<html>
<head><meta charset=""utf-8"" /></head>
<body style=""margin: 0; padding: 24px 16px; background-color: #0B0E17; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #E4E1E5;"">
  <span style=""display: none; max-height: 0; overflow: hidden; opacity: 0;"">{WebUtility.HtmlEncode(preheader)}</span>
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width: 480px; margin: 0 auto; background-color: #121622; border-radius: 16px; border: 1px solid rgba(255, 255, 255, 0.08); overflow: hidden;"">
    <tr>
      <td style=""padding: 24px 28px; border-bottom: 1px solid rgba(255, 255, 255, 0.08); text-align: center;"">
        <img src=""{LogoUrl}"" alt=""Dillustration"" height=""32"" style=""height: 32px; display: inline-block; vertical-align: middle;"" />
      </td>
    </tr>
    <tr>
      <td style=""padding: 28px 28px 8px 28px; font-size: 14px; line-height: 1.7;"">
        {bodyHtml}
      </td>
    </tr>
    <tr>
      <td style=""padding: 20px 28px 24px 28px; font-size: 11.5px; color: #78767A;"">
        {WebUtility.HtmlEncode(footerNote)}
      </td>
    </tr>
    <tr>
      <td style=""padding: 16px 28px; background-color: #0E111A; text-align: center; font-size: 11px; color: #64748B;"">
        DILLUSTRATION • Nền tảng Commission &amp; Đấu giá tranh số • Hỗ trợ: support@dillustration.art
      </td>
    </tr>
  </table>
</body>
</html>";
    }

    private static string BuildCodeBlockHtml(string code) =>
        $@"<div style=""margin: 16px 0; text-align: center; background-color: #171D2D; border: 1px solid rgba(167, 172, 217, 0.25); border-radius: 12px; padding: 18px 12px;"">
            <span style=""font-size: 30px; font-weight: bold; letter-spacing: 8px; color: #A7ACD9; font-family: 'Courier New', monospace;"">{WebUtility.HtmlEncode(code)}</span>
          </div>";

    private const string DefaultExpiryNote = "Lưu ý: nếu không phải bạn yêu cầu, hãy bỏ qua email này — tài khoản của bạn vẫn an toàn và không có gì thay đổi.";

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
            <p style=""margin: 0 0 4px 0; color: #C7C5CF;"">Đây là mã xác minh để kích hoạt email cho tài khoản Dillustration của bạn:</p>
            {BuildCodeBlockHtml(verificationToken)}
            <p style=""margin: 12px 0 0 0; color: #C7C5CF;"">Mã có hiệu lực trong 10 phút.</p>";
        var html = BuildBrandedEmailHtml("Mã xác minh email Dillustration của bạn", body, DefaultExpiryNote);
        return SendAsync(toEmail, "Xác minh email - Dillustration", html, cancellationToken);
    }

    public Task SendVerificationCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào,</p>
            <p style=""margin: 0 0 4px 0; color: #C7C5CF;"">Đây là mã xác minh để hoàn tất đăng ký tài khoản Dillustration:</p>
            {BuildCodeBlockHtml(code)}
            <p style=""margin: 12px 0 0 0; color: #C7C5CF;"">Mã có hiệu lực trong 10 phút.</p>";
        var html = BuildBrandedEmailHtml("Mã xác minh đăng ký Dillustration của bạn", body, DefaultExpiryNote);
        return SendAsync(toEmail, "Mã xác minh đăng ký - Dillustration", html, cancellationToken);
    }

    public Task SendPasswordResetCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào,</p>
            <p style=""margin: 0 0 4px 0; color: #C7C5CF;"">Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản Dillustration của bạn. Dùng mã bên dưới để tiếp tục:</p>
            {BuildCodeBlockHtml(code)}
            <p style=""margin: 12px 0 0 0; color: #C7C5CF;"">Mã có hiệu lực trong 10 phút.</p>";
        var html = BuildBrandedEmailHtml("Mã đặt lại mật khẩu Dillustration của bạn", body, "Lưu ý: nếu không phải bạn yêu cầu đổi mật khẩu, hãy bỏ qua email này — mật khẩu hiện tại của bạn vẫn giữ nguyên, không có gì bị thay đổi.");
        return SendAsync(toEmail, "Mã đặt lại mật khẩu - Dillustration", html, cancellationToken);
    }

    public Task SendChangeEmailCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào,</p>
            <p style=""margin: 0 0 4px 0; color: #C7C5CF;"">Bạn vừa yêu cầu đổi email đăng nhập của tài khoản Dillustration sang địa chỉ này. Dùng mã bên dưới để xác minh:</p>
            {BuildCodeBlockHtml(code)}
            <p style=""margin: 12px 0 0 0; color: #C7C5CF;"">Mã có hiệu lực trong 10 phút.</p>";
        var html = BuildBrandedEmailHtml("Mã xác minh đổi email Dillustration", body, "Lưu ý: nếu không phải bạn yêu cầu đổi email, hãy bỏ qua email này.");
        return SendAsync(toEmail, "Mã xác minh đổi email - Dillustration", html, cancellationToken);
    }

    public Task SendEmailChangedNoticeAsync(string oldEmail, string newEmail, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào,</p>
            <p style=""margin: 0; color: #C7C5CF;"">
              Email đăng nhập của tài khoản Dillustration gắn với địa chỉ này vừa được đổi sang
              <strong style=""color: #FFFFFF;"">{WebUtility.HtmlEncode(newEmail)}</strong>. Mọi thiết bị khác đã bị đăng xuất để đảm bảo an toàn.
            </p>";
        var html = BuildBrandedEmailHtml("Email đăng nhập của bạn vừa được thay đổi", body, "Nếu đây không phải là bạn, vui lòng liên hệ hỗ trợ ngay lập tức qua support@dillustration.art để khóa tài khoản kịp thời.");
        return SendAsync(oldEmail, "Email tài khoản của bạn vừa được thay đổi - Dillustration", html, cancellationToken);
    }

    public Task SendReauthCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $@"
            <p style=""margin: 0 0 4px 0;"">Xin chào,</p>
            <p style=""margin: 0 0 4px 0; color: #C7C5CF;"">Để xác nhận chính bạn đang yêu cầu đổi email đăng nhập, vui lòng dùng mã xác thực danh tính bên dưới:</p>
            {BuildCodeBlockHtml(code)}
            <p style=""margin: 12px 0 0 0; color: #C7C5CF;"">Mã có hiệu lực trong 10 phút.</p>";
        var html = BuildBrandedEmailHtml("Mã xác thực lại danh tính Dillustration", body, "Lưu ý: nếu không phải bạn yêu cầu, hãy bỏ qua email này — tài khoản của bạn vẫn an toàn.");
        return SendAsync(toEmail, "Mã xác thực lại danh tính - Dillustration", html, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var resendApiKey = _configuration["Email:ResendApiKey"];
        var senderEmail = _configuration["Email:SenderEmail"];
        var senderName = _configuration["Email:SenderName"] ?? "Dillustration Platform";

        // Ưu tiên Resend HTTP API (port 443) vì Render Free Tier chặn outbound SMTP (port 25/465/587).
        if (!string.IsNullOrWhiteSpace(resendApiKey))
        {
            var fromAddress = !string.IsNullOrWhiteSpace(senderEmail) && !senderEmail.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                ? $"{senderName} <{senderEmail}>"
                : $"{senderName} <onboarding@resend.dev>";

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
            {
                Content = JsonContent.Create(new { from = fromAddress, to = new[] { toEmail }, subject, html = htmlBody })
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resendApiKey.Trim());

            try
            {
                using var res = await ResendHttpClient.SendAsync(req, cancellationToken);
                if (res.IsSuccessStatusCode) return;
                var errBody = await res.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Resend API trả lỗi {Status}: {Body}. To={Email}", res.StatusCode, errBody, toEmail);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Gọi Resend API thất bại. To={Email}", toEmail);
            }
        }

        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out var port) ? port : 587;
        var senderPassword = _configuration["Email:SenderPassword"];

        if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(senderEmail) || string.IsNullOrWhiteSpace(senderPassword)
            || senderEmail.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase)
            || senderPassword.StartsWith("REPLACE_WITH", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Email:SenderEmail/SenderPassword chưa được cấu hình — chỉ log thay vì gửi mail thật. To={Email}, Subject={Subject}, Body={Body}",
                toEmail, subject, htmlBody);
            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(senderEmail, senderName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(smtpHost, smtpPort)
        {
            EnableSsl = true,
            Timeout = 6000,
            Credentials = new NetworkCredential(senderEmail, senderPassword)
        };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(6));

        try
        {
            await client.SendMailAsync(message, cts.Token);
        }
        catch (Exception ex)
        {
            // Render Free Tier chặn outbound port 587/465 nên SMTP sẽ timeout — không làm treo request đăng ký.
            _logger.LogWarning(ex,
                "Không thể gửi email qua SMTP ({Host}:{Port}). Fallback ghi log: To={Email}, Subject={Subject}, Body={Body}",
                smtpHost, smtpPort, toEmail, subject, htmlBody);
        }
    }

    public Task SendEscrowReceiptEmailAsync(
        string toEmail,
        string clientName,
        string creatorName,
        string commissionTitle,
        decimal amount,
        string commissionId,
        string licenseType,
        IEnumerable<(string Title, int Percentage, decimal Price)> milestones,
        CancellationToken cancellationToken = default)
    {
        var code = commissionId.Length >= 8 ? commissionId[..8].ToUpperInvariant() : commissionId.ToUpperInvariant();
        var orderCode = $"CMD-{code}";
        var receiptCode = $"REC-{code}";
        var licenseLabel = licenseType == "Commercial" ? "Thương mại (Commercial 1.5×)" : "Cá nhân (Personal)";

        var milestoneRows = string.Join("", milestones.Select(m => $@"
            <tr style=""border-bottom: 1px solid rgba(255, 255, 255, 0.05);"">
                <td style=""padding: 8px 0; color: #E4E1E5;"">{WebUtility.HtmlEncode(m.Title)}</td>
                <td align=""center"" style=""padding: 8px 0; color: #A7ACD9; font-family: monospace; font-size: 11px;"">{m.Percentage}%</td>
                <td align=""right"" style=""padding: 8px 0; color: #FFFFFF; font-family: monospace;"">{m.Price:N0} ₫</td>
            </tr>"));

        var html = $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 20px; background-color: #0B0E17; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #E4E1E5;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width: 600px; margin: 0 auto; background-color: #121622; border-radius: 14px; border: 1px solid rgba(255, 255, 255, 0.08); overflow: hidden;"">
    <tr>
      <td style=""padding: 22px 28px 16px 28px; border-bottom: 1px solid rgba(255, 255, 255, 0.06);"">
        <table role=""presentation"" width=""100%"">
          <tr>
            <td align=""left"">
              <span style=""font-size: 16px; font-weight: bold; color: #FFFFFF; letter-spacing: 0.5px;"">✦ DILLUSTRATION</span>
            </td>
            <td align=""right"">
              <span style=""font-family: monospace; font-size: 11px; color: #A7ACD9; background: rgba(167, 172, 217, 0.1); padding: 4px 8px; border-radius: 6px; border: 1px solid rgba(167, 172, 217, 0.2);"">
                #{receiptCode}
              </span>
            </td>
          </tr>
        </table>
      </td>
    </tr>
    <tr>
      <td style=""padding: 24px 28px 16px 28px; font-size: 13px; line-height: 1.6;"">
        <p style=""margin: 0 0 12px 0;"">Chào <strong>@{WebUtility.HtmlEncode(clientName)}</strong>,</p>
        <p style=""margin: 0; color: #C7C5CF;"">
          Khoản ký quỹ <strong style=""color: #FFFFFF;"">{amount:N0} ₫</strong> cho đơn vẽ <strong style=""color: #FFFFFF;"">""{WebUtility.HtmlEncode(commissionTitle)}""</strong> cùng họa sĩ <strong style=""color: #A7ACD9;"">@{WebUtility.HtmlEncode(creatorName)}</strong> đã được giữ an toàn tại quỹ ủy thác Escrow. Họa sĩ đã nhận thông báo và bắt đầu thực hiện dự án.
        </p>
      </td>
    </tr>
    <tr>
      <td style=""padding: 0 28px 16px 28px;"">
        <div style=""background-color: #171D2D; border-radius: 10px; border: 1px solid rgba(255, 255, 255, 0.05); padding: 14px 16px; font-size: 12px;"">
          <table role=""presentation"" width=""100%"">
            <tr>
              <td style=""color: #919099; padding: 4px 0;"">Mã đơn hàng</td>
              <td align=""right"" style=""color: #FFFFFF; font-family: monospace; font-weight: 600;"">#{orderCode}</td>
            </tr>
            <tr>
              <td style=""color: #919099; padding: 4px 0;"">Quyền sử dụng</td>
              <td align=""right"" style=""color: #34D399; font-weight: 600;"">{licenseLabel}</td>
            </tr>
            <tr>
              <td style=""color: #919099; padding: 4px 0;"">Phương thức</td>
              <td align=""right"" style=""color: #FFFFFF;"">Ví Dillustration • Escrow 24/7</td>
            </tr>
          </table>
        </div>
      </td>
    </tr>
    <tr>
      <td style=""padding: 0 28px 20px 28px; font-size: 12px;"">
        <p style=""margin: 0 0 8px 0; font-size: 11px; font-weight: bold; text-transform: uppercase; color: #919099;"">Tiến độ giải ngân các cột mốc</p>
        <table role=""presentation"" width=""100%"" style=""border-collapse: collapse;"">
          {milestoneRows}
          <tr>
            <td colspan=""2"" style=""padding: 12px 0 4px 0; font-weight: bold; color: #FFFFFF;"">Tổng ký quỹ tạm giữ</td>
            <td align=""right"" style=""padding: 12px 0 4px 0; font-family: monospace; font-size: 14px; font-weight: bold; color: #A7ACD9;"">{amount:N0} ₫</td>
          </tr>
        </table>
      </td>
    </tr>
    <tr>
      <td style=""padding: 0 28px 24px 28px;"">
        <div style=""background-color: rgba(167, 172, 217, 0.05); border-left: 2px solid #A7ACD9; padding: 10px 14px; border-radius: 0 8px 8px 0; font-size: 11.5px; color: #C7C5CF;"">
          <strong style=""color: #FFFFFF;"">Bản xem trước trong Workroom:</strong> Các bức vẽ phác thảo sẽ có dấu mờ mang mã #{receiptCode} để bảo hộ quyền tác giả. File gốc không dấu sẽ tự động mở khóa ngay khi bạn duyệt mốc cuối cùng.
        </div>
      </td>
    </tr>
    <tr>
      <td style=""padding: 16px 28px; background-color: #0E111A; text-align: center; font-size: 11px; color: #64748B;"">
        DILLUSTRATION • Sàn giao dịch tranh & Commission an tâm • Hỗ trợ: support@dillustration.art
      </td>
    </tr>
  </table>
</body>
</html>";
        return SendAsync(toEmail, $"Biên nhận ký quỹ #{receiptCode} — Đơn vẽ cùng @{creatorName}", html, cancellationToken);
    }

    public Task SendCertificateCompletedEmailAsync(
        string toEmail,
        string clientName,
        string creatorName,
        string commissionTitle,
        string commissionId,
        string licenseType,
        string? previewImageUrl,
        CancellationToken cancellationToken = default)
    {
        var code = commissionId.Length >= 8 ? commissionId[..8].ToUpperInvariant() : commissionId.ToUpperInvariant();
        var certCode = $"COA-2026-DILL-{code}";
        var orderCode = $"CMD-{code}";
        var licenseScope = licenseType == "Commercial" ? "Toàn quyền khai thác thương mại (Đ.20 Luật SHTT)" : "Quyền sử dụng cá nhân phi thương mại";

        var html = $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 20px; background-color: #0B0E17; font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; color: #E4E1E5;"">
  <table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""max-width: 600px; margin: 0 auto; background-color: #121622; border-radius: 14px; border: 1px solid rgba(167, 172, 217, 0.3); overflow: hidden;"">
    <tr>
      <td style=""padding: 24px 28px; background: linear-gradient(145deg, #182032 0%, #111524 100%); border-bottom: 1px solid rgba(255, 255, 255, 0.08);"">
        <span style=""font-size: 11px; color: #34D399; font-weight: bold; text-transform: uppercase;"">✓ ĐƠN HÀNG ĐÃ NGHIỆM THU HOÀN TẤT 100%</span>
        <h2 style=""margin: 6px 0 0 0; font-size: 20px; color: #FFFFFF;"">Chứng nhận Bản quyền Số (Certificate of Authenticity)</h2>
        <p style=""margin: 4px 0 0 0; font-size: 12px; color: #A7ACD9;"">Mã chứng thư: {certCode}</p>
      </td>
    </tr>
    <tr>
      <td style=""padding: 24px 28px; font-size: 13px; line-height: 1.6;"">
        <p style=""margin: 0 0 10px 0;"">Chúc mừng <strong>@{WebUtility.HtmlEncode(clientName)}</strong>,</p>
        <p style=""margin: 0 0 16px 0; color: #C7C5CF;"">
          Đơn vẽ <strong>""{WebUtility.HtmlEncode(commissionTitle)}""</strong> cùng tác giả <strong>@{WebUtility.HtmlEncode(creatorName)}</strong> đã chính thức hoàn thành và xác lập quyền sở hữu theo Luật Sở hữu trí tuệ Việt Nam.
        </p>
        <div style=""background-color: #171D2D; border-radius: 10px; border: 1px solid rgba(255, 255, 255, 0.05); padding: 14px 16px; font-size: 12px; margin-bottom: 16px;"">
          <p style=""margin: 0 0 6px 0;""><strong>Tác giả sáng tạo gốc:</strong> {WebUtility.HtmlEncode(creatorName)} (Giữ vĩnh viễn quyền nhân thân)</p>
          <p style=""margin: 0 0 6px 0;""><strong>Chủ sở hữu khai thác:</strong> {WebUtility.HtmlEncode(clientName)} ({licenseScope})</p>
          <p style=""margin: 0;""><strong>Mã đơn hàng:</strong> #{orderCode}</p>
        </div>
        <p style=""margin: 0; font-size: 12px; color: #919099;"">
          Kho tệp tin HD gốc độ phân giải cao đã được mở khóa toàn bộ. Bạn có thể đăng nhập vào phòng Workroom để tải trọn bộ tệp PSD, PNG và xuất bản in PDF chứng nhận bản quyền bất kỳ lúc nào.
        </p>
      </td>
    </tr>
    <tr>
      <td style=""padding: 16px 28px; background-color: #0E111A; text-align: center; font-size: 11px; color: #64748B;"">
        DILLUSTRATION Escrow & Trust Authority • SEP490 Final • FPT University HCM
      </td>
    </tr>
  </table>
</body>
</html>";
        return SendAsync(toEmail, $"Chứng nhận Bản quyền Số [{certCode}] — Đơn #{orderCode} hoàn thành", html, cancellationToken);
    }

    public Task SendBugReportReplyEmailAsync(
        string toEmail,
        string reportTitle,
        string replyMessage,
        string statusLabel,
        CancellationToken cancellationToken = default)
    {
        var safeTitle = WebUtility.HtmlEncode(reportTitle);
        var safeReply = WebUtility.HtmlEncode(replyMessage).Replace("\n", "<br/>");
        var safeStatus = WebUtility.HtmlEncode(statusLabel);

        var html = $@"
<!DOCTYPE html>
<html>
<body style=""margin: 0; padding: 24px; background-color: #131316; font-family: Arial, sans-serif; color: #E4E1E5;"">
  <table width=""100%"" style=""max-width: 560px; margin: 0 auto; background-color: #181B29; border-radius: 16px; border: 1px solid rgba(167, 172, 217, 0.25); overflow: hidden;"">
    <tr>
      <td style=""padding: 20px 24px; background-color: #1F2335; border-bottom: 1px solid rgba(255, 255, 255, 0.08);"">
        <span style=""font-size: 11px; color: #A7ACD9; font-weight: bold; text-transform: uppercase;"">DILLUSTRATION • PHẢN HỒI HỖ TRỢ</span>
        <h2 style=""margin: 6px 0 0 0; font-size: 18px; color: #FFFFFF;"">{safeTitle}</h2>
      </td>
    </tr>
    <tr>
      <td style=""padding: 22px 24px; font-size: 14px; line-height: 1.6;"">
        <p style=""margin: 0 0 12px 0; color: #C7C5CF;"">Quản trị viên đã cập nhật trạng thái: <strong style=""color: #A7ACD9;"">{safeStatus}</strong></p>
        <div style=""background-color: #131316; border-radius: 12px; border: 1px solid rgba(167, 172, 217, 0.2); padding: 14px 16px; color: #E4E1E5;"">
          {safeReply}
        </div>
      </td>
    </tr>
  </table>
</body>
</html>";
        return SendAsync(toEmail, $"[DILLUSTRATION] Phản hồi báo cáo: {reportTitle}", html, cancellationToken);
    }

}