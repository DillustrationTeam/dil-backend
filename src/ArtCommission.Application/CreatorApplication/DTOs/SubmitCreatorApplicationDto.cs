namespace ArtCommission.Application.CreatorApplication.DTOs;

public sealed record SubmitCreatorApplicationDto
{
    public string? PrimaryStyle { get; set; }
    public List<string> PortfolioLinks { get; set; } = new();
    public string? SpeedpaintVideoUrl { get; set; }
    public List<string>? SocialLinks { get; set; } = new();
    public string IdProofUrl { get; set; } = string.Empty;
}
