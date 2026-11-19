using ArtCommission.Application.Auction.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Auction.Queries.GetAuctionPenalties;

/// <summary>
/// Danh sách án phạt giao hàng cho Ban quản trị (màn "Vận hành đấu giá → Án phạt").
///
/// VÌ SAO CẦN: thang phạt 5 mức đã chạy (tự áp khi phân xử khiếu nại) nhưng trước đây
/// không có màn nào xem lại, cũng không có đường thu hồi khi Admin xử nhầm — người bán
/// bị trừ 30–50% phí hoặc bị khoá bán mà không ai gỡ được.
///
/// Chỉ Admin gọi được (controller chặn). Trả kèm thông tin người bán, phiên nguồn và
/// trạng thái hiệu lực để Admin quyết định thu hồi.
/// </summary>
public sealed record GetAuctionPenaltiesQuery(
    bool IsAdmin,
    string? Status = null,
    Guid? SellerId = null,
    int? MinLevel = null,
    string? Cursor = null,
    int Limit = 20
) : IRequest<(bool Success, IReadOnlyList<AuctionPenaltyAdminDto>? Data, object? Meta, string[] Errors)>;

public sealed class GetAuctionPenaltiesQueryHandler
    : IRequestHandler<GetAuctionPenaltiesQuery, (bool, IReadOnlyList<AuctionPenaltyAdminDto>?, object?, string[])>
{
    private const int MaxLimit = 100;

    private readonly IApplicationDbContext _db;

    public GetAuctionPenaltiesQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool, IReadOnlyList<AuctionPenaltyAdminDto>?, object?, string[])> Handle(
        GetAuctionPenaltiesQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.IsAdmin)
        {
            return (false, null, null, ["Chỉ Ban quản trị mới xem được danh sách án phạt."]);
        }

        var limit = request.Limit <= 0 ? 20 : Math.Min(request.Limit, MaxLimit);

        var query = _db.AuctionPenalties
            .AsNoTracking()
            .Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.Status)
            && !string.Equals(request.Status, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (!Enum.TryParse<PenaltyStatus>(request.Status, ignoreCase: true, out var status))
            {
                return (false, null, null, [$"Trạng thái án phạt không hợp lệ: {request.Status}."]);
            }

            query = query.Where(p => p.Status == status);
        }

        if (request.SellerId.HasValue)
        {
            query = query.Where(p => p.SellerId == request.SellerId.Value);
        }

        if (request.MinLevel.HasValue)
        {
            query = query.Where(p => p.StrikeLevel >= request.MinLevel.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        // Cursor theo (CreatedAt, Id) — danh sách phạt nhỏ nhưng vẫn phân trang để không
        // tải cả bảng khi dữ liệu tích tụ nhiều năm.
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            if (!PenaltyCursor.TryDecode(request.Cursor, out var cursorCreatedAt, out var cursorId))
            {
                return (false, null, null, ["Cursor không hợp lệ."]);
            }

            query = query.Where(p => p.CreatedAt < cursorCreatedAt
                                     || (p.CreatedAt == cursorCreatedAt && p.Id.CompareTo(cursorId) > 0));
        }

        var rows = await query
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .Take(limit + 1)
            .Select(p => new
            {
                p.Id,
                p.SellerId,
                p.StrikeLevel,
                p.Status,
                p.SurchargePercent,
                p.SessionsTotal,
                p.SessionsUsed,
                p.BanUntil,
                p.EscrowReleasedAtStrike,
                p.SourceAuctionId,
                p.SourceDeliveryId,
                p.Reason,
                p.RevokedAt,
                p.RevokedByAdminId,
                p.RevokeReason,
                p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var page = hasMore ? rows.Take(limit).ToList() : rows;

        var sellerIds = page.Select(p => p.SellerId).Distinct().ToList();
        var adminIds = page.Where(p => p.RevokedByAdminId.HasValue)
            .Select(p => p.RevokedByAdminId!.Value).Distinct().ToList();

        var users = await _db.Users
            .AsNoTracking()
            .Where(u => sellerIds.Contains(u.Id) || adminIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.Email })
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var items = page.Select(p => new AuctionPenaltyAdminDto(
            PenaltyId: p.Id,
            SellerId: p.SellerId,
            SellerName: users.TryGetValue(p.SellerId, out var seller) ? seller.FullName : null,
            SellerEmail: users.TryGetValue(p.SellerId, out var seller2) ? seller2.Email : null,
            StrikeLevel: p.StrikeLevel,
            Status: p.Status.ToString(),
            SurchargePercent: p.SurchargePercent,
            SessionsTotal: p.SessionsTotal,
            SessionsUsed: p.SessionsUsed,
            BanUntil: p.BanUntil,
            EscrowReleasedAtStrike: p.EscrowReleasedAtStrike,
            SourceAuctionId: p.SourceAuctionId,
            SourceDeliveryId: p.SourceDeliveryId,
            Reason: p.Reason,
            RevokedAt: p.RevokedAt,
            RevokedByAdminName: p.RevokedByAdminId.HasValue && users.TryGetValue(p.RevokedByAdminId.Value, out var admin)
                ? admin.FullName
                : null,
            RevokeReason: p.RevokeReason,
            CreatedAt: p.CreatedAt)).ToList();

        var last = page.LastOrDefault();
        var nextCursor = hasMore && last is not null
            ? PenaltyCursor.Encode(last.CreatedAt, last.Id)
            : null;

        return (true, items, new { cursor = nextCursor, nextCursor, total, count = items.Count }, []);
    }
}

/// <summary>Cursor keyset cho danh sách án phạt (CreatedAt, Id).</summary>
public static class PenaltyCursor
{
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        var payload = string.Join('|',
            createdAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            id.ToString("N"));
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload));
    }

    public static bool TryDecode(string cursor, out DateTimeOffset createdAt, out Guid id)
    {
        createdAt = default;
        id = Guid.Empty;
        try
        {
            var raw = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            var parts = raw.Split('|');
            if (parts.Length != 2)
            {
                return false;
            }

            if (!DateTimeOffset.TryParseExact(parts[0], "O",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out createdAt))
            {
                return false;
            }

            return Guid.TryParseExact(parts[1], "N", out id);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
