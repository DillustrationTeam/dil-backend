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

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Xin chào {fullName},</p><p>Mã đặt lại mật khẩu của bạn: <b>{resetToken}</b></p>";
        return SendAsync(toEmail, "Đặt lại mật khẩu - Dillustration", body, cancellationToken);
    }

    public Task SendVerificationCodeEmailAsync(string toEmail, string code, CancellationToken cancellationToken = default)
    {
        var body = $"<p>Mã xác minh email của bạn là:</p><p style=\"font-size:28px;font-weight:bold;letter-spacing:4px;\">{code}</p><p>Mã có hiệu lực trong 10 phút. Nếu bạn không yêu cầu mã này, hãy bỏ qua email.</p>";
        return SendAsync(toEmail, "Mã xác minh đăng ký - Dillustration", body, cancellationToken);
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
}
