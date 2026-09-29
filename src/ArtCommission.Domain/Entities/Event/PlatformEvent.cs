using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Event;

public class PlatformEvent : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? BannerUrl { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Rules { get; set; }
    public string? Prize { get; set; }
    public EventStatus Status { get; set; } = EventStatus.Draft;
    public DateTimeOffset StartAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public Guid CreatedByAdminId { get; set; }
    public ApplicationUser? CreatedByAdmin { get; set; }
    public ICollection<EventSubmission> Submissions { get; set; } = new List<EventSubmission>();
}
