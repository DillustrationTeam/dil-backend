using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ChangeMyPassword;

public record ChangeMyPasswordCommand(
    Guid UserId,
    string? CurrentPassword,
    string NewPassword,
    string Code,
    Guid? CurrentSessionId
) : IRequest<(bool Success, string[] Errors)>;

public class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.NewPassword)
            .MinimumLength(6).WithMessage("New password must be at least 6 characters.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Verification code is required.");
    }
}

public class ChangeMyPasswordCommandHandler : IRequestHandler<ChangeMyPasswordCommand, (bool Success, string[] Errors)>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public ChangeMyPasswordCommandHandler(IApplicationDbContext db, IIdentityService identityService, IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _identityService = identityService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<(bool Success, string[] Errors)> Handle(ChangeMyPasswordCommand request, CancellationToken cancellationToken)
    {
        var validation = new ChangeMyPasswordCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        var email = user.Email!.Trim().ToLowerInvariant();

        // Bắt buộc xác thực OTP gửi tới email hiện tại trước khi cho đổi/đặt mật khẩu.
        var verificationCode = await _db.EmailVerificationCodes
            .Where(c => c.Email == email && c.Purpose == VerificationCodePurpose.ChangePassword
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

        var hasPassword = await _identityService.HasPasswordAsync(request.UserId, cancellationToken);

        (bool Success, string[] Errors) result;
        if (!hasPassword)
        {
            // Tài khoản chỉ đăng nhập qua Google (chưa từng có mật khẩu) -> "Đặt mật khẩu", không cần mật khẩu hiện tại.
            result = await _identityService.AddPasswordAsync(request.UserId, request.NewPassword, cancellationToken);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            {
                return (false, new[] { "Current password is required." });
            }

            result = await _identityService.ChangePasswordAsync(request.UserId, request.CurrentPassword, request.NewPassword, cancellationToken);
        }

        if (!result.Success)
        {
            return result;
        }

        verificationCode.ConsumedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Đổi/đặt mật khẩu xong -> thu hồi mọi phiên khác để tăng bảo mật, giữ lại đúng phiên hiện tại.
        await _jwtTokenGenerator.RevokeAllTokensExceptAsync(request.UserId, request.CurrentSessionId, cancellationToken);

        return (true, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
