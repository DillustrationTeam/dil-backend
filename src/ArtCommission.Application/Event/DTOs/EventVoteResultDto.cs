namespace ArtCommission.Application.Event.DTOs;

public sealed record EventVoteResultDto
{
    public Guid SubmissionId { get; set; }

    public Guid VoterId { get; set; }

    public DateTimeOffset VotedAt { get; set; }

    public int UpdatedVoteCount { get; set; }

    public bool IsVoted { get; set; }
}
