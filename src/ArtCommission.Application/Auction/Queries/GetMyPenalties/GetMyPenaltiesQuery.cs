using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Auction.Queries.GetAuctionPenalties;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetMyPenalties;

/// <summary>
/// "Tôi bị báo cáo" — người bán tự đọc các lần bị người mua khiếu nại và bị Ban quản trị
/// xác nhận sai.
///
/// VÌ SAO CẦN: màn quản trị (GetAuctionPenaltiesQuery) chỉ Admin xem được. Người bán trước đây
/// chỉ thấy đúng một con số "Bạn đang ở mức 3/5" trong thẻ giao hàng, kèm bảng thang phạt chung
/// chung — không biết mình bị báo cáo ở phiên nào, người mua nêu lý do gì, Ban quản trị kết luận
/// ra sao, còn bị trừ bao nhiêu phiên, có đang bị khoá bán hay không, và án phạt có bị thu hồi.
///
/// Phạm vi dữ liệu: CHỈ án phạt của chính người gọi (lọc theo SellerId = UserId), không có
/// đường nâng quyền qua query param.
/// </summary>
public sealed record GetMyPenaltiesQuery(
    Guid UserId,
    string? Cursor = null,
    int Limit = 10
) : IRequest<(bool Success, IReadOnlyList<MyAuctionPenaltyDto>? Data, object? Meta, string[] Errors)>;

public sealed class GetMyPenaltiesQueryHandler
    : IRequestHandler<GetMyPenaltiesQuery, (bool, IReadOnlyList<MyAuctionPenaltyDto>?, object?, string[])>
{
    private const int MaxLimit = 50;
    private const int DefaultLimit = 10;

    private readonly IApplicationDbContext _db;

    public GetMyPenaltiesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<MyAuctionPenaltyDto>?, object?, string[])> Handle(
        GetMyPenaltiesQuery request,
        CancellationToken cancellationToken)
    {
        if (request.UserId == Guid.Empty)
        {
            return (false, null, null, ["Cần đăng nhập để xem lịch sử bị báo cáo."]);
        }

        var limit = request.Limit <= 0 ? DefaultLimit : Math.Min(request.Limit, MaxLimit);
        var now = DateTimeOffset.UtcNow;

        var baseQuery = _db.AuctionPenalties
            .AsNoTracking()
            .Where(p => p.SellerId == request.UserId && !p.IsDeleted);

        // Tổng hợp lấy trên TOÀN BỘ án phạt của người bán, không phụ thuộc trang đang xem —
        // mức phạt hiện tại là thuộc tính của tài khoản, không phải của trang 1.
        var allMine = await baseQuery
            .Select(p => new
            {
                p.StrikeLevel,
                p.Status,
                p.SurchargePercent,
                p.SessionsTotal,
                p.SessionsUsed,
                p.BanUntil
            })
            .ToListAsync(cancellationToken);

        var notRevoked = allMine.Where(p => p.Status != PenaltyStatus.Revoked).ToList();
        // "Đang phải chịu" = còn phụ phí hoặc còn khoá bán. Consumed nghĩa là đã trừ hết số
        // phiên, không còn hiệu lực — nhưng KHÔNG bị thu hồi, nên tách riêng khỏi Active.
        var serving = allMine.Where(p => p.Status == PenaltyStatus.Active).ToList();
        var consumed = allMine.Count(p => p.Status == PenaltyStatus.Consumed);

        // Mức đang đứng lấy theo bậc cao nhất chưa bị thu hồi: thang phạt cộng dồn và chỉ tăng,
        // nên lần vi phạm tiếp theo sẽ tính từ bậc này.
        var currentLevel = notRevoked.Count == 0 ? 0 : notRevoked.Max(p => p.StrikeLevel);
        var activeSurcharge = serving
            .Where(p => p.SurchargePercent.HasValue
                        && p.SessionsUsed < p.SessionsTotal)
            .Select(p => p.SurchargePercent!.Value)
            .DefaultIfEmpty(0)
            .Max();
        var remainingSessions = serving
            .Where(p => p.SessionsTotal > p.SessionsUsed)
            .Sum(p => Math.Max(p.SessionsTotal - p.SessionsUsed, 0));
        var bannedUntil = serving
            .Where(p => p.BanUntil.HasValue && p.BanUntil.Value > now)
            .Select(p => (DateTimeOffset?)p.BanUntil!.Value)
            .Max();

        var summary = new MyPenaltySummaryDto(
            CurrentLevel: currentLevel,
            ActiveSurchargePercent: activeSurcharge,
            SurchargeSessionsRemaining: remainingSessions,
            BannedUntil: bannedUntil,
            ActivePenaltyCount: serving.Count,
            ConsumedPenaltyCount: consumed,
            RevokedPenaltyCount: allMine.Count - notRevoked.Count);

        var query = baseQuery;

        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!PenaltyCursor.TryDecode(request.Cursor, out var cursorCreatedAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(p => p.CreatedAt < cursorCreatedAt
                                     || (p.CreatedAt == cursorCreatedAt && p.Id.CompareTo(cursorId) > 0));
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit + 1)
            .Select(p => new
            {
                p.Id,
                p.StrikeLevel,
                p.Status,
                p.SurchargePercent,
                p.SessionsTotal,
                p.SessionsUsed,
                p.BanUntil,
                p.EscrowReleasedAtStrike,
                p.Reason,
                p.SourceAuctionId,
                p.SourceDeliveryId,
                p.RevokedAt,
                p.RevokeReason,
                p.CreatedAt,
                ArtworkTitle = p.Auction != null && p.Auction.Artwork != null
                    ? p.Auction.Artwork.Title
                    : null,
                ArtworkThumbnailUrl = p.Auction != null && p.Auction.Artwork != null
                    ? (p.Auction.Artwork.ThumbnailUrl ?? p.Auction.Artwork.ImageUrl)
                    : null
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        // Ngữ cảnh khiếu nại lấy từ đơn giao nhận nguồn: lý do người mua nêu, ghi chú phân xử
        // và số bằng chứng — đây là phần người bán cần đọc để biết bị báo cáo vì cái gì.
        var deliveryIds = page.Where(p => p.SourceDeliveryId.HasValue)
            .Select(p => p.SourceDeliveryId!.Value)
            .Distinct()
            .ToList();

        var deliveries = await _db.Set<ArtCommission.Domain.Entities.Auction.AuctionDelivery>()
            .AsNoTracking()
            .Where(d => deliveryIds.Contains(d.Id))
            .Select(d => new
            {
                d.Id,
                d.DisputeReason,
                d.DisputeResolvedNote,
                d.DisputeResolution,
                d.DisputeEvidenceUrl,
                d.DisputeImageUrls,
                d.DisputeVideoUrl
            })
            .ToDictionaryAsync(d => d.Id, cancellationToken);

        var items = page.Select(p =>
        {
            deliveries.TryGetValue(p.SourceDeliveryId ?? Guid.Empty, out var delivery);

            var images = CountImages(delivery?.DisputeImageUrls, delivery?.DisputeEvidenceUrl);

            return new MyAuctionPenaltyDto(
                PenaltyId: p.Id,
                StrikeLevel: p.StrikeLevel,
                Status: p.Status.ToString(),
                SurchargePercent: p.SurchargePercent,
                SessionsTotal: p.SessionsTotal,
                SessionsUsed: p.SessionsUsed,
                BanUntil: p.BanUntil,
                EscrowReleasedAtStrike: p.EscrowReleasedAtStrike,
                Reason: p.Reason,
                BuyerComplaint: delivery?.DisputeReason,
                AdminNote: delivery?.DisputeResolvedNote,
                DisputeResolution: delivery?.DisputeResolution,
                EvidenceImageCount: images,
                HasEvidenceVideo: !string.IsNullOrWhiteSpace(delivery?.DisputeVideoUrl),
                SourceAuctionId: p.SourceAuctionId,
                SourceDeliveryId: p.SourceDeliveryId,
                ArtworkTitle: p.ArtworkTitle,
                ArtworkThumbnailUrl: p.ArtworkThumbnailUrl,
                RevokedAt: p.RevokedAt,
                RevokeReason: p.RevokeReason,
                CreatedAt: p.CreatedAt);
        }).ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? PenaltyCursor.Encode(last.CreatedAt, last.Id)
            : null;

        return (true, items, new { cursor = nextCursor, nextCursor, total, count = items.Count, summary }, []);
    }

    /// <summary>
    /// Đếm số ảnh bằng chứng: cột mới lưu JSON danh sách, cột cũ chỉ có 1 URL (có thể phân
    /// tách bằng dấu phẩy). Gộp cả hai và loại trùng để con số khớp với màn quản trị.
    /// </summary>
    private static int CountImages(string? imageUrlsJson, string? legacyUrl)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(imageUrlsJson))
        {
            var trimmed = imageUrlsJson.Trim();
            if (trimmed.StartsWith('['))
            {
                try
                {
                    var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(trimmed);
                    foreach (var url in parsed ?? [])
                    {
                        if (!string.IsNullOrWhiteSpace(url)) urls.Add(url.Trim());
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // Cột hỏng/định dạng lạ: bỏ qua, vẫn còn URL cũ để đếm.
                }
            }
            else
            {
                foreach (var url in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    urls.Add(url);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(legacyUrl))
        {
            foreach (var url in legacyUrl.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                urls.Add(url);
            }
        }

        return urls.Count;
    }
}
