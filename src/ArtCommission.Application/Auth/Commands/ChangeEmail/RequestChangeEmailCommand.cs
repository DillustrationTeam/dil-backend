using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ChangeEmail;

public record RequestChangeEmailCommand(
    Guid UserId,
    string NewEmail,
    string? CurrentPassword = null,
    string? TwoFactorCode = null,
    string? ReauthCode = null
) : IRequest<(bool Success, string[] Errors)>;

public class RequestChangeEmailCommandValidator : AbstractValidator<RequestChangeEmailCommand>
{
    public RequestChangeEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}

public class RequestChangeEmailCommandHandler : IRequestHandler<RequestChangeEmailCommand, (bool Success, string[] Errors)>
{
    private const int MaxReauthAttempts = 5;
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;

    public RequestChangeEmailCommandHandler(IApplicationDbContext db, IIdentityService identityService, IEmailService emailService)
    {
        _db = db;
        _identityService = identityService;
        _emailService = emailService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(RequestChangeEmailCommand request, CancellationToken cancellationToken)
    {
        var validation = new RequestChangeEmailCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        var currentEmail = user.Email!.Trim().ToLowerInvariant();

        if (string.Equals(currentEmail, newEmail, StringComparison.OrdinalIgnoreCase))
        {
            return (false, new[] { "New email must be different from your current email." });
        }

        var isUnique = await _identityService.IsEmailUniqueAsync(newEmail, cancellationToken);
        if (!isUnique)
        {
            return (false, new[] { "This email is already registered to another account." });
        }

        // Xác thực lại danh tính trước khi cho đổi email — mật khẩu (+2FA nếu có) với tài khoản có mật khẩu,
        // hoặc mã OTP gửi tới email hiện tại (sinh ra ở RequestEmailChangeReauthCommand) với tài khoản chỉ đăng nhập Google.
        var hasPassword = await _identityService.HasPasswordAsync(request.UserId, cancellationToken);
        if (hasPassword)
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return (false, new[] { "Current password is required." });
            }

            var passwordValid = await _identityService.VerifyPasswordAsync(request.UserId, request.CurrentPassword, cancellationToken);
            if (!passwordValid)
            {
                return (false, new[] { "Incorrect password." });
            }

            var twoFactorEnabled = await _identityService.IsTwoFactorEnabledAsync(request.UserId, cancellationToken);
            if (twoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(request.TwoFactorCode))
                {
                    return (false, new[] { "Two-factor code is required." });
                }

                var (twoFactorValid, _, twoFactorCodeAlreadyUsed) = await _identityService.VerifyTwoFactorCodeAsync(request.UserId, request.TwoFactorCode, cancellationToken);
                if (!twoFactorValid)
                {
                    return (false, new[] { twoFactorCodeAlreadyUsed ? "This recovery code has already been used." : "Invalid two-factor code." });
                }
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.ReauthCode))
            {
                return (false, new[] { "Verification code for your current email is required." });
            }

            var reauthCode = await _db.EmailVerificationCodes
                .Where(c => c.UserId == request.UserId && c.Email == currentEmail && c.Purpose == VerificationCodePurpose.EmailChangeReauth
                    && c.ConsumedAt == null && c.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(c => c.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (reauthCode == null)
            {
                return (false, new[] { "Code expired or not found, please request a new one." });
            }

            var reauthCodeHash = HashCode(request.ReauthCode.Trim());
            if (!string.Equals(reauthCodeHash, reauthCode.CodeHash, StringComparison.Ordinal))
            {
                reauthCode.AttemptCount++;
                if (reauthCode.AttemptCount >= MaxReauthAttempts)
                {
                    reauthCode.ExpiresAt = DateTimeOffset.UtcNow;
                }

                await _db.SaveChangesAsync(cancellationToken);
                return (false, new[] { "Invalid code." });
            }

            reauthCode.ConsumedAt = DateTimeOffset.UtcNow;
        }

        var recentCutoff = DateTimeOffset.UtcNow.Subtract(MinResendInterval);
        var hasRecentCode = await _db.EmailVerificationCodes
            .AnyAsync(c => c.Email == newEmail && c.Purpose == VerificationCodePurpose.EmailChange
                && c.ConsumedAt == null && c.CreatedAt > recentCutoff, cancellationToken);
        if (hasRecentCode)
        {
            return (false, new[] { "Please wait before requesting another code." });
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);

        _db.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = request.UserId,
            Email = newEmail,
            CodeHash = codeHash,
            Purpose = VerificationCodePurpose.EmailChange,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendChangeEmailCodeEmailAsync(newEmail, code, cancellationToken);

        return (true, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
