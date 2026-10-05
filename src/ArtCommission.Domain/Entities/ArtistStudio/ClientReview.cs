using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class ClientReview : BaseEntity
{
    public Guid CommissionId { get; set; }
    public Guid CreatorId { get; set; }
    public Guid ClientId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }

    public ApplicationUser? Creator { get; set; }
    public ApplicationUser? Client { get; set; }
}
