namespace ArtCommission.Application.Event.DTOs;

public sealed record EventCriteriaRequestDto
{
    public Guid? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal MaxScore { get; set; } = 10.0m;

    public decimal Weight { get; set; } = 1.0m;

    public int DisplayOrder { get; set; }
}
