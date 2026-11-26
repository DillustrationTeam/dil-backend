namespace ArtCommission.Application.Ai.ReferenceSketch;

/// <summary>
/// Danh sách kỹ thuật bố cục dùng cho nút "🎲 Gợi ý bố cục độc đáo" — mỗi lần gọi chọn ngẫu
/// nhiên 3 kỹ thuật khác nhau để Gemini viết 1 gợi ý riêng cho mỗi kỹ thuật, tránh lặp ý.
/// </summary>
public static class LayoutHintTechniques
{
    public static readonly string[] All =
    [
        "worm's-eye view",
        "bird's-eye view",
        "Dutch angle",
        "frame-within-frame",
        "reflection",
        "silhouette/backlight",
        "negative space",
        "foreground occlusion",
        "leading lines",
        "over-the-shoulder",
        "extreme close-up",
        "ultra-wide establishing shot",
        "symmetry break",
        "forced perspective",
    ];

    public static string[] PickRandom(int count, Random? random = null)
    {
        random ??= Random.Shared;
        return All.OrderBy(_ => random.Next()).Take(count).ToArray();
    }
}
