namespace ArtCommission.Application.CreatorApplication.DTOs;

public sealed record SubmitCreatorApplicationDto
{
    public string? PrimaryStyle { get; init; }
    public List<string> PortfolioLinks { get; init; } = new();
    public string? SpeedpaintVideoUrl { get; init; }
    public List<string>? SocialLinks { get; init; } = new();
    public string IdProofUrl { get; init; } = string.Empty;
}
