namespace ArtCommission.Application.Event.DTOs;
public sealed record EventVoteDto(
    Guid SubmissionId,
    Guid VoterId,
    string? VoterName,
    DateTimeOffset VotedAt
);
