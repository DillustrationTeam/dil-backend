using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Workstation;

public record GetCreatorWorkstationQuery(Guid UserId) : IRequest<CreatorWorkstationStateDto?>;
public record GetCreatorFaqsQuery(Guid UserId) : IRequest<IReadOnlyList<CreatorFaqDto>>;
public record GetCreatorWorkItemsQuery(Guid UserId) : IRequest<IReadOnlyList<CreatorWorkItemDto>>;
public record GetCreatorAssetsQuery(Guid UserId, string? AssetType) : IRequest<IReadOnlyList<CreatorAssetDto>>;
public record CreatorWorkstationStateDto(CreatorTermsDto Terms, AutoReplySettingDto AutoReply, IReadOnlyList<CreatorFaqDto> Faqs, IReadOnlyList<CreatorWorkItemDto> WorkItems, IReadOnlyList<CreatorAssetDto> Assets);

public class CreatorWorkstationQueryHandler : IRequestHandler<GetCreatorWorkstationQuery, CreatorWorkstationStateDto?>, IRequestHandler<GetCreatorFaqsQuery, IReadOnlyList<CreatorFaqDto>>, IRequestHandler<GetCreatorWorkItemsQuery, IReadOnlyList<CreatorWorkItemDto>>, IRequestHandler<GetCreatorAssetsQuery, IReadOnlyList<CreatorAssetDto>>
{
    private readonly IApplicationDbContext _db; public CreatorWorkstationQueryHandler(IApplicationDbContext db) => _db = db;
    private async Task<Guid?> ProfileId(Guid userId, CancellationToken ct) => await _db.CreatorProfiles.Where(x => x.UserId == userId && !x.IsDeleted).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    private static CreatorFaqDto Faq(CreatorFaq x) => new(x.Id, x.Question, x.Answer, x.DisplayOrder);
    private static CreatorWorkItemDto Work(CreatorWorkItem x) => new(x.Id, x.ClientName, x.Title, x.Stage, x.DueAt, x.CreatedAt);
    private static CreatorAssetDto Asset(CreatorAsset x) => new(x.Id, x.AssetType, x.Name, x.AssetUrl, x.MetadataJson, x.CreatedAt);
    public async Task<IReadOnlyList<CreatorFaqDto>> Handle(GetCreatorFaqsQuery q, CancellationToken ct) { var id = await ProfileId(q.UserId, ct); return id is null ? Array.Empty<CreatorFaqDto>() : await _db.CreatorFaqs.AsNoTracking().Where(x => x.CreatorProfileId == id && !x.IsDeleted).OrderBy(x => x.DisplayOrder).Select(x => new CreatorFaqDto(x.Id, x.Question, x.Answer, x.DisplayOrder)).ToListAsync(ct); }
    public async Task<IReadOnlyList<CreatorWorkItemDto>> Handle(GetCreatorWorkItemsQuery q, CancellationToken ct) { var id = await ProfileId(q.UserId, ct); return id is null ? Array.Empty<CreatorWorkItemDto>() : await _db.CreatorWorkItems.AsNoTracking().Where(x => x.CreatorProfileId == id && !x.IsDeleted).OrderBy(x => x.Stage).ThenBy(x => x.DueAt).Select(x => new CreatorWorkItemDto(x.Id, x.ClientName, x.Title, x.Stage, x.DueAt, x.CreatedAt)).ToListAsync(ct); }
    public async Task<IReadOnlyList<CreatorAssetDto>> Handle(GetCreatorAssetsQuery q, CancellationToken ct) { var id = await ProfileId(q.UserId, ct); return id is null ? Array.Empty<CreatorAssetDto>() : await _db.CreatorAssets.AsNoTracking().Where(x => x.CreatorProfileId == id && !x.IsDeleted && (q.AssetType == null || x.AssetType == q.AssetType)).OrderByDescending(x => x.CreatedAt).Select(x => new CreatorAssetDto(x.Id, x.AssetType, x.Name, x.AssetUrl, x.MetadataJson, x.CreatedAt)).ToListAsync(ct); }
    public async Task<CreatorWorkstationStateDto?> Handle(GetCreatorWorkstationQuery q, CancellationToken ct)
    {
        var id = await ProfileId(q.UserId, ct); if (id is null) return null;
        var terms = await _db.CreatorTerms.AsNoTracking().FirstOrDefaultAsync(x => x.CreatorProfileId == id && !x.IsDeleted, ct);
        var autoReply = await _db.CreatorAutoReplySettings.AsNoTracking().FirstOrDefaultAsync(x => x.CreatorProfileId == id && !x.IsDeleted, ct);
        return new CreatorWorkstationStateDto(terms is null ? new CreatorTermsDto(1.5m, null, null) : new CreatorTermsDto(terms.CommercialLicenseMultiplier, terms.RevisionPolicy, terms.CancellationPolicy), autoReply is null ? new AutoReplySettingDto(false, string.Empty) : new AutoReplySettingDto(autoReply.IsEnabled, autoReply.BriefTemplate), await Handle(new GetCreatorFaqsQuery(q.UserId), ct), await Handle(new GetCreatorWorkItemsQuery(q.UserId), ct), await Handle(new GetCreatorAssetsQuery(q.UserId, null), ct));
    }
}

public record CreatorWorkstationMutationCommand(string Action, Guid UserId, Guid TargetId, object? Payload = null) : IRequest<(bool Success, object? Data, string[] Errors)>;
public class CreatorWorkstationMutationHandler : IRequestHandler<CreatorWorkstationMutationCommand, (bool Success, object? Data, string[] Errors)>
{
    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal) { "PendingBrief", "InProgress", "Review", "Completed" };
    private readonly IApplicationDbContext _db; public CreatorWorkstationMutationHandler(IApplicationDbContext db) => _db = db;
    private async Task<CreatorProfile?> Profile(Guid userId, CancellationToken ct) => await _db.CreatorProfiles.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);
    public async Task<(bool Success, object? Data, string[] Errors)> Handle(CreatorWorkstationMutationCommand c, CancellationToken ct)
    {
        var profile = await Profile(c.UserId, ct); if (profile is null) return (false, null, new[] { "Creator profile is required." });
        switch (c.Action)
        {
            case "terms":
                if (c.Payload is not UpdateCreatorTermsRequest terms || terms.CommercialLicenseMultiplier is < 1m or > 10m) return (false, null, new[] { "Commercial license multiplier must be between 1 and 10." });
                var entity = await _db.CreatorTerms.FirstOrDefaultAsync(x => x.CreatorProfileId == profile.Id && !x.IsDeleted, ct) ?? new CreatorTerms { CreatorProfileId = profile.Id };
                entity.CommercialLicenseMultiplier = terms.CommercialLicenseMultiplier; entity.RevisionPolicy = terms.RevisionPolicy?.Trim(); entity.CancellationPolicy = terms.CancellationPolicy?.Trim(); if (entity.Id == Guid.Empty) _db.CreatorTerms.Add(entity); await _db.SaveChangesAsync(ct); return (true, new CreatorTermsDto(entity.CommercialLicenseMultiplier, entity.RevisionPolicy, entity.CancellationPolicy), Array.Empty<string>());
            case "auto-reply":
                if (c.Payload is not UpdateAutoReplySettingRequest reply || reply.BriefTemplate.Length > 2000) return (false, null, new[] { "Auto-reply template is invalid." });
                var setting = await _db.CreatorAutoReplySettings.FirstOrDefaultAsync(x => x.CreatorProfileId == profile.Id && !x.IsDeleted, ct) ?? new CreatorAutoReplySetting { CreatorProfileId = profile.Id };
                setting.IsEnabled = reply.IsEnabled; setting.BriefTemplate = reply.BriefTemplate.Trim(); if (setting.Id == Guid.Empty) _db.CreatorAutoReplySettings.Add(setting); await _db.SaveChangesAsync(ct); return (true, new AutoReplySettingDto(setting.IsEnabled, setting.BriefTemplate), Array.Empty<string>());
            case "faq":
                if (c.Payload is not UpsertCreatorFaqRequest faq || string.IsNullOrWhiteSpace(faq.Question) || string.IsNullOrWhiteSpace(faq.Answer)) return (false, null, new[] { "FAQ question and answer are required." });
                var newFaq = new CreatorFaq { CreatorProfileId = profile.Id, Question = faq.Question.Trim(), Answer = faq.Answer.Trim(), DisplayOrder = faq.DisplayOrder }; _db.CreatorFaqs.Add(newFaq); await _db.SaveChangesAsync(ct); return (true, new CreatorFaqDto(newFaq.Id, newFaq.Question, newFaq.Answer, newFaq.DisplayOrder), Array.Empty<string>());
            case "work-item":
                if (c.Payload is not CreateWorkItemRequest item || string.IsNullOrWhiteSpace(item.ClientName) || string.IsNullOrWhiteSpace(item.Title) || !Stages.Contains(item.Stage)) return (false, null, new[] { "Work item details or stage are invalid." });
                var work = new CreatorWorkItem { CreatorProfileId = profile.Id, ClientName = item.ClientName.Trim(), Title = item.Title.Trim(), Stage = item.Stage, DueAt = item.DueAt }; _db.CreatorWorkItems.Add(work); await _db.SaveChangesAsync(ct); return (true, new CreatorWorkItemDto(work.Id, work.ClientName, work.Title, work.Stage, work.DueAt, work.CreatedAt), Array.Empty<string>());
            case "work-stage":
                if (c.Payload is not UpdateWorkItemStageRequest stage || !Stages.Contains(stage.Stage)) return (false, null, new[] { "Work item stage is invalid." });
                var existing = await _db.CreatorWorkItems.FirstOrDefaultAsync(x => x.Id == c.TargetId && x.CreatorProfileId == profile.Id && !x.IsDeleted, ct); if (existing is null) return (false, null, new[] { "Work item not found." }); existing.Stage = stage.Stage; existing.UpdatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); return (true, new CreatorWorkItemDto(existing.Id, existing.ClientName, existing.Title, existing.Stage, existing.DueAt, existing.CreatedAt), Array.Empty<string>());
            case "asset":
                if (c.Payload is not CreateCreatorAssetRequest asset || string.IsNullOrWhiteSpace(asset.Name) || !new[] { "Pose", "Palette", "Preset" }.Contains(asset.AssetType)) return (false, null, new[] { "Asset type, name, or data is invalid." });
                var newAsset = new CreatorAsset { CreatorProfileId = profile.Id, AssetType = asset.AssetType, Name = asset.Name.Trim(), AssetUrl = asset.AssetUrl, MetadataJson = asset.MetadataJson }; _db.CreatorAssets.Add(newAsset); await _db.SaveChangesAsync(ct); return (true, new CreatorAssetDto(newAsset.Id, newAsset.AssetType, newAsset.Name, newAsset.AssetUrl, newAsset.MetadataJson, newAsset.CreatedAt), Array.Empty<string>());
            case "delete-asset":
                var delete = await _db.CreatorAssets.FirstOrDefaultAsync(x => x.Id == c.TargetId && x.CreatorProfileId == profile.Id && !x.IsDeleted, ct); if (delete is null) return (false, null, new[] { "Asset not found." }); delete.IsDeleted = true; delete.UpdatedAt = DateTimeOffset.UtcNow; await _db.SaveChangesAsync(ct); return (true, null, Array.Empty<string>());
            default: return (false, null, new[] { "Unsupported creator workstation action." });
        }
    }
}
