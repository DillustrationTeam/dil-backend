using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using System.Text;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;

public record RateCardMilestoneDto(int Sequence, string Title, decimal Price);
public record RateCardPackageDto(Guid Id, string Name, string? Description, decimal Price, List<RateCardMilestoneDto> Milestones);

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
            return (true, packages, Array.Empty<string>());
        }
        catch
        {
            return (true, new List<RateCardPackageDto>(), Array.Empty<string>());
        }
    }
}
