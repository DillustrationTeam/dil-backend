using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.ManageCreatorArtworks;

public record GetCreatorArtworksQuery(Guid UserId, string? Search, string? Status)
    : IRequest<IReadOnlyList<ArtworkDto>>;

public sealed class GetCreatorArtworksQueryHandler
    : IRequestHandler<GetCreatorArtworksQuery, IReadOnlyList<ArtworkDto>>
{
    private readonly IApplicationDbContext _db;

    public GetCreatorArtworksQueryHandler(IApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<ArtworkDto>> Handle(GetCreatorArtworksQuery request, CancellationToken cancellationToken)
    {
        var profileId = await _db.CreatorProfiles
            .Where(profile => profile.UserId == request.UserId && !profile.IsDeleted)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (profileId is null)
        {
            return Array.Empty<ArtworkDto>();
        }

        var query = _db.Artworks.AsNoTracking()
            .Where(artwork => artwork.CreatorProfileId == profileId && !artwork.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(artwork => artwork.Title.Contains(search)
                || (artwork.Description != null && artwork.Description.Contains(search))
                || artwork.ArtworkTags.Any(link => link.Tag != null && link.Tag.Name.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status) && request.Status != "All")
        {
            query = request.Status == "Flagged"
                ? query.Where(artwork => artwork.ModerationStatus == "Pending" && artwork.FlagReason != null && artwork.FlagReason != "")
                : query.Where(artwork => artwork.ModerationStatus == request.Status);
        }

        return await query.OrderByDescending(artwork => artwork.CreatedAt)
            .Select(artwork => new ArtworkDto(
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
                artwork.ArtworkTags.Select(link => link.Tag!.Name).ToList()))
            .ToListAsync(cancellationToken);
    }
}

public record UpdateCreatorArtworkCommand(
    Guid UserId,
    Guid ArtworkId,
    string Title,
    string? Description,
    IReadOnlyList<string>? Tags,
    string? ImageUrl,
    string? ThumbnailUrl)
    : IRequest<(ArtworkDto? Data, string[] Errors)>;

public sealed class UpdateCreatorArtworkCommandHandler
    : IRequestHandler<UpdateCreatorArtworkCommand, (ArtworkDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public UpdateCreatorArtworkCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(ArtworkDto? Data, string[] Errors)> Handle(UpdateCreatorArtworkCommand request, CancellationToken cancellationToken)
    {
        var title = request.Title?.Trim() ?? string.Empty;
        var tags = (request.Tags ?? Array.Empty<string>())
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (title.Length is 0 or > 200)
        {
            return (null, new[] { "Title must contain between 1 and 200 characters." });
        }

        if (tags.Length > 10 || tags.Any(tag => tag.Length > 50))
        {
            return (null, new[] { "Use up to 10 tags, each no longer than 50 characters." });
        }

        if ((request.ImageUrl is not null && (request.ImageUrl.Length > 500 || request.ImageUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase)))
            || (request.ThumbnailUrl is not null && (request.ThumbnailUrl.Length > 500 || request.ThumbnailUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))))
        {
            return (null, new[] { "Upload the image file first; image URLs must be 500 characters or fewer." });
        }

        var profileId = await _db.CreatorProfiles
            .Where(profile => profile.UserId == request.UserId && !profile.IsDeleted)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var artwork = profileId is null
            ? null
            : await _db.Artworks
                .Include(item => item.ArtworkTags)
                .ThenInclude(link => link.Tag)
                .FirstOrDefaultAsync(item => item.Id == request.ArtworkId
                    && item.CreatorProfileId == profileId
                    && !item.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (null, new[] { "Artwork not found." });
        }

        artwork.Title = title;
        artwork.Description = request.Description?.Trim();
        if (request.ImageUrl is not null)
        {
            artwork.ImageUrl = request.ImageUrl;
            artwork.ThumbnailUrl = request.ThumbnailUrl ?? request.ImageUrl;
            artwork.ModerationStatus = "Pending";
            artwork.ModeratorId = null;
            artwork.ModeratedAt = null;
            artwork.ModerationNote = null;
            artwork.FlagReason = null;
        }
        artwork.UpdatedAt = DateTimeOffset.UtcNow;

        foreach (var link in artwork.ArtworkTags.ToArray())
        {
            if (link.Tag is null || tags.Contains(link.Tag.Name, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            artwork.ArtworkTags.Remove(link);
            _db.Set<ArtworkTag>().Remove(link);
        }

        foreach (var tagName in tags)
        {
            if (artwork.ArtworkTags.Any(link => string.Equals(link.Tag?.Name, tagName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var tag = await _db.Set<Tag>()
                .FirstOrDefaultAsync(item => item.Name == tagName, cancellationToken);

            if (tag is null)
            {
                tag = new Tag { Name = tagName };
                _db.Set<Tag>().Add(tag);
            }

            artwork.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork.Id, Tag = tag });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return (new ArtworkDto(
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
            artwork.ArtworkTags.Where(link => link.Tag is not null).Select(link => link.Tag!.Name).ToList()),
            Array.Empty<string>());
    }
}

public record DeleteCreatorArtworkCommand(Guid UserId, Guid ArtworkId)
    : IRequest<(bool Success, string[] Errors)>;

public sealed class DeleteCreatorArtworkCommandHandler
    : IRequestHandler<DeleteCreatorArtworkCommand, (bool Success, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public DeleteCreatorArtworkCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<(bool Success, string[] Errors)> Handle(DeleteCreatorArtworkCommand request, CancellationToken cancellationToken)
    {
        var profileId = await _db.CreatorProfiles
            .Where(profile => profile.UserId == request.UserId && !profile.IsDeleted)
            .Select(profile => (Guid?)profile.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var artwork = profileId is null
            ? null
            : await _db.Artworks.FirstOrDefaultAsync(item => item.Id == request.ArtworkId
                && item.CreatorProfileId == profileId
                && !item.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (false, new[] { "Artwork not found." });
        }

        artwork.IsDeleted = true;
        artwork.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return (true, Array.Empty<string>());
    }
}
