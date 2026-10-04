using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Event;

public class EventCriteria : BaseEntity
{
    public Guid EventId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MaxScore { get; set; } = 10.00m;

    public decimal Weight { get; set; }

    public int DisplayOrder { get; set; } = 0;

    public PlatformEvent? Event { get; set; }
    
    public ICollection<CriteriaScore> CriteriaScores { get; set; } = new List<CriteriaScore>();
}
