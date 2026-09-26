using System.Net;
using System.Net.Mail;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;

    public EmailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, "Verify your Dillustration email",
            $"Hello {fullName},\n\nYour verification token is:\n{verificationToken}\n", cancellationToken);

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, CancellationToken cancellationToken = default) =>
        SendAsync(toEmail, "Reset your Dillustration password",
            $"Hello {fullName},\n\nUse this token in the password reset form:\n{resetToken}\n", cancellationToken);

    private async Task SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        var host = _configuration["Email:SmtpHost"];
        var sender = _configuration["Email:SenderEmail"];
        var password = _configuration["Email:SenderPassword"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(sender) || string.IsNullOrWhiteSpace(password)
            || sender.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
            || password.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase))
            throw new SmtpException("SMTP email settings are required.");

        var port = int.TryParse(_configuration["Email:SmtpPort"], out var configuredPort) ? configuredPort : 587;
        using var message = new MailMessage
        {
            From = new MailAddress(sender, _configuration["Email:SenderName"] ?? "Dillustration"),
            Subject = subject,
            Body = body
        };
        message.To.Add(toEmail);
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(sender, password)
        };
        await client.SendMailAsync(message, cancellationToken);
    }
}
