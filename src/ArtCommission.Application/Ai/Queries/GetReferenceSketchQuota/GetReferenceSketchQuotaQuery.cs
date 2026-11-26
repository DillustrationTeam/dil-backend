using System.Globalization;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Queries.GetReferenceSketchQuota;

public sealed record ReferenceSketchQuotaDto(bool Enabled, int Used, int Limit, DateTimeOffset? ResetsAt, decimal DiscountPercent);

/// <summary>
/// GET /api/v1/ai/reference-sketches/quota — khách xem còn bao nhiêu lượt AI phác thảo
/// bố cục trong 24h hiện tại, và creator đã bật tính năng hay chưa.
/// </summary>
public sealed record GetReferenceSketchQuotaQuery(Guid ClientId, Guid CreatorProfileId)
    : IRequest<(bool Success, ReferenceSketchQuotaDto? Data, string[] Errors)>;

public class GetReferenceSketchQuotaQueryHandler
    : IRequestHandler<GetReferenceSketchQuotaQuery, (bool Success, ReferenceSketchQuotaDto? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;

    public GetReferenceSketchQuotaQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, ReferenceSketchQuotaDto? Data, string[] Errors)> Handle(
        GetReferenceSketchQuotaQuery request,
        CancellationToken cancellationToken)
    {
        var terms = await _db.CreatorTerms.AsNoTracking()
            .FirstOrDefaultAsync(t => t.CreatorProfileId == request.CreatorProfileId && !t.IsDeleted, cancellationToken);
        var enabled = terms?.AllowAiReferenceSketch ?? false;

        var windowStart = DateTimeOffset.UtcNow - ReferenceSketchLimits.QuotaWindow;
        var succeededCreatedAt = await _db.AiReferenceGenerations.AsNoTracking()
            .Where(g =>
                g.ClientId == request.ClientId &&
                g.CreatorProfileId == request.CreatorProfileId &&
                g.Status == AiReferenceGenerationStatus.Succeeded &&
                g.CreatedAt >= windowStart)
            .OrderBy(g => g.CreatedAt)
            .Select(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

        var resetsAt = succeededCreatedAt.Count > 0
            ? succeededCreatedAt[0] + ReferenceSketchLimits.QuotaWindow
            : (DateTimeOffset?)null;

        // Giới hạn lượt + % giảm giá — admin cấu hình tại /admin/fee-policy (PlatformConfig),
        // fallback về mặc định (ReferenceSketchLimits.MaxGenerationsPerWindow / 0%) nếu chưa cấu hình.
        var aiConfigKeys = new[]
        {
            PlatformConfigKeys.AiReferenceSketchMaxGenerationsPerWindow,
            PlatformConfigKeys.AiReferenceSketchDiscountPercent
        };
        var aiConfigs = await _db.PlatformConfigs.AsNoTracking()
            .Where(c => aiConfigKeys.Contains(c.Key) && !c.IsDeleted)
            .ToListAsync(cancellationToken);

        var limit = ReferenceSketchLimits.MaxGenerationsPerWindow;
        var limitValue = aiConfigs.FirstOrDefault(c => c.Key == PlatformConfigKeys.AiReferenceSketchMaxGenerationsPerWindow)?.Value;
        if (limitValue is not null && int.TryParse(limitValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedLimit))
        {
            limit = parsedLimit;
        }

        var discountPercent = 0m;
        var discountValue = aiConfigs.FirstOrDefault(c => c.Key == PlatformConfigKeys.AiReferenceSketchDiscountPercent)?.Value;
        if (discountValue is not null && decimal.TryParse(discountValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedDiscount))
        {
            discountPercent = parsedDiscount;
        }

        var dto = new ReferenceSketchQuotaDto(
            enabled,
            succeededCreatedAt.Count,
            limit,
            resetsAt,
            discountPercent);

        return (true, dto, Array.Empty<string>());
    }
}
