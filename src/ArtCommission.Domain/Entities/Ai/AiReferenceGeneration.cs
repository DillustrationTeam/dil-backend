using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;

namespace ArtCommission.Domain.Entities.Ai;

public class AiReferenceGeneration : BaseEntity
{
    public Guid ClientId { get; set; }
    public Guid CreatorProfileId { get; set; }
    public string BriefText { get; set; } = string.Empty;
    public string? LayoutHint { get; set; }
    public string? VariantsJson { get; set; }
    public string Model { get; set; } = string.Empty;
    public AiReferenceGenerationStatus Status { get; set; } = AiReferenceGenerationStatus.Pending;
    public int ProgressPercent { get; set; }
    public string? BlockReason { get; set; }
    public string? Error { get; set; }
    public long? DurationMs { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<AiReferenceImage> Images { get; set; } = [];
}
