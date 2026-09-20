using ArtCommission.Application.ArtistStudio.Moderation.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Moderation.Queries;

/// <summary>
/// Query lấy danh sách hàng đợi kiểm duyệt tác phẩm (SCR-20).
/// </summary>
public record GetModerationQueueQuery(
    string? Status = "Pending",
    string? Search = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(IReadOnlyList<ModerationQueueItemDto> Items, int TotalCount, int PendingCount)>;

public class GetModerationQueueQueryHandler 
    : IRequestHandler<GetModerationQueueQuery, (IReadOnlyList<ModerationQueueItemDto> Items, int TotalCount, int PendingCount)>
{
    private readonly IApplicationDbContext _db;

    public GetModerationQueueQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(IReadOnlyList<ModerationQueueItemDto> Items, int TotalCount, int PendingCount)> Handle(
        GetModerationQueueQuery request,
        CancellationToken cancellationToken)
    {
        var pendingCount = await _db.Artworks
            .CountAsync(x => x.ModerationStatus == "Pending" && !x.IsDeleted, cancellationToken);

        var query = _db.Artworks
            .AsNoTracking()
            .Include(x => x.CreatorProfile)
                .ThenInclude(cp => cp!.User)
            .Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status) && !request.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Flagged", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.ModerationStatus == "Pending" || !string.IsNullOrEmpty(x.FlagReason));
            }
            else
            {
                query = query.Where(x => x.ModerationStatus == request.Status);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Title.Contains(search) || 
                                     (x.CreatorProfile != null && (x.CreatorProfile.DisplayName.Contains(search) || 
                                                                  (x.CreatorProfile.User != null && x.CreatorProfile.User.UserName!.Contains(search)))));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var page = request.Page > 0 ? request.Page : 1;
        var pageSize = request.PageSize is > 0 and <= 100 ? request.PageSize : 10;

        var items = await query
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new ModerationQueueItemDto
            {
                ArtworkId = x.Id,
                Title = x.Title,
                CreatorProfileId = x.CreatorProfileId,
                CreatorName = x.CreatorProfile != null ? x.CreatorProfile.DisplayName : "Unknown",
                CreatorUsername = x.CreatorProfile != null && x.CreatorProfile.User != null ? x.CreatorProfile.User.UserName ?? string.Empty : string.Empty,
                CreatorAvatarUrl = x.CreatorProfile != null ? x.CreatorProfile.BannerUrl : null,
                ImageUrl = x.ImageUrl,
                ThumbnailUrl = x.ThumbnailUrl,
                Style = x.Style,
                SafeScore = x.SafeScore,
                FlagReason = x.FlagReason,
                ModerationStatus = x.ModerationStatus,
                IsAiGenerated = x.IsAiGenerated,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return (items, totalCount, pendingCount);
    }
}
