using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record CreateEventDto(
    string Title,
    string Description,
    string? BannerUrl,
    string? Rules,
    string? Prize,
    EventStatus Status,
    DateTimeOffset StartAt,
    DateTimeOffset EndsAt
);
