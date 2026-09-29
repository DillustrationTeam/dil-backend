using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.Event;

public class EventVote
{
    public Guid SubmissionId { get; set; }
    public Guid VoterId { get; set; }
    public DateTimeOffset VotedAt { get; set; } = DateTimeOffset.Now;
    public EventSubmission? Submission { get; set; }
    public ApplicationUser? Voter { get; set; }
}