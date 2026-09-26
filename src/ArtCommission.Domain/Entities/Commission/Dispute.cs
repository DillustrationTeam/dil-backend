using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Commission;

public class Dispute : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid RaisedById { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, UnderReview, Resolved
    public string? Resolution { get; set; } // ClientWin100, ArtistWin100, SplitCustom
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }

    // Navigation property
    public Commission Commission { get; set; } = null!;
}
