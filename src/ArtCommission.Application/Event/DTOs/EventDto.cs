namespace ArtCommission.Application.Event.DTOs;

public sealed record EventDto(
    Guid Id,
    string Title,
    string? BannerUrl,
    string Description,
    string? Prize,
    string Status, 
    DateTimeOffset StartAt,
    DateTimeOffset EndsAt,
    Guid CreatedByAdminId,
    string? CreatedByAdminName,
    int SubmissionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    bool IsActive
);
