using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.ExternalServices.Common;

/// <summary>
/// Dịch bằng nhà cung cấp CHÍNH (Google Translate), thất bại thì tự chuyển sang DỰ PHÒNG (Gemini).
///
/// VÌ SAO cần lớp này: endpoint công khai của Google có thể bị chặn theo mạng/region, còn
/// Cloud Translation chính thức có thể hết quota. Nếu chỉ có một nhà cung cấp thì tính năng
/// dịch của Workroom chết hẳn; có dự phòng thì sự cố chỉ còn là "chậm hơn một nhịp".
/// </summary>
public class FallbackTranslationClient : ITranslationClient
{
    private readonly ITranslationClient _primary;
    private readonly ITranslationClient _fallback;
    private readonly ILogger<FallbackTranslationClient> _logger;

    public FallbackTranslationClient(
        ITranslationClient primary,
        ITranslationClient fallback,
        ILogger<FallbackTranslationClient> logger)
    {
        _primary = primary;
        _fallback = fallback;
        _logger = logger;
    }

    public async Task<AiCompletionResult> TranslateAsync(
        string text,
        string targetLang,
        CancellationToken cancellationToken = default)
    {
        var primary = await _primary.TranslateAsync(text, targetLang, cancellationToken);
        if (primary.Success)
        {
            return primary;
        }

        _logger.LogWarning(
            "Dịch bằng nhà cung cấp chính thất bại ({Error}); chuyển sang dự phòng.",
            primary.Error);

        var fallback = await _fallback.TranslateAsync(text, targetLang, cancellationToken);
        if (fallback.Success)
        {
            return fallback;
        }

        return AiCompletionResult.Fail(
            $"Chính: {primary.Error} | Dự phòng: {fallback.Error}");
    }
}
