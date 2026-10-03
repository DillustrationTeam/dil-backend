namespace ArtCommission.Application.CreatorApplication.DTOs;

public class SubmitCreatorApplicationDto
{
    public string? PrimaryStyle { get; set; }
    public List<string> PortfolioLinks { get; set; } = new List<string>();
    public List<string>? SpeedpaintVideoUrls { get; set; } = new List<string>();
    public List<string>? SocialLinks { get; set; } = new List<string>();
    public string IdProofUrl { get; set; } = string.Empty;
    public string IdProofBackUrl { get; set; } = string.Empty;
}
