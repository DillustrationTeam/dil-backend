using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ResetPassword;

public record ResetPasswordCommand(
    string Email,
    string Code,
    string NewPassword
) : IRequest<(bool Success, string[] Errors)>;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, (bool Success, string[] Errors)>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;

    public ResetPasswordCommandHandler(IApplicationDbContext db, IIdentityService identityService)
    {
        _db = db;
        _identityService = identityService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var verificationCode = await _db.EmailVerificationCodes
            .Where(c => c.Email == email && c.Purpose == VerificationCodePurpose.PasswordReset
                && c.ConsumedAt == null && c.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verificationCode == null)
        {
            return (false, new[] { "Code expired or not found, please request a new one." });
        }

        var codeHash = HashCode(request.Code.Trim());
        if (!string.Equals(codeHash, verificationCode.CodeHash, StringComparison.Ordinal))
        {
            verificationCode.AttemptCount++;
            if (verificationCode.AttemptCount >= MaxAttempts)
            {
                verificationCode.ExpiresAt = DateTimeOffset.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return (false, new[] { "Invalid code." });
        }

        verificationCode.ConsumedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Mã OTP đã xác minh danh tính người dùng — mượn token nội bộ của ASP.NET Identity để
        // thực sự đổi mật khẩu, tái dùng validator/hasher có sẵn thay vì tự set password hash.
        var identityToken = await _identityService.GeneratePasswordResetTokenAsync(email, cancellationToken);
        if (string.IsNullOrEmpty(identityToken))
        {
            return (false, new[] { "Invalid reset password request." });
        }

        return await _identityService.ResetPasswordAsync(email, identityToken, request.NewPassword, cancellationToken);
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("New password is required.")
            .MinimumLength(6).WithMessage("Password must be at least 6 characters long.");
    }
}
