namespace ArtCommission.Application.CreatorApplication.DTOs;

public class SubmitCreatorApplicationDto
{
    public List<string> PortfolioLinks { get; set; } = new List<string>();
    public List<string>? SocialLinks { get; set; } = new List<string>();
    public string IdProofUrl { get; set; } = string.Empty;
}
