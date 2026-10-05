using System.Security.Cryptography;
using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.ChangeEmail;

public record ConfirmChangeEmailCommand(
    Guid UserId,
    string NewEmail,
    string Code,
    Guid? CurrentSessionId = null
) : IRequest<(bool Success, UserDto? Data, string[] Errors)>;

public class ConfirmChangeEmailCommandValidator : AbstractValidator<ConfirmChangeEmailCommand>
{
    public ConfirmChangeEmailCommandValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.");
    }
}

public class ConfirmChangeEmailCommandHandler : IRequestHandler<ConfirmChangeEmailCommand, (bool Success, UserDto? Data, string[] Errors)>
{
    private const int MaxAttempts = 5;

    private readonly IApplicationDbContext _db;
    private readonly IIdentityService _identityService;
    private readonly IEmailService _emailService;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;

    public ConfirmChangeEmailCommandHandler(
        IApplicationDbContext db,
        IIdentityService identityService,
        IEmailService emailService,
        IJwtTokenGenerator jwtTokenGenerator)
    {
        _db = db;
        _identityService = identityService;
        _emailService = emailService;
        _jwtTokenGenerator = jwtTokenGenerator;
    }

    public async Task<(bool Success, UserDto? Data, string[] Errors)> Handle(ConfirmChangeEmailCommand request, CancellationToken cancellationToken)
    {
        var validation = new ConfirmChangeEmailCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var newEmail = request.NewEmail.Trim().ToLowerInvariant();

        var verificationCode = await _db.EmailVerificationCodes
            .Where(c => c.UserId == request.UserId && c.Email == newEmail && c.Purpose == VerificationCodePurpose.EmailChange
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

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, null, new[] { "User not found." });
        }

        var oldEmail = user.Email!;

        try
        {
            var (success, errors) = await _identityService.ChangeEmailAsync(request.UserId, newEmail, cancellationToken);
            if (!success)
            {
                return (false, null, errors);
            }

            verificationCode.ConsumedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (LooksLikeUniqueViolation(ex))
        {
            // Đua nhau: người khác vừa đăng ký/đổi trùng email này trong lúc ta đang xác minh mã.
            return (false, null, new[] { "This email is already registered to another account." });
        }

        await _emailService.SendEmailChangedNoticeAsync(oldEmail, newEmail, cancellationToken);

        // Đổi email xong -> thu hồi mọi phiên khác để tăng bảo mật, giữ lại đúng phiên hiện tại.
        await _jwtTokenGenerator.RevokeAllTokensExceptAsync(request.UserId, request.CurrentSessionId, cancellationToken);

        var updatedUser = await _db.Users.AsNoTracking().FirstAsync(u => u.Id == request.UserId, cancellationToken);

        return (true, Map(updatedUser), Array.Empty<string>());
    }

    private static string HashCode(string code)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(code);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    /// <summary>
    /// EF Core không có cách chung chung để phân biệt loại DbUpdateException theo provider (chỉ SQL Server
    /// ở đây), nên nhận diện qua thông điệp lỗi của provider.
    /// </summary>
    private static bool LooksLikeUniqueViolation(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;

        return message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
               || message.Contains("UNIQUE KEY", StringComparison.OrdinalIgnoreCase)
               || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static UserDto Map(ApplicationUser user) => new(
        user.Id, user.Email!, user.FullName, user.IsVerified, user.CreatedAt,
        user.AvatarUrl, user.CoverUrl, user.Bio, user.SocialLinks);
}
