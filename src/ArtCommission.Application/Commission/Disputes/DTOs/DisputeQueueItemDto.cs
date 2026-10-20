namespace ArtCommission.Application.Commission.Disputes.DTOs;

/// <summary>
/// DTO biểu diễn một vụ tranh chấp trong hàng đợi kiểm duyệt trọng tài (SCR-22 / UC30).
/// </summary>
public record DisputeQueueItemDto
{
    public Guid DisputeId { get; init; }
    public Guid CommissionId { get; init; }
    public string CommissionTitle { get; init; } = string.Empty;

    public Guid RaisedById { get; init; }
    public string RaisedByName { get; init; } = string.Empty;
    public string RaisedByRole { get; init; } = string.Empty; // "Client" hoặc "Creator"

    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public string ClientEmail { get; init; } = string.Empty;

    public Guid CreatorId { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public string CreatorEmail { get; init; } = string.Empty;

    public decimal FinalPrice { get; init; }
    public decimal EscrowHeldAmount { get; init; }

    public string Reason { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending"; // Pending, UnderReview, Resolved
    public string? Resolution { get; init; } // ClientWin100, ArtistWin100, SplitCustom

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
}
