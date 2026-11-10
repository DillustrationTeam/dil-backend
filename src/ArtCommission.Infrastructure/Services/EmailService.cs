using System.Net;
using System.Net.Mail;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;

    public EmailService(ILogger<EmailService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Xin chào {fullName},</p><p>Mã xác minh email của bạn: <b>{verificationToken}</b></p>";
        return SendAsync(toEmail, "Xác minh email - Dillustration", body, cancellationToken);
    }

    public Task SendVerificationCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Mã xác minh email của bạn là:</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:4px;\">{code}</p><p>Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu mã này, hãy bỏ qua email.</p>";
        return SendAsync(toEmail, "Mã xác minh đăng ký - Dillustration", body, cancellationToken);
    }

    public Task SendPasswordResetCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Mã đặt lại mật khẩu của bạn là:</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:4px;\">{code}</p><p>Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu đổi mật khẩu, hãy bỏ qua email này.</p>";
        return SendAsync(toEmail, "Mã đặt lại mật khẩu - Dillustration", body, cancellationToken);
    }

    public Task SendChangeEmailCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Mã xác minh để đổi sang email này là:</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:4px;\">{code}</p><p>Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu đổi email, hãy bỏ qua email này.</p>";
        return SendAsync(toEmail, "Mã xác minh đổi email - Dillustration", body, cancellationToken);
    }

    public Task SendEmailChangedNoticeAsync(string oldEmail, string newEmail, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Email đăng nhập của tài khoản bạn vừa được đổi sang <b>{newEmail}</b>.</p><p>Nếu đây không phải là bạn, vui lòng liên hệ hỗ trợ ngay lập tức.</p>";
        return SendAsync(oldEmail, "Email tài khoản của bạn vừa được thay đổi - Dillustration", body, cancellationToken);
    }

    public Task SendReauthCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Mã xác thực lại danh tính của bạn là:</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:4px;\">{code}</p><p>Dùng mã này để xác nhận chính bạn đang yêu cầu đổi email đăng nhập. Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>";
        return SendAsync(toEmail, "Mã xác thực lại danh tính - Dillustration", body, cancellationToken);
    }

    private async Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var smtpHost = _configuration["Email:SmtpHost"];
        var smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out var port) ? port : 587;
        var senderEmail = _configuration["Email:SenderEmail"];
        var senderPassword = _configuration["Email:SenderPassword"];
        var senderName = _configuration["Email:SenderName"] ?? "Dillustration Platform";

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
            Credentials = new NetworkCredential(senderEmail, senderPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
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

}