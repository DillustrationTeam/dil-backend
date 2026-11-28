using System.Text.Json;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.Commission;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.Ai.Tools;

public sealed class GetCreatorServicesTool(IApplicationDbContext db) : IAiTool
{
    public string Name => "get_creator_services";

    public string Description =>
        "Gets an approved creator's active commission packages (optionally filtered by maxPrice or maxDeliveryDays), add-ons, FAQs, rate card, and commission terms by creatorProfileId or displayName.";

    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            creatorProfileId = new
            {
                type = "string",
                description = "Optional GUID of the creator profile"
            },
            displayName = new
            {
                type = "string",
                description = "Optional creator display name to look up"
            },
            maxPrice = new
            {
                type = "number",
                description = "Optional maximum starting price filter (VND)"
            },
            maxDeliveryDays = new
            {
                type = "integer",
                description = "Optional maximum delivery days filter"
            }
        }
    };

    public async Task<object> ExecuteAsync(JsonElement args, CancellationToken cancellationToken = default)
    {
        Guid? profileId = args.TryGetProperty("creatorProfileId", out var idProp)
            && idProp.ValueKind == JsonValueKind.String
            && Guid.TryParse(idProp.GetString(), out var parsedId)
                ? parsedId
                : null;

        var displayName = args.TryGetProperty("displayName", out var dnProp) && dnProp.ValueKind == JsonValueKind.String
            ? dnProp.GetString()?.Trim().ToLower()
            : null;

        decimal? maxPrice = args.TryGetProperty("maxPrice", out var maxP) && maxP.ValueKind == JsonValueKind.Number && maxP.TryGetDecimal(out var parsedMaxP)
            ? parsedMaxP
            : null;

        int? maxDeliveryDays = args.TryGetProperty("maxDeliveryDays", out var maxD) && maxD.ValueKind == JsonValueKind.Number && maxD.TryGetInt32(out var parsedMaxD)
            ? parsedMaxD
            : null;

        if (profileId is null && string.IsNullOrEmpty(displayName))
        {
            return new { error = "Either creatorProfileId or displayName is required." };
        }

        var query = db.CreatorProfiles
            .AsNoTracking()
            .Where(p => p.IsApproved && !p.IsDeleted);

        if (profileId.HasValue)
        {
            query = query.Where(p => p.Id == profileId.Value);
        }
        else
        {
            query = query.Where(p => p.DisplayName.ToLower().Contains(displayName!));
        }

        var creator = await query
            .OrderByDescending(p => p.RatingAverage)
            .Select(p => new
            {
                p.Id,
                p.DisplayName,
                p.IsAcceptingOrders,
                p.AvailableSlots,
                p.RateCardJson
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (creator is null)
        {
            return new { found = false, message = "No approved creator matched." };
        }

        var services = await db.CommissionServices
            .AsNoTracking()
            .Where(s => s.CreatorProfileId == creator.Id
                && s.IsActive
                && !s.IsDeleted
                && (!maxPrice.HasValue || s.StartingPrice <= maxPrice.Value)
                && (!maxDeliveryDays.HasValue || s.DeliveryDays <= maxDeliveryDays.Value))
            .OrderBy(s => s.StartingPrice)
            .Select(s => new
            {
                s.Title,
                s.Description,
                s.StartingPrice,
                s.DeliveryDays,
                s.MaxRevisions
            })
            .ToListAsync(cancellationToken);

        var addons = await db.Set<CreatorAddon>()
            .AsNoTracking()
            .Where(a => a.CreatorId == creator.Id && a.IsActive && !a.IsDeleted)
            .OrderBy(a => a.Price)
            .Select(a => new
            {
                a.Title,
                a.Description,
                a.Price,
                a.AllowedMilestone,
                a.ExtendDays
            })
            .ToListAsync(cancellationToken);

        var terms = await db.CreatorTerms
            .AsNoTracking()
            .Where(t => t.CreatorProfileId == creator.Id && !t.IsDeleted)
            .Select(t => new
            {
                t.CommercialLicenseMultiplier,
                t.RevisionPolicy,
                t.CancellationPolicy,
                t.AllowAiReferenceSketch
            })
            .FirstOrDefaultAsync(cancellationToken);

        var faqs = await db.CreatorFaqs
            .AsNoTracking()
            .Where(f => f.CreatorProfileId == creator.Id && !f.IsDeleted)
            .OrderBy(f => f.DisplayOrder)
            .Select(f => new
            {
                f.Question,
                f.Answer
            })
            .ToListAsync(cancellationToken);

        return new
        {
            found = true,
            creator = new
            {
                creator.Id,
                url = $"/creators/{creator.Id}",
                creator.DisplayName,
                creator.IsAcceptingOrders,
                creator.AvailableSlots,
                creator.RateCardJson
            },
            services,
            addons,
            terms,
            faqs
        };
    }
}
