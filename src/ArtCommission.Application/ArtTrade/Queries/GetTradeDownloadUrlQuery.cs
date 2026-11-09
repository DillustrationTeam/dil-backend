using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtTrade.Queries;

public sealed record GetTradeDownloadUrlQuery(
    Guid TradeId,
    Guid UserId
) : IRequest<(bool Success, TradeDownloadUrlDto? Data, string[] Errors)>;

public class GetTradeDownloadUrlQueryHandler : IRequestHandler<GetTradeDownloadUrlQuery, (bool Success, TradeDownloadUrlDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;

    public GetTradeDownloadUrlQueryHandler(IApplicationDbContext db, IStorageService storageService)
    {
        _db = db;
        _storageService = storageService;
    }

    public async Task<(bool Success, TradeDownloadUrlDto? Data, string[] Errors)> Handle(GetTradeDownloadUrlQuery request, CancellationToken cancellationToken)
    {
        var trade = await _db.ArtTrades
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == request.TradeId && !t.IsDeleted, cancellationToken);

        if (trade == null)
        {
            return (false, null, ["Không tìm thấy giao dịch trao đổi tranh."]);
        }

        var isInitiator = trade.InitiatorId == request.UserId;
        var isReceiver = trade.ReceiverId == request.UserId;
        if (!isInitiator && !isReceiver)
        {
            return (false, null, ["Bạn không tham gia vào giao dịch này."]);
        }

        if (trade.Status != TradeStatus.Completed)
        {
            return (false, null, ["Chỉ có thể tải file gốc khi cả 2 bên đã bấm Xác nhận hài lòng và giao dịch Hoàn tất (Completed)."]);
        }

        // Họa sĩ A tải file do Họa sĩ B nộp; Họa sĩ B tải file do Họa sĩ A nộp
        var targetFileKey = isInitiator ? trade.ReceiverOriginalFileUrl : trade.InitiatorOriginalFileUrl;
        if (string.IsNullOrEmpty(targetFileKey))
        {
            return (false, null, ["Không tìm thấy file gốc của tác phẩm để tải về."]);
        }

        var expiresIn = TimeSpan.FromMinutes(15);
        var expiresAt = DateTimeOffset.UtcNow.Add(expiresIn);
        var presignedUrl = _storageService.GeneratePresignedDownloadUrl(targetFileKey, expiresIn);
        var fileName = Path.GetFileName(targetFileKey);

        return (true, new TradeDownloadUrlDto(presignedUrl, expiresAt, fileName), []);
    }
}

