namespace ArtCommission.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string fullName, string verificationToken, CancellationToken cancellationToken = default);
    Task SendPasswordResetEmailAsync(string toEmail, string fullName, string resetToken, CancellationToken cancellationToken = default);
}
