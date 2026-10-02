using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Event.DTOs;

public sealed record CreateEventDto
{
    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? BannerUrl { get; set; }

    public string? Rules { get; set; }

    public string? Prize { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Draft;

    public DateTimeOffset StartAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }
}
