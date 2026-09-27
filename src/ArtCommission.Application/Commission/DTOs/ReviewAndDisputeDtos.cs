namespace ArtCommission.Application.Commission.DTOs;

public class ReviewDto
{
    public Guid Id { get; set; }
    public Guid CommissionId { get; set; }
    public Guid ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string? ReviewerReply { get; set; }
    public List<string>? AttachedImages { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class DisputeDto
{
    public Guid Id { get; set; }
    public Guid CommissionId { get; set; }
    public Guid RaisedById { get; set; }
    public string? RaisedByName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Resolution { get; set; }
    public decimal? ClientRefundAmount { get; set; }
    public decimal? ArtistPayAmount { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
