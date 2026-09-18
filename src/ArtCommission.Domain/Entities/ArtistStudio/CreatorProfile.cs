using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class CreatorProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Bio { get; set; }
    public string? RateCard { get; set; }
    public int CommissionSlots { get; set; }
    public decimal RatingAvg { get; set; }
    public bool IsAiVerified { get; set; }

    public ApplicationUser? User { get; set; }
    public ICollection<Artwork> Artworks { get; set; } = [];
}
