using ArtCommission.Domain.Enums;

namespace ArtCommission.Application.Dispute.TradeDispute.DTOs;

public class TradeDisputeDto
{
    public Guid Id { get; set; }
    public Guid ArtTradeId { get; set; }
    public string? TradeTitle { get; set; }
    public Guid RaisedById { get; set; }
    public string? RaisedByName { get; set; }
    public Guid PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public TradeDisputeCategory? Category { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public List<string> EvidenceUrlList { get; set; } = new();
    public string Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed record CreateTradeDisputeRequest(
    Guid ArtTradeId,
    string Reason,
    string? Category = null,
    List<string>? EvidenceUrls = null
);

public sealed record ResolveTradeDisputeRequest(
    string Resolution,
    string? AdminNote = null
);

public static class TradeDisputeResolutions
{
    public const string FaultInitiator = "FaultInitiator";
    public const string FaultReceiver = "FaultReceiver";
    public const string MutualCancel = "MutualCancel";
    public const string Dismissed = "Dismissed";

    public static readonly string[] All = [FaultInitiator, FaultReceiver, MutualCancel, Dismissed];
}

