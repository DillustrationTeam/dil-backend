using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.Ai;

public class AiReferenceImage : BaseEntity
{
    public Guid GenerationId { get; set; }
    public int VariantIndex { get; set; }
    public string TempStorageKey { get; set; } = string.Empty;
    public string? PermanentUrl { get; set; }
    public string LabelVi { get; set; } = string.Empty;
    public string LabelEn { get; set; } = string.Empty;
    public Guid? CommissionId { get; set; }

    public AiReferenceGeneration? Generation { get; set; }
}
