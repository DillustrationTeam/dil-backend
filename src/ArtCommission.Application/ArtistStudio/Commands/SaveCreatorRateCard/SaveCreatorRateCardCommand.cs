using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Commands.SaveCreatorRateCard;

public record SaveRateCardRequest(List<RateCardPackageDto> Packages);

public record SaveCreatorRateCardCommand(Guid UserId, List<RateCardPackageDto> Packages)
    : IRequest<(bool Success, List<RateCardPackageDto>? Data, string[] Errors)>;

public class SaveCreatorRateCardCommandHandler
    : IRequestHandler<SaveCreatorRateCardCommand, (bool Success, List<RateCardPackageDto>? Data, string[] Errors)>
{
    private readonly IApplicationDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public SaveCreatorRateCardCommandHandler(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<(bool Success, List<RateCardPackageDto>? Data, string[] Errors)> Handle(SaveCreatorRateCardCommand request, CancellationToken cancellationToken)
    {
        var profile = await _db.Set<Domain.Entities.ArtistStudio.CreatorProfile>()
            .FirstOrDefaultAsync(x => x.UserId == request.UserId && !x.IsDeleted, cancellationToken);

        if (profile is null)
        {
            return (false, null, new[] { "Creator profile not found. Please setup your profile first." });
        }

        try
        {
            profile.RateCardJson = JsonSerializer.Serialize(request.Packages ?? new List<RateCardPackageDto>(), JsonOptions);
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            return (true, request.Packages, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            return (false, null, new[] { $"Failed to save rate card: {ex.Message}" });
        }
    }
}
