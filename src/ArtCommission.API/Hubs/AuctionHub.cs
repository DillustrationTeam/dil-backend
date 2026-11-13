using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Hubs;

/// <summary>
/// Hub realtime cho từng phiên đấu giá (`/hubs/auctions`).
///
/// LUẬT: chỉ được vào group của một phiên CÓ THẬT, chưa bị xoá mềm và chưa bị huỷ.
/// Trước đây chỉ chặn Guid.Empty nên bất kỳ ai đăng nhập cũng join được group của phiên
/// bất kỳ (kể cả phiên đã huỷ hoặc GUID dò được) và nhận BidPlaced kèm BidderId/holdAmount,
/// AuctionExtended, AuctionStatusChanged.
///
/// Cách xử lý khi phiên không hợp lệ: IM LẶNG không thêm vào group (không ném lỗi) — ném
/// HubException sẽ làm FE hiện toast lỗi ở những trang hợp lệ như phiên vừa bị huỷ, trong khi
/// việc cần làm chỉ là không phát dữ liệu cho người không liên quan.
/// </summary>
[Authorize]
public sealed class AuctionHub : Hub
{
    private readonly IApplicationDbContext _db;

    public AuctionHub(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task JoinAuction(Guid auctionId)
    {
        if (auctionId == Guid.Empty)
        {
            throw new HubException("Mã phiên đấu giá không hợp lệ.");
        }

        var canJoin = await _db.Auctions
            .AsNoTracking()
            .AnyAsync(a => a.Id == auctionId
                           && !a.IsDeleted
                           && a.Status != AuctionStatus.Cancelled);

        if (!canJoin)
        {
            // Không có dữ liệu gì để lộ nếu không join; ngắt im lặng để FE không văng lỗi.
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(auctionId));
    }

    public Task LeaveAuction(Guid auctionId)
    {
        if (auctionId == Guid.Empty)
        {
            throw new HubException("Mã phiên đấu giá không hợp lệ.");
        }

        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(auctionId));
    }

    public static string GroupName(Guid auctionId) => $"auction:{auctionId:D}";
}
