using System.Text.Json;

namespace ArtCommission.Application.Auction.Risk;

/// <summary>
/// Reads the human sentence out of <c>AuctionRiskSignal.EvidenceJson</c>.
///
/// Evidence is stored as <c>{"signal":"…","evidence":"…"}</c>. One shared reader for every path
/// (GET sheet, review/claim response) so a sheet never shows raw JSON on one endpoint and a sentence
/// on another - and so legacy rows written with escaped <c>\uXXXX</c> are decoded on read without a
/// data migration.
/// </summary>
public static class RiskEvidence
{
    public static string Extract(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("evidence", out var evidence)
                ? evidence.GetString() ?? string.Empty
                : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
