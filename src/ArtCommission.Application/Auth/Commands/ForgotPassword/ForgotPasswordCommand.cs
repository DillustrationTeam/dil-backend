using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ForgotPassword;

public record ForgotPasswordCommand(
    string Email
) : IRequest<bool>;

public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, bool>
{
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(IApplicationDbContext db, IIdentityService identityService, IEmailService emailService)
    {
        _db = db;
        _identityService = identityService;
        _emailService = emailService;
    }

    public async Task<bool> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // Dùng chính API đã có sẵn để kiểm tra tồn tại thay vì thêm method mới — token sinh ra
        // ở đây không dùng tới, chỉ mượn nó để biết email có gắn với tài khoản còn hoạt động không.
        var probe = await _identityService.GeneratePasswordResetTokenAsync(email, cancellationToken);
        var userExists = !string.IsNullOrEmpty(probe);

        // Luôn trả về true dù email có tồn tại hay không, và không báo lỗi khi đang trong thời
        // gian chờ gửi lại — tránh lộ thông tin email nào đã đăng ký (chống dò email).
        if (!userExists)
        {
            return true;
        }

        var recentCutoff = DateTimeOffset.UtcNow.Subtract(MinResendInterval);
        var hasRecentCode = await _db.EmailVerificationCodes
            .AnyAsync(c => c.Email == email && c.Purpose == VerificationCodePurpose.PasswordReset
                && c.ConsumedAt == null && c.CreatedAt > recentCutoff, cancellationToken);

        if (hasRecentCode)
        {
            return true;
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);

        var verificationCode = new EmailVerificationCode
        {
            Email = email,
            CodeHash = codeHash,
            Purpose = VerificationCodePurpose.PasswordReset,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        };

        _db.EmailVerificationCodes.Add(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendPasswordResetCodeEmailAsync(email, code, cancellationToken);

        return true;
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
