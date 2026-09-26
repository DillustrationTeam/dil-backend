namespace ArtCommission.Application.ArtistStudio.DTOs;

public record CreatorTermsDto(decimal CommercialLicenseMultiplier, string? RevisionPolicy, string? CancellationPolicy);
public record UpdateCreatorTermsRequest(decimal CommercialLicenseMultiplier, string? RevisionPolicy, string? CancellationPolicy);
public record AutoReplySettingDto(bool IsEnabled, string BriefTemplate);
public record UpdateAutoReplySettingRequest(bool IsEnabled, string BriefTemplate);
public record CreatorFaqDto(Guid Id, string Question, string Answer, int DisplayOrder);
public record UpsertCreatorFaqRequest(string Question, string Answer, int DisplayOrder);
public record CreatorWorkItemDto(Guid Id, string ClientName, string Title, string Stage, DateTimeOffset? DueAt, DateTimeOffset CreatedAt);
public record CreateWorkItemRequest(string ClientName, string Title, string Stage, DateTimeOffset? DueAt);
public record UpdateWorkItemStageRequest(string Stage);
public record CreatorAssetDto(Guid Id, string AssetType, string Name, string? AssetUrl, string? MetadataJson, DateTimeOffset CreatedAt);
public record CreateCreatorAssetRequest(string AssetType, string Name, string? AssetUrl, string? MetadataJson);
