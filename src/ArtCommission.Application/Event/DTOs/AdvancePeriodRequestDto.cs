namespace ArtCommission.Application.Event.DTOs;

public class AdvancePeriodRequestDto
{
    /// <summary>
    /// Giai đoạn mục tiêu muốn chuyển tới: "submission", "judging", "voting", "ended", hoặc null/empty để chuyển sang giai đoạn kế tiếp.
    /// </summary>
    public string? TargetPeriod { get; set; }
}
