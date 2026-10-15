namespace ArtCommission.Application.Commission.DTOs;
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
