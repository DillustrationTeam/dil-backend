using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly ILogger<EmailService> _logger;

    public EmailService(ILogger<EmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending verification email to {Email} ({FullName}) with token: {Token}", toEmail, fullName, verificationToken);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Sending password reset email to {Email} ({FullName}) with token: {Token}", toEmail, fullName, resetToken);
        return Task.CompletedTask;
    }
}
