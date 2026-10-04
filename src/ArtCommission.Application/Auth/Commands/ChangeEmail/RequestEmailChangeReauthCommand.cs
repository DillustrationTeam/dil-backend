using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ChangeEmail;

/// <summary>
/// Gửi OTP tới email HIỆN TẠI để tài khoản không có mật khẩu (chỉ đăng nhập Google) xác thực lại
/// danh tính trước khi đổi email — thay thế cho bước nhập mật khẩu mà tài khoản này không có.
/// </summary>
public record RequestEmailChangeReauthCommand(Guid UserId) : IRequest<(bool Success, string[] Errors)>;

public class RequestEmailChangeReauthCommandHandler : IRequestHandler<RequestEmailChangeReauthCommand, (bool Success, string[] Errors)>
{
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;

    public RequestEmailChangeReauthCommandHandler(IApplicationDbContext db, IIdentityService identityService, IEmailService emailService)
    {
        _db = db;
        _identityService = identityService;
        _emailService = emailService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(RequestEmailChangeReauthCommand request, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, new[] { "User not found." });
        }

        var hasPassword = await _identityService.HasPasswordAsync(request.UserId, cancellationToken);
        if (hasPassword)
        {
            return (false, new[] { "This account already has a password — use it to request an email change instead." });
        }

        var email = user.Email!.Trim().ToLowerInvariant();

        var recentCutoff = DateTimeOffset.UtcNow.Subtract(MinResendInterval);
        var hasRecentCode = await _db.EmailVerificationCodes
            .AnyAsync(c => c.UserId == request.UserId && c.Email == email && c.Purpose == VerificationCodePurpose.EmailChangeReauth
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
            Email = email,
            CodeHash = codeHash,
            Purpose = VerificationCodePurpose.EmailChangeReauth,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        });
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendReauthCodeEmailAsync(email, code, cancellationToken);

        return (true, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}
