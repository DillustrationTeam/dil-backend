namespace ArtCommission.Application.Event.DTOs;

public sealed record CriteriaScoreItemDto
{
    public Guid? CriteriaId { get; set; }
    public string? CriteriaName { get; set; }
    public decimal Score { get; set; }
}

public sealed record SubmitScoreDto
{
    public Guid SubmissionId { get; set; }
    public decimal TotalScore { get; set; }
    public string? Notes { get; set; }
    public bool NoConflictOfInterest { get; set; } = true;
    public bool NominateSpecialAward { get; set; } = false;
    public string? SpecialAwardCategory { get; set; }
    public List<CriteriaScoreItemDto> CriteriaScores { get; set; } = new();
}

public sealed record SubmissionScoreResultDto
{
    public Guid SubmissionId { get; set; }
    public decimal Score { get; set; }
    public decimal? TotalAverageScore { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset GradedAt { get; set; }
    public bool IsLocked { get; set; }
}

public sealed record EventCriteriaDto
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal MaxScore { get; set; } = 10.0m;
    public decimal Weight { get; set; } = 1.0m;
    public int DisplayOrder { get; set; }
}
