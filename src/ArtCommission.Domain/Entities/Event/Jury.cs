using ArtCommission.Domain.Common;
using ArtCommission.Domain.Entities.ArtistStudio;

namespace ArtCommission.Domain.Entities.Event;

public class Jury : BaseEntity
{
    public Guid EventId { get; set; }

    public Guid CreatorId { get; set; }

    public bool IsHeadJury { get; set; }

    public PlatformEvent? Event { get; set; }

    public CreatorProfile? Creator { get; set; }
    
    public ICollection<CriteriaScore> CriteriaScores { get; set; } = new List<CriteriaScore>();
}
