using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class CreatorTerms : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public decimal CommercialLicenseMultiplier { get; set; } = 1.5m;
    public string? RevisionPolicy { get; set; }
    public string? CancellationPolicy { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}

public class CreatorAutoReplySetting : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public bool IsEnabled { get; set; }
    public string BriefTemplate { get; set; } = string.Empty;
    public CreatorProfile? CreatorProfile { get; set; }
}

public class CreatorFaq : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}

public class CreatorWorkItem : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = "PendingBrief";
    public DateTimeOffset? DueAt { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}

public class CreatorAsset : BaseEntity
{
    public Guid CreatorProfileId { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? AssetUrl { get; set; }
    public string? MetadataJson { get; set; }
    public CreatorProfile? CreatorProfile { get; set; }
}
