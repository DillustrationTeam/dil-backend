using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetArtTradeByIdQuery(
    Guid TradeId,
    Guid UserId
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

        var isParticipant = trade.InitiatorId == request.UserId || trade.ReceiverId == request.UserId;
        if (!isParticipant)
        {
            return (false, null, ["Bạn không có quyền xem chi tiết giao dịch này."]);
        }

        return (true, ArtTradeDto.FromEntity(trade, _storageService), []);
    }
}

