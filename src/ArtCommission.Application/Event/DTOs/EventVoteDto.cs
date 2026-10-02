namespace ArtCommission.Application.Event.DTOs;

public sealed record EventVoteDto
{
    public Guid SubmissionId { get; set; }

    public Guid VoterId { get; set; }

    public string? VoterName { get; set; }

    public DateTimeOffset VotedAt { get; set; }
}
