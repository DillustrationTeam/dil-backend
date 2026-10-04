using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArtCommission.API.Hubs;

[Authorize]
public sealed class AuctionHub : Hub
{
    public Task JoinAuction(Guid auctionId)
    {
        if (auctionId == Guid.Empty)
        {
            throw new HubException("Mã phiên đấu giá không hợp lệ.");
        }

        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(auctionId));
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
