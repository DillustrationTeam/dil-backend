namespace ArtCommission.Infrastructure.ExternalServices.ArtworkAi;
public class ArtworkAiOptions
{
    public string VisionModel { get; set; } = "gemini-3.5-flash-lite";
    public string VisionFallbackModel { get; set; } = "gemini-3.1-flash-lite";
    public string SightengineApiUser { get; set; } = "";
    public string SightengineApiSecret { get; set; } = "";
    // Exact hostnames of the application's own public artwork storage. Never accept arbitrary URLs.
    public string[] AllowedImageHosts { get; set; } = ["res.cloudinary.com"];
}
