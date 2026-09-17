using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class Follow
{
    public Guid FollowerUserId { get; set; }
    public Guid CreatorProfileId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ApplicationUser? Follower { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}
