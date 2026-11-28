using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class SearchPublicVouchersTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "search_public_vouchers";

    public string Description =>
        "Searches or lists active, platform-wide public discount vouchers by optional keyword, scope, discountType, or orderAmount.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            keyword = new
            {
                type = "string",
                description = "Optional keyword matching voucher code or name"
            },
            scope = new
            {
                type = "string",
                @enum = new[] { "commission", "auction", "deposit", "any" },
                description = "Optional voucher scope filter"
            },
            discountType = new
            {
                type = "string",
                @enum = new[] { "percent", "fixed", "any" },
                description = "Optional discount type filter"
            },
            orderAmount = new
            {
                type = "number",
                description = "Optional order amount (VND); returns vouchers where MinOrderAmount <= orderAmount"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        var keyword = args.TryGetProperty("keyword", out var kw) && kw.ValueKind == JsonValueKind.String
            ? kw.GetString()?.Trim().ToLower()
            : null;

        var scope = args.TryGetProperty("scope", out var sc) && sc.ValueKind == JsonValueKind.String
            ? sc.GetString()?.Trim().ToLowerInvariant()
            : "any";

        var discountType = args.TryGetProperty("discountType", out var dt) && dt.ValueKind == JsonValueKind.String
            ? dt.GetString()?.Trim().ToLowerInvariant()
            : "any";

        decimal? orderAmount = args.TryGetProperty("orderAmount", out var oa) && oa.ValueKind == JsonValueKind.Number && oa.TryGetDecimal(out var parsedOa)
            ? parsedOa
            : null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = db.Vouchers
            .AsNoTracking()
            .Where(v => !v.IsDeleted
                && v.IsActive
                && v.CreatedByUserId == null
                && v.StartDate <= today
                && v.EndDate >= today);

        query = scope switch
        {
            "commission" => query.Where(v => (v.Scope & VoucherScope.Commission) != 0),
            "auction" => query.Where(v => (v.Scope & VoucherScope.Auction) != 0),
            "deposit" => query.Where(v => (v.Scope & VoucherScope.Deposit) != 0),
            _ => query
        };

        query = discountType switch
        {
            "percent" => query.Where(v => v.DiscountType == DiscountType.Percent),
            "fixed" => query.Where(v => v.DiscountType == DiscountType.Fixed),
            _ => query
        };

        if (orderAmount.HasValue)
        {
            query = query.Where(v => v.MinOrderAmount <= orderAmount.Value);
        }

        if (!string.IsNullOrEmpty(keyword))
        {
            query = query.Where(v =>
                v.VoucherCode.ToLower().Contains(keyword)
                || (v.Name != null && v.Name.ToLower().Contains(keyword)));
        }

        return await query
            .OrderBy(v => v.EndDate)
            .Take(10)
            .Select(v => new
            {
                v.VoucherCode,
                url = "/vouchers",
                v.Name,
                discountType = v.DiscountType.ToString(),
                v.DiscountValue,
                v.MinOrderAmount,
                v.MaxDiscountAmount,
                scope = v.Scope.ToString(),
                v.StartDate,
                v.EndDate
            })
            .ToListAsync(cancellationToken);
    }
}
