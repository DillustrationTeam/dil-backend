using ArtCommission.Domain.Common;

namespace ArtCommission.Domain.Entities.ArtistStudio;

public class Tag : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsAiGenerated { get; set; }
    public ICollection<ArtworkTag> ArtworkTags { get; set; } = new List<ArtworkTag>();
}
