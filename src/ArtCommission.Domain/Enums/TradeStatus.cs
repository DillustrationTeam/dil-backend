using System.Text.Json.Serialization;

namespace ArtCommission.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TradeStatus
{
    Proposed = 1,
    Accepted = 2,
    BothSubmitted = 3,
    Completed = 4,
    Rejected = 5,
    Cancelled = 6,
    Disputed = 7
}

