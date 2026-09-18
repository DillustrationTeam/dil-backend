namespace ArtCommission.Domain.Entities.ArtistStudio;

public class ArtworkTag
{
    public Guid ArtworkId { get; set; }
    public Guid TagId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Artwork? Artwork { get; set; }
    public Tag? Tag { get; set; }
}
