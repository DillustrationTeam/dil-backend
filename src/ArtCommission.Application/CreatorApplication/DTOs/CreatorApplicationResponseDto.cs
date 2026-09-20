namespace ArtCommission.Application.CreatorApplication.DTOs;

public class CreatorApplicationResponseDto 
{
    public Guid Id { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string ApplicantUsername { get; set; } = string.Empty;
    public string ApplicantEmail { get; set; } = string.Empty;
    public string? PrimaryStyle { get; set; }
    public List<string> PortfolioLinks { get; set; } = new List<string>();
    public string? SpeedpaintVideoUrl { get; set; }
    public List<string>? SocialLinks { get; set; }
    public string IdProofUrl { get; set; } = string.Empty;
    public bool IsNationalIdVerified { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? ReviewedByModId { get; set; }
    public string? ReviewedByModName { get; set; }
    public string? ReviewNote { get; set; }
    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? ReviewedAt { get; set; }
}