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

        if (request.Packages is null || request.Packages.Count > 10 || request.Packages.Any(p =>
            p is null || string.IsNullOrWhiteSpace(p.Name) || p.Name.Length > 100 || p.Description?.Length > 2000 ||
            p.Milestones is null || p.Milestones.Count is < 1 or > 10 ||
            p.Milestones.Any(m => m is null || string.IsNullOrWhiteSpace(m.Title) || m.Title.Length > 200 ||
                m.Price <= 0 || decimal.Truncate(m.Price) != m.Price || m.Price > 1000000000000m || m.MaxRevisions is < 0 or > 20) ||
            !p.Milestones.Select(m => m.Sequence).SequenceEqual(Enumerable.Range(1, p.Milestones.Count)) ||
            p.Price != p.Milestones.Sum(m => m.Price)))
            return (false, null, new[] { "Gói cần 1–10 mốc, giá VND nguyên dương, tổng giá đúng và 0–20 lần sửa mỗi mốc." });

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
