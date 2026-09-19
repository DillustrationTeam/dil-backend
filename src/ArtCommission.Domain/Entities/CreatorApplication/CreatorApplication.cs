using ArtCommission.Domain.Common;
using ArtCommission.Domain.Enums;
using ArtCommission.Domain.Entities.Identity;

namespace ArtCommission.Domain.Entities.CreatorApplication;

public class CreatorApplication : BaseEntity
{
    public Guid ApplicantId { get; set; }
    public List<string> PortfolioLinks { get; set; } = new List<string>();
    public List<string>? SocialLinks { get; set; } = new List<string>();
    public string IdProofUrl { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Pending;
    public Guid? ReviewedByModId { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ReviewedAt { get; set; }
    public ApplicationUser? Applicant { get; set; }
    public ApplicationUser? ReviewedByMod { get; set; }
}