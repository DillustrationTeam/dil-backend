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

    public int MaxVote { get; set; } = 1;

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTimeOffset SubmissionStartAt { get; set; }

    public DateTimeOffset SubmissionEndAt { get; set; }

    public DateTimeOffset JudgingStartAt { get; set; }

    public DateTimeOffset JudgingEndAt { get; set; }

    public DateTimeOffset VotingStartAt { get; set; }

    public DateTimeOffset VotingEndAt { get; set; }

    public DateTimeOffset ResultAnnouncementAt { get; set; }

    public Guid CreatedByAdminId { get; set; }

    public ApplicationUser? CreatedByAdmin { get; set; }

    public ICollection<EventSubmission> Submissions { get; set; } = new List<EventSubmission>();
    
    public ICollection<EventCriteria> Criteria { get; set; } = new List<EventCriteria>();

    public ICollection<Jury> Juries { get; set; } = new List<Jury>();

    public ICollection<Invitation> Invitations { get; set; } = new List<Invitation>();
}
