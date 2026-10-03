using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Auction;

/// <summary>Trần proxy-bid hiện hành của một người trong một phiên.</summary>
public class AuctionAutoBid : BaseEntity
{
    public Guid AuctionId { get; set; }
    public Guid BidderId { get; set; }
    public decimal MaxAmount { get; set; }

    /// <summary>Giữ nguyên khi người dùng thay trần để giải quyết hòa trần ổn định.</summary>
    public DateTimeOffset RegisteredAt { get; set; }

    public Auction? Auction { get; set; }
}
