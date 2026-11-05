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
    public string Reason { get; set; } = string.Empty;
    public string? EvidenceUrls { get; set; }
    public List<string> EvidenceUrlList { get; set; } = new();
    public string Status { get; set; } = "Pending";
    public string? Resolution { get; set; }
    public string? AdminNote { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public class CreateTradeDisputeRequest
{
    public Guid ArtTradeId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public List<string>? EvidenceUrls { get; set; }
}

public class ResolveTradeDisputeRequest
{
    public string Resolution { get; set; } = string.Empty;
    public string? AdminNote { get; set; }
}

public static class TradeDisputeResolutions
{
    public const string FaultInitiator = "FaultInitiator";
    public const string FaultReceiver = "FaultReceiver";
    public const string MutualCancel = "MutualCancel";
    public const string Dismissed = "Dismissed";

    public static readonly string[] All = [FaultInitiator, FaultReceiver, MutualCancel, Dismissed];
}

