namespace ArtCommission.Application.Event.DTOs;

public sealed record EventVoteDto
{
    public Guid SubmissionId { get; set; }

    public string? SubmissionTitle { get; set; }

    public Guid? EventId { get; set; }

    public string? EventTitle { get; set; }

    public Guid? ArtworkId { get; set; }

    public string? ArtworkImageUrl { get; set; }

    public Guid VoterId { get; set; }

    public string? VoterName { get; set; }

    public string? VoterUsername { get; set; }

    public DateTimeOffset VotedAt { get; set; }
}
