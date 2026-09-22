using System.Security.Cryptography;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ConfirmVerificationCode;

public record ConfirmVerificationCodeCommand(
    string Email,
    string Code
) : IRequest<(bool Success, string? VerificationTicket, string[] Errors)>;

public class ConfirmVerificationCodeCommandHandler : IRequestHandler<ConfirmVerificationCodeCommand, (bool Success, string? VerificationTicket, string[] Errors)>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly IEmailVerificationService _emailVerificationService;

    public ConfirmVerificationCodeCommandHandler(IApplicationDbContext db, IEmailVerificationService emailVerificationService)
    {
        _db = db;
        _emailVerificationService = emailVerificationService;
    }

    public async Task<(bool Success, string? VerificationTicket, string[] Errors)> Handle(ConfirmVerificationCodeCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var verificationCode = await _db.EmailVerificationCodes
            .Where(c => c.Email == email && c.Purpose == VerificationCodePurpose.EmailVerification
                && c.ConsumedAt == null && c.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verificationCode == null)
        {
            return (false, null, new[] { "Code expired or not found, please request a new one." });
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
            return (false, null, new[] { "Invalid code." });
        }

        verificationCode.ConsumedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var ticket = _emailVerificationService.GenerateTicket(email);
        return (true, ticket, Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }
}

public class ConfirmVerificationCodeCommandValidator : AbstractValidator<ConfirmVerificationCodeCommand>
{
    public ConfirmVerificationCodeCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.");
    }
}
