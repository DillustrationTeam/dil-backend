namespace ArtCommission.Application.CreatorApplication.DTOs;

public sealed record CreatorApplicationResponseDto 
{
    public Guid Id { get; init; }
    public string ApplicantName { get; init; } = string.Empty;
    public string ApplicantUsername { get; init; } = string.Empty;
    public string ApplicantEmail { get; init; } = string.Empty;
    public string? PrimaryStyle { get; init; }
    public List<string> PortfolioLinks { get; init; } = new();
    public string? SpeedpaintVideoUrl { get; init; }
    public List<string>? SocialLinks { get; init; }
    public string IdProofUrl { get; init; } = string.Empty;
    public bool IsNationalIdVerified { get; init; }
    public string Status { get; init; } = string.Empty;
    public Guid? ReviewedByModId { get; init; }
    public string? ReviewedByModName { get; init; }
    public string? ReviewNote { get; init; }
    public DateTimeOffset SubmittedAt { get; init; } = DateTimeOffset.Now;
    public DateTimeOffset? ReviewedAt { get; init; }
}