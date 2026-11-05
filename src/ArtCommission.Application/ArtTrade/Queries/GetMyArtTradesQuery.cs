using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetMyArtTradesQuery(
    Guid UserId,
    TradeStatus? Status = null,
    int Page = 1,
    int PageSize = 10
) : IRequest<(bool Success, List<ArtTradeSummaryDto> Items, int TotalCount)>;

public class GetMyArtTradesQueryHandler : IRequestHandler<GetMyArtTradesQuery, (bool Success, List<ArtTradeSummaryDto> Items, int TotalCount)>
{
    private readonly IApplicationDbContext _db;

    public GetMyArtTradesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, List<ArtTradeSummaryDto> Items, int TotalCount)> Handle(GetMyArtTradesQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 50 ? 10 : request.PageSize;

        var query = _db.ArtTrades
            .AsNoTracking()
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .Where(t => !t.IsDeleted && (t.InitiatorId == request.UserId || t.ReceiverId == request.UserId));

        if (request.Status.HasValue)
        {
            query = query.Where(t => t.Status == request.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var trades = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = trades.Select(t => TradeMapper.ToSummaryDto(t, request.UserId)).ToList();
        return (true, items, totalCount);
    }
}

