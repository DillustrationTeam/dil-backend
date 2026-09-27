using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.SendVerificationCode;

public record SendVerificationCodeCommand(
    string Email
) : IRequest<(bool Success, string[] Errors)>;

public class SendVerificationCodeCommandHandler : IRequestHandler<SendVerificationCodeCommand, (bool Success, string[] Errors)>
{
    private static readonly TimeSpan MinResendInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10);

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;

    public SendVerificationCodeCommandHandler(IApplicationDbContext db, IIdentityService identityService, IEmailService emailService)
    {
        _db = db;
        _identityService = identityService;
        _emailService = emailService;
    }

    public async Task<(bool Success, string[] Errors)> Handle(SendVerificationCodeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var isUnique = await _identityService.IsEmailUniqueAsync(email, cancellationToken);
        if (!isUnique)
        {
            return (false, new[] { "Email already registered. Please sign in instead." });
        }

        var recentCutoff = DateTimeOffset.UtcNow.Subtract(MinResendInterval);
        var hasRecentCode = await _db.EmailVerificationCodes
            .AnyAsync(c => c.Email == email && c.Purpose == VerificationCodePurpose.EmailVerification
                && c.ConsumedAt == null && c.CreatedAt > recentCutoff, cancellationToken);

        if (hasRecentCode)
        {
            return (false, new[] { "Please wait before requesting another code." });
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var codeHash = HashCode(code);

        var verificationCode = new EmailVerificationCode
        {
            Email = email,
            CodeHash = codeHash,
            Purpose = VerificationCodePurpose.EmailVerification,
            ExpiresAt = DateTimeOffset.UtcNow.Add(CodeLifetime)
        };

        _db.EmailVerificationCodes.Add(verificationCode);
        await _db.SaveChangesAsync(cancellationToken);

        await _emailService.SendVerificationCodeEmailAsync(email, code, cancellationToken);

        return (true, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public class SendVerificationCodeCommandValidator : AbstractValidator<SendVerificationCodeCommand>
{
    public SendVerificationCodeCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");
    }
}
