using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using MediatR;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetMyTradeSlotStatusQuery(
    Guid UserId
) : IRequest<(bool Success, TradeSlotStatusDto Data)>;

public class GetMyTradeSlotStatusQueryHandler : IRequestHandler<GetMyTradeSlotStatusQuery, (bool Success, TradeSlotStatusDto Data)>
{
    private readonly ITradeSlotService _slotService;

    public GetMyTradeSlotStatusQueryHandler(ITradeSlotService slotService)
    {
        _slotService = slotService;
    }

    public async Task<(bool Success, TradeSlotStatusDto Data)> Handle(GetMyTradeSlotStatusQuery request, CancellationToken cancellationToken)
    {
        var status = await _slotService.GetUserSlotStatusAsync(request.UserId, cancellationToken);
        return (true, status);
    }
}

