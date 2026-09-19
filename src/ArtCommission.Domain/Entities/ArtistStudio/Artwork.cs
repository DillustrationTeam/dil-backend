using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class Artwork : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string? Style { get; set; }
    public ArtworkStatus Status { get; set; } = ArtworkStatus.Published;

    public CreatorProfile? Creator { get; set; }
    public ICollection<ArtworkTag> ArtworkTags { get; set; } = [];
}
