using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class ClientProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public DateTimeOffset? UsernameChangedAt { get; set; }
    public List<string> InterestTags { get; set; } = new();
    public string? Country { get; set; }
    public string? Timezone { get; set; }
    public List<string> PreferredLanguages { get; set; } = new();

    public ApplicationUser? User { get; set; }
}
