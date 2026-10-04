using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;

public record UploadArtworkCommand(
    Guid UserId,
    string Title,
    string? Description,
    string ImageUrl,
    string? ThumbnailUrl,
    bool IsAiGenerated = false,
    decimal? AiDetectionScore = null,
    IReadOnlyList<string>? Tags = null
) : IRequest<(bool Success, ArtworkDto? Data, string[] Errors)>;

public class UploadArtworkCommandValidator : AbstractValidator<UploadArtworkCommand>
{
    public UploadArtworkCommandValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Artwork title is required.")
            .MaximumLength(200).WithMessage("Artwork title must be 200 characters or fewer.");

        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required.")
            .Must(IsHttpUrl).WithMessage("Image URL must be a valid HTTP(S) URL. Upload the image file first.")
            .MaximumLength(500).WithMessage("Image URL must be 500 characters or fewer. Upload the image file first.");

        RuleFor(x => x.ThumbnailUrl)
            .Must(url => string.IsNullOrWhiteSpace(url) || IsHttpUrl(url))
            .WithMessage("Thumbnail URL must be a valid HTTP(S) URL. Upload the thumbnail file first.")
            .MaximumLength(500).WithMessage("Thumbnail URL must be 500 characters or fewer. Upload the image file first.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Artwork description must be 2000 characters or fewer.");

        RuleFor(x => x.AiDetectionScore)
            .InclusiveBetween(0m, 1m).When(x => x.AiDetectionScore.HasValue)
            .WithMessage("AI detection score must be between 0 and 1.");

        RuleFor(x => x.Tags)
            .Must(tags => tags is null || tags.Count <= 10).WithMessage("You can assign up to 10 tags.");
        RuleForEach(x => x.Tags).NotEmpty().MaximumLength(50);
    }

    private static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}

public class UploadArtworkCommandHandler : IRequestHandler<UploadArtworkCommand, (bool Success, ArtworkDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UploadArtworkCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ArtworkDto? Data, string[] Errors)> Handle(UploadArtworkCommand request, CancellationToken cancellationToken)
    {
        var validation = new UploadArtworkCommandValidator().Validate(request);
        if (!validation.IsValid)
        {
            return (false, null, validation.Errors.Select(e => e.ErrorMessage).ToArray());
        }

        var profile = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            var user = await _db.Set<ArtCommission.Domain.Entities.Identity.ApplicationUser>()
                .FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken);

            profile = new ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile
            {
                UserId = request.UserId,
                DisplayName = string.IsNullOrWhiteSpace(user?.FullName) ? "Creator Studio" : user.FullName,
                Bio = "Digital Artist Studio",
                IsAcceptingOrders = true,
                IsApproved = true
            };

            _db.Set<ArtCommission.Domain.Entities.ArtistStudio.CreatorProfile>().Add(profile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        var artwork = new ArtCommission.Domain.Entities.ArtistStudio.Artwork
        {
            CreatorProfileId = profile.Id,
            Title = request.Title.Trim(),
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            ThumbnailUrl = request.ThumbnailUrl,
            IsAiGenerated = request.IsAiGenerated,
            AiDetectionScore = request.AiDetectionScore,
            ModerationStatus = "Pending",
            ViewCount = 0
        };

        _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Artwork>().Add(artwork);
        await _db.SaveChangesAsync(cancellationToken);

        if (request.Tags is { Count: > 0 })
        {
            foreach (var tagName in request.Tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var tag = await _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Tag>()
                    .FirstOrDefaultAsync(x => x.Name == tagName, cancellationToken);

                if (tag is null)
                {
                    tag = new ArtCommission.Domain.Entities.ArtistStudio.Tag
                    {
                        Name = tagName.Trim(),
                        IsAiGenerated = request.IsAiGenerated,
                    };
                    _db.Set<ArtCommission.Domain.Entities.ArtistStudio.Tag>().Add(tag);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                _db.Set<ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag>().Add(new ArtCommission.Domain.Entities.ArtistStudio.ArtworkTag
                {
                    ArtworkId = artwork.Id,
                    TagId = tag.Id
                });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        return (true, new ArtworkDto(
            artwork.Id,
            artwork.CreatorProfileId,
            artwork.Title,
            artwork.Description,
            artwork.ImageUrl,
            artwork.ThumbnailUrl,
            artwork.IsAiGenerated,
            artwork.AiDetectionScore,
            artwork.ModerationStatus,
            artwork.ViewCount,
            artwork.CreatedAt,
            (request.Tags ?? Array.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
        ), Array.Empty<string>());
    }
}
