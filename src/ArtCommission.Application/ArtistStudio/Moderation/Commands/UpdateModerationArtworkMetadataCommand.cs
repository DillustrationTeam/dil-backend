using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Moderation.Commands;

public record UpdateModerationArtworkMetadataRequest(
    Guid? CategoryId,
    IReadOnlyList<string>? Tags
);

public record UpdateModerationArtworkMetadataCommand(
    Guid ArtworkId,
    Guid ModeratorId,
    Guid? CategoryId,
    IReadOnlyList<string>? Tags
) : IRequest<(bool Success, ArtworkInspectionDetailDto? Data, string[] Errors)>;

public sealed class UpdateModerationArtworkMetadataCommandHandler
    : IRequestHandler<UpdateModerationArtworkMetadataCommand, (bool Success, ArtworkInspectionDetailDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IMediator _mediator;

    public UpdateModerationArtworkMetadataCommandHandler(IApplicationDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<(bool Success, ArtworkInspectionDetailDto? Data, string[] Errors)> Handle(
        UpdateModerationArtworkMetadataCommand request,
        CancellationToken cancellationToken)
    {
        var artwork = await _db.Artworks
            .Include(a => a.ArtworkTags)
            .ThenInclude(at => at.Tag)
            .FirstOrDefaultAsync(a => a.Id == request.ArtworkId && !a.IsDeleted, cancellationToken);

        if (artwork is null)
        {
            return (false, null, new[] { "Không tìm thấy tác phẩm." });
        }

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            var category = await _db.ArtworkCategories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && !c.IsDeleted && c.IsActive, cancellationToken);

            if (category is null)
            {
                return (false, null, new[] { "Danh mục lớn không tồn tại hoặc đã ngừng hoạt động." });
            }

            artwork.CategoryId = category.Id;
            artwork.Style = category.Name;
        }
        else if (request.CategoryId == Guid.Empty)
        {
            artwork.CategoryId = null;
        }

        if (request.Tags is not null)
        {
            var normalizedTags = request.Tags
                .Select(t => t.Trim().TrimStart('#'))
                .Where(t => t.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (normalizedTags.Length > 10 || normalizedTags.Any(t => t.Length > 50))
            {
                return (false, null, new[] { "Tối đa 10 tag, mỗi tag không quá 50 ký tự." });
            }

            foreach (var link in artwork.ArtworkTags.ToArray())
            {
                if (link.Tag is null || !normalizedTags.Contains(link.Tag.Name, StringComparer.OrdinalIgnoreCase))
                {
                    artwork.ArtworkTags.Remove(link);
                    _db.ArtworkTags.Remove(link);
                }
            }

            foreach (var tagName in normalizedTags)
            {
                if (artwork.ArtworkTags.Any(link => string.Equals(link.Tag?.Name, tagName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Name == tagName, cancellationToken);
                if (tag is null)
                {
                    tag = new Tag { Name = tagName, IsAiGenerated = false };
                    _db.Tags.Add(tag);
                }

                artwork.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork.Id, Tag = tag });
            }
        }

        artwork.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var updated = await _mediator.Send(new Queries.GetArtworkInspectionDetailQuery(artwork.Id), cancellationToken);
        return (true, updated, Array.Empty<string>());
    }
}
