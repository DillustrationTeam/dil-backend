namespace ArtCommission.Application.Event.DTOs;

public sealed record EventStatsDto
{
    public long TotalPrizePool { get; init; }
    public string FormattedTotalPrizePool { get; init; } = string.Empty;
    public int OpenEventsCount { get; init; }
    public int TotalSubmissionsCount { get; init; }
    public int TotalEventsCount { get; init; }
}
