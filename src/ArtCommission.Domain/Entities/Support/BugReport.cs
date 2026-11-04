using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Support;

public enum BugReportStatus
{
    Open,
    InProgress,
    Resolved
}

public class BugReport : BaseEntity
{
    public Guid? ReporterId { get; set; }
    public string ReporterEmail { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public BugReportStatus Status { get; set; } = BugReportStatus.Open;
    public string? AdminNote { get; set; }
}
