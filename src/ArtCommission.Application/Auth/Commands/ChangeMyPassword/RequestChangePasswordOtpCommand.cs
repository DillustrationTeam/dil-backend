using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ChangeMyPassword;

public record RequestChangePasswordOtpCommand(Guid UserId) : IRequest<(bool Success, string[] Errors)>;

public class RequestChangePasswordOtpCommandHandler : IRequestHandler<RequestChangePasswordOtpCommand, (bool Success, string[] Errors)>
{
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IEmailService _emailService;

    public RequestChangePasswordOtpCommandHandler(IApplicationDbContext db, IEmailService emailService)
    {
        _db = db;
        _emailService = emailService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(RequestChangePasswordOtpCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        var email = user.Email!.Trim().ToLowerInvariant();

        var recentCutoff = DateTimeOffset.UtcNow.Subtract(MinResendInterval);
        var hasRecentCode = await _db.EmailVerificationCodes
            .AnyAsync(c => c.Email == email && c.Purpose == VerificationCodePurpose.ChangePassword
                && c.ConsumedAt == null && c.CreatedAt > recentCutoff, cancellationToken);
        if (hasRecentCode)
        {
            return (false, new[] { "Please wait before requesting another code." });
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);

        _db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            Email = email,
            CodeHash = codeHash,
            Purpose = VerificationCodePurpose.ChangePassword,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetCodeEmailAsync(email, code, cancellationToken);

        return (true, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
