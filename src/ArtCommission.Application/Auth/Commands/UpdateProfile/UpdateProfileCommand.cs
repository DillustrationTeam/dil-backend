using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auth.Commands.UpdateProfile;

public record UpdateProfileCommand(
    Guid UserId,
    string? FullName,
    string? Bio,
    string? AvatarUrl,
    string? CoverUrl,
    List<string>? SocialLinks
) : IRequest<(bool Success, UserDto? Data, string[] Errors)>;

public class UpdateProfileCommandValidator : AbstractValidator<UpdateProfileCommand>
{
    // Danh sách tối giản, chỉ để chặn vài từ phổ biến nhất — dự án chưa có thư viện
    // profanity filter nào, đây là giải pháp tạm, không toàn diện.
    private static readonly string[] BannedWords = { "fuck", "shit", "địt", "lồn", "đĩ" };

    public UpdateProfileCommandValidator()
    {
        RuleFor(x => x.FullName)
            .Must(name => name!.Trim().Length is >= 2 and <= 50)
            .WithMessage("Full name must be 2-50 characters.")
            .Must(name => !ContainsBannedWord(name!))
            .WithMessage("Full name contains inappropriate language.")
            .When(x => !string.IsNullOrWhiteSpace(x.FullName));

        RuleFor(x => x.Bio)
            .MaximumLength(1000).WithMessage("Bio must be 1000 characters or fewer.")
            .When(x => !string.IsNullOrWhiteSpace(x.Bio));

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithMessage("Avatar URL must be 500 characters or fewer.")
            .Must(IsValidUrl).WithMessage("Avatar URL is not a valid URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.AvatarUrl));

        RuleFor(x => x.CoverUrl)
            .MaximumLength(500).WithMessage("Cover URL must be 500 characters or fewer.")
            .Must(IsValidUrl).WithMessage("Cover URL is not a valid URL.")
            .When(x => !string.IsNullOrWhiteSpace(x.CoverUrl));

        RuleFor(x => x.SocialLinks)
            .Must(links => links == null || links.Count <= 10)
            .WithMessage("You can add up to 10 social links.");

        RuleForEach(x => x.SocialLinks)
            .Must(IsValidUrl).WithMessage("One of the social links is not a valid URL.")
            .When(x => x.SocialLinks is not null);
    }

    private static bool ContainsBannedWord(string value)
    {
        var lowered = value.ToLowerInvariant();
        return BannedWords.Any(lowered.Contains);
    }

    private static bool IsValidUrl(string? url) =>
        !string.IsNullOrWhiteSpace(url)
        && Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public class UpdateProfileCommandHandler : IRequestHandler<UpdateProfileCommand, (bool Success, UserDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateProfileCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, UserDto? Data, string[] Errors)> Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var validation = new UpdateProfileCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, null, new[] { "User not found." });
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            user.FullName = request.FullName.Trim();
        }

        if (request.Bio is not null)
        {
            user.Bio = string.IsNullOrWhiteSpace(request.Bio) ? null : request.Bio.Trim();
        }

        if (request.AvatarUrl is not null)
        {
            user.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        }

        if (request.CoverUrl is not null)
        {
            user.CoverUrl = string.IsNullOrWhiteSpace(request.CoverUrl) ? null : request.CoverUrl.Trim();
        }

        if (request.SocialLinks is not null)
        {
            user.SocialLinks = request.SocialLinks
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Select(l => l.Trim())
                .ToList();
        }

        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return (true, Map(user), Array.Empty<string>());
    }

    private static UserDto Map(Domain.Entities.Identity.ApplicationUser user)
        => new(
            user.Id,
            user.Email!,
            user.FullName,
            user.IsVerified,
            user.CreatedAt,
            user.AvatarUrl,
            user.CoverUrl,
            user.Bio,
            user.SocialLinks
        );
}
