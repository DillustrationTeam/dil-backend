using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Event;

public class EventSubmission 
{ 
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }

    public Guid SubmitterId { get; set; }

    public Guid ArtworkId { get; set; }

    public bool AiScanPassed { get; set; } = false;

    public int VoteCount { get; set; } = 0;

    public Decimal? Score { get; set; }

    public string? AdminNote { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.Now;

    public PlatformEvent? Event { get; set; }

    public ApplicationUser? Submitter { get; set; }

    public Artwork? Artwork { get; set; }

    public ICollection<EventVote> Votes { get; set; } = new List<EventVote>();
}