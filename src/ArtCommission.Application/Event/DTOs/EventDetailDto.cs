namespace ArtCommission.Application.Event.DTOs;

public sealed record EventDetailDto(
    Guid Id,
    string Title,
    string? BannerUrl,
    string Description,
    string? Rules,
    string? Prize,
    string Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndsAt,
    Guid CreatedByAdminId,
    string? CreatedByAdminName,
    string? CreatedByAdminAvatarUrl,
    int SubmissionCount,
    int TotalVoteCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsOpenForSubmission
);
