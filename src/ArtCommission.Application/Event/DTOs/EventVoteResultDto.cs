namespace ArtCommission.Application.Event.DTOs;
public sealed record EventVoteResultDto(
    Guid SubmissionId,
    Guid VoterId,
    DateTimeOffset VotedAt,
    int UpdatedVoteCount,
    bool IsVoted
);
