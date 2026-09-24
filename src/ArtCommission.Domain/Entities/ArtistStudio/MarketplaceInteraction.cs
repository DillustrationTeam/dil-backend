using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class ArtworkFavorite
{
    public Guid UserId { get; set; }
    public Guid ArtworkId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Artwork? Artwork { get; set; }
}

public class ArtworkComment : BaseEntity
{
    public Guid ArtworkId { get; set; }
    public Guid UserId { get; set; }
    public string Body { get; set; } = string.Empty;
    public Artwork? Artwork { get; set; }
}

public class PersonalCollection : BaseEntity
{
    public Guid OwnerUserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public ICollection<CollectionArtwork> CollectionArtworks { get; set; } = new List<CollectionArtwork>();
}

public class CollectionArtwork
{
    public Guid CollectionId { get; set; }
    public Guid ArtworkId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public PersonalCollection? Collection { get; set; }
    public Artwork? Artwork { get; set; }
}

public class CommissionService : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal StartingPrice { get; set; }
    public int DeliveryDays { get; set; }
    public int MaxRevisions { get; set; }
    public bool IsActive { get; set; } = true;
    public CreatorProfile? CreatorProfile { get; set; }
}

public class CreatorReview : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public Guid ReviewerUserId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}
