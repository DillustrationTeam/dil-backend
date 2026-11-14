using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetArtTradeByIdQuery(
    Guid TradeId,
    Guid UserId,
    bool IsAdmin = false
) : IRequest<(bool Success, ArtTradeDto? Data, string[] Errors)>;

public class GetArtTradeByIdQueryHandler : IRequestHandler<GetArtTradeByIdQuery, (bool Success, ArtTradeDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;

    public GetArtTradeByIdQueryHandler(IApplicationDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<(bool Success, ArtTradeDto? Data, string[] Errors)> Handle(GetArtTradeByIdQuery request, CancellationToken cancellationToken)
    {
        var trade = await _db.ArtTrades
            .AsNoTracking()
            .Include(t => t.Initiator)
            .Include(t => t.Receiver)
            .Include(t => t.InitiatorArtwork)
            .Include(t => t.ReceiverArtwork)
            .FirstOrDefaultAsync(t => t.Id == request.TradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
        {
            return (false, null, ["Không tìm thấy giao dịch trao đổi tranh."]);
        }

        if (trade.ReceiverAcceptedAt.HasValue && trade.Status == TradeStatus.Proposed)
        {
            var tracked = await _db.ArtTrades.FirstOrDefaultAsync(t => t.Id == trade.Id && !t.IsDeleted, cancellationToken);
            if (tracked != null && tracked.Status == TradeStatus.Proposed)
            {
                var now = DateTimeOffset.UtcNow;
                tracked.Status = TradeStatus.Accepted;
                if (!tracked.SubmissionDeadlineAt.HasValue)
                {
                    tracked.SubmissionDeadlineAt = now.AddDays(14);
                }
                tracked.UpdatedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                trade.Status = TradeStatus.Accepted;
                trade.SubmissionDeadlineAt = tracked.SubmissionDeadlineAt;
                trade.UpdatedAt = now;
            }
        }

        var isParticipant = trade.InitiatorId == request.UserId || trade.ReceiverId == request.UserId;
        if (!isParticipant && !request.IsAdmin)
        {
            return (false, null, ["Bạn không có quyền xem chi tiết giao dịch này."]);
        }

        return (true, ArtTradeDto.FromEntity(trade, _storageService), []);
    }
}

