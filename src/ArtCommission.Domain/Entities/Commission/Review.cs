using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Commission;

public class Review : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid ReviewerId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewerReply { get; set; }
    public bool IsVisible { get; set; } = true;

    // Navigation property
    public Commission Commission { get; set; } = null!;
}
