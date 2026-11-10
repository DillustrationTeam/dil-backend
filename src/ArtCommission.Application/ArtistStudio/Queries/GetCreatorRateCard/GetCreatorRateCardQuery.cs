using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;

public record RateCardMilestoneDto(int Sequence, string Title, decimal Price, int MaxRevisions = 3);
public record RateCardPackageDto(Guid Id, string Name, string? Description, decimal Price, List<RateCardMilestoneDto> Milestones, int MaxRevisions = 3, decimal ExtraRevisionPrice = 0, List<string>? SampleUrls = null, int OrderCount = 0, int? MaxSlots = null, int ActiveOrderCount = 0, int? RemainingSlots = null);

// Older stored packages used slugs. Map them consistently to IDs for display and ordering.
public sealed class RateCardIdConverter : JsonConverter<Guid>
{
    public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString() ?? string.Empty;
        return Guid.TryParse(value, out var id) ? id : new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(value)).AsSpan(0, 16));
    }
    public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}

public record GetCreatorRateCardQuery(Guid Identifier) : IRequest<(bool Success, List<RateCardPackageDto>? Data, string[] Errors)>;

public class GetCreatorRateCardQueryHandler : IRequestHandler<GetCreatorRateCardQuery, (bool Success, List<RateCardPackageDto>? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, Converters = { new RateCardIdConverter() } };

    public GetCreatorRateCardQueryHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, List<RateCardPackageDto>? Data, string[] Errors)> Handle(GetCreatorRateCardQuery request, CancellationToken cancellationToken)
    {
        var profile = await _db.Set<Domain.Entities.ArtistStudio.CreatorProfile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => (x.Id == request.Identifier || x.UserId == request.Identifier) && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return (false, null, new[] { "Creator profile not found." });
        }

        if (string.IsNullOrWhiteSpace(profile.RateCardJson))
        {
            return (true, new List<RateCardPackageDto>(), Array.Empty<string>());
        }

        try
        {
            var packages = JsonSerializer.Deserialize<List<RateCardPackageDto>>(profile.RateCardJson, JsonOptions) ?? new List<RateCardPackageDto>();
            packages = packages.Select(p => p with
            {
                Milestones = p.Milestones is { Count: > 0 }
                    ? p.Milestones
                    : new List<RateCardMilestoneDto> { new(1, "Hoàn thiện (Final)", p.Price, p.MaxRevisions) },
                SampleUrls = p.SampleUrls ?? new List<string>()
            }).ToList();
            if (packages.Count > 0)
            {
                var packageIds = packages.Select(p => (Guid?)p.Id).ToList();
                var counts = await _db.Set<Domain.Entities.Commission.Commission>()
                    .AsNoTracking()
                    .Where(c => !c.IsDeleted && c.CreatorId == profile.Id && c.PackageId != null && packageIds.Contains(c.PackageId))
                    .GroupBy(c => c.PackageId!.Value)
                    .Select(g => new
                    {
                        PackageId = g.Key,
                        TotalCount = g.Count(),
                        ActiveCount = g.Count(c =>
                            c.Status == Domain.Enums.CommissionStatus.InProgress
                            || c.Status == Domain.Enums.CommissionStatus.Delivered
                            || c.Status == Domain.Enums.CommissionStatus.Disputed)
                    })
                    .ToDictionaryAsync(x => x.PackageId, x => x, cancellationToken);

                packages = packages.Select(p =>
                {
                    var stat = counts.GetValueOrDefault(p.Id);
                    var activeCount = stat?.ActiveCount ?? 0;
                    int? remaining = p.MaxSlots.HasValue ? Math.Max(0, p.MaxSlots.Value - activeCount) : null;
                    return p with
                    {
                        OrderCount = stat?.TotalCount ?? 0,
                        ActiveOrderCount = activeCount,
                        RemainingSlots = remaining
                    };
                }).ToList();
            }
            return (true, packages, Array.Empty<string>());
        }
        catch
        {
            return (true, new List<RateCardPackageDto>(), Array.Empty<string>());
        }
    }
}
