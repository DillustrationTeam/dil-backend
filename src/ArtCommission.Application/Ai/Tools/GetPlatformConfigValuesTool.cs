using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class GetPlatformConfigValuesTool(IApplicationDbContext db) : IAiTool
{
    private static readonly string[] FeeKeys =
    [
        PlatformConfigKeys.PlatformFeePercent,
        PlatformConfigKeys.PlatformFeeRateSqlKey,
        PlatformConfigKeys.AuctionHoldPercent,
        PlatformConfigKeys.AiReferenceSketchDiscountPercent
    ];

    private static readonly string[] LimitKeys =
    [
        PlatformConfigKeys.MinPayoutAmount,
        PlatformConfigKeys.PayoutEnabled,
        PlatformConfigKeys.MilestoneAutoApprovalDays,
        PlatformConfigKeys.EscrowHoldDaysSqlKey,
        PlatformConfigKeys.DefaultFreeRevisionLimit,
        PlatformConfigKeys.MaxRevisionCountSqlKey,
        PlatformConfigKeys.AuctionPaymentWindowHours,
        PlatformConfigKeys.AiReferenceSketchMaxGenerationsPerWindow
    ];

    public string Name => "get_platform_config_values";

    public string Description =>
        "Gets allowlisted public platform fee percentages and operational limits/deadlines.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            topic = new
            {
                type = "string",
                @enum = new[] { "fees", "limits", "all" },
                description = "Optional topic filter ('fees', 'limits', or 'all')"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var topic = args.TryGetProperty("topic", out var t) && t.ValueKind == JsonValueKind.String
            ? t.GetString()?.Trim().ToLowerInvariant()
            : "all";

        var allowedKeys = topic switch
        {
            "fees" => FeeKeys,
            "limits" => LimitKeys,
            _ => [.. FeeKeys, .. LimitKeys]
        };

        return await db.PlatformConfigs
            .AsNoTracking()
            .Where(c => !c.IsDeleted && allowedKeys.Contains(c.Key))
            .OrderBy(c => c.Key)
            .Select(c => new
            {
                c.Key,
                c.Value,
                c.Description
            })
            .ToListAsync(cancellationToken);
    }
}
