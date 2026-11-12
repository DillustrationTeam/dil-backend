using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Trade;

public class TradeSlotFreeze : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ArtTradeId { get; set; }
    public DateTimeOffset FrozenFrom { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FrozenUntil { get; set; }
    public string? Reason { get; set; }
    public bool IsRevoked { get; set; }
    public string? RevokedReason { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedByAdminId { get; set; }

    // Navigation Properties
    public ApplicationUser? User { get; set; }
    public ArtTrade? ArtTrade { get; set; }
    public ApplicationUser? RevokedByAdmin { get; set; }
}

