using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record SearchCreatorsForTradeQuery(
    string? Query,
    Guid CurrentUserId,
    int Limit = 10
) : IRequest<List<CreatorTradeSearchDto>>;

public class SearchCreatorsForTradeQueryHandler : IRequestHandler<SearchCreatorsForTradeQuery, List<CreatorTradeSearchDto>>
{
    private readonly IApplicationDbContext _db;
    private readonly ITradeSlotService _slotService;

    public SearchCreatorsForTradeQueryHandler(IApplicationDbContext db, ITradeSlotService slotService)
    {
        _db = db;
        _slotService = slotService;
    }

    public async Task<List<CreatorTradeSearchDto>> Handle(SearchCreatorsForTradeQuery request, CancellationToken cancellationToken)
    {
        var queryable = _db.CreatorProfiles
            .Include(cp => cp.User)
            .AsNoTracking()
            .Where(cp => !cp.IsDeleted && cp.UserId != request.CurrentUserId);

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var term = request.Query.Trim().ToLower();
            queryable = queryable.Where(cp =>
                cp.DisplayName.ToLower().Contains(term) ||
                (cp.Headline != null && cp.Headline.ToLower().Contains(term)) ||
                (cp.Specialties != null && cp.Specialties.ToLower().Contains(term)) ||
                (cp.User != null && cp.User.FullName != null && cp.User.FullName.ToLower().Contains(term)));
        }

        var limit = Math.Clamp(request.Limit, 1, 30);
        var creators = await queryable
            .OrderByDescending(cp => cp.FollowerCount)
            .ThenBy(cp => cp.DisplayName)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var results = new List<CreatorTradeSearchDto>();
        foreach (var c in creators)
        {
            var (canTrade, availableSlots, _) = await _slotService.CheckTradeSlotAvailableAsync(c.UserId, cancellationToken);
            results.Add(new CreatorTradeSearchDto(
                CreatorProfileId: c.Id,
                UserId: c.UserId,
                DisplayName: c.DisplayName,
                Headline: c.Headline,
                Specialties: c.Specialties,
                AvatarUrl: c.User?.AvatarUrl,
                AvailableSlots: availableSlots,
                IsAcceptingOrders: c.IsAcceptingOrders && canTrade
            ));
        }

        return results;
    }
}

