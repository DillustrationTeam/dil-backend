using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Trade;

namespace ArtCommission.Domain.Entities.Dispute;

public class TradeDispute : BaseEntity
{
    public Guid ArtTradeId { get; set; }
    public Guid RaisedById { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // Navigation property
    public ArtTrade ArtTrade { get; set; } = null!;
}

