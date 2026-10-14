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
