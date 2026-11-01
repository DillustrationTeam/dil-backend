namespace ArtCommission.Application.Event.DTOs;

public sealed record JuryProgressItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GradedPercent { get; set; }
    public int CompletedCount { get; set; }
    public int TotalAssigned { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public bool IsHeadJury { get; set; }
    public List<Guid> GradedSubmissionIds { get; set; } = new();
}

public sealed record CombinedScoreItemDto
{
    public int Rank { get; set; }
    public Guid SubmissionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string ArtworkImageUrl { get; set; } = string.Empty;
    public decimal JuryScoreAvg { get; set; }
    public decimal HeadJuryScore { get; set; }
    public decimal DeviationPercent { get; set; }
}

public sealed record ArbitrationCaseDto
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string ArtworkImageUrl { get; set; } = string.Empty;
    public decimal DeviationPercent { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Decision { get; set; }
}

public sealed record WinnerApprovalItemDto
{
    public string AwardName { get; set; } = string.Empty;
    public Guid SubmissionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string ScoreOrVotes { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed record HeadJuryWorkspaceDto
{
    public int TotalSubmissions { get; set; }
    public int GradedSubmissions { get; set; }
    public int CompletedJuryCount { get; set; }
    public int TotalJuryCount { get; set; }
    public int AnomalyFlagsCount { get; set; }
    public int OverallProgressPercent { get; set; }
    public List<JuryProgressItemDto> JuryProgressList { get; set; } = new();
    public List<CombinedScoreItemDto> CombinedScores { get; set; } = new();
    public List<ArbitrationCaseDto> ArbitrationCases { get; set; } = new();
    public List<WinnerApprovalItemDto> FinalWinners { get; set; } = new();
    public bool AllJuryCompleted { get; set; }
    public bool DisputesResolved { get; set; }
    public bool WinnersConfirmed { get; set; }
    public bool IsResultsApproved { get; set; }
}
