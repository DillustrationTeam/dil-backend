using ArtCommission.Application.Ai.Common;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ArtCommission.Infrastructure.ExternalServices.OpenAi;

/// <summary>
/// Bản giả của <see cref="IAiImageClient"/>, dùng khi chưa cấu hình <c>OpenAi:ApiKey</c>
/// (dev, e2e, demo). Vẽ ảnh mẫu bằng ImageSharp — KHÔNG gọi OpenAI, không tốn tiền,
/// không phụ thuộc mạng.
///
/// Delay ~2s để mô phỏng thời gian sinh ảnh thật, nhờ vậy luồng tiến trình % phía trên
/// (UC AI phác thảo bố cục) có gì đó để hiển thị khi test bằng client này.
/// </summary>
public class FakeAiImageClient : IAiImageClient
{
    private static int _callCounter;

    private readonly ILogger<FakeAiImageClient> _logger;

    public FakeAiImageClient(ILogger<FakeAiImageClient> logger)
    {
        _logger = logger;
    }

    public async Task<AiImageResult> GenerateAsync(
        string prompt, string size, CancellationToken cancellationToken = default)
    {
        await Task.Delay(2000, cancellationToken);

        var (width, height) = ParseSize(size);

        // Luân phiên 2 biến thể để 2 ảnh "sinh song song" trong 1 lượt nhìn khác nhau,
        // giống việc 2 phương án bố cục thật phải khác pose/camera/composition.
        var variant = Interlocked.Increment(ref _callCounter) % 2;

        using var image = new Image<Rgba32>(width, height);

        // Nền phẳng một màu nhạt theo phong cách cố định (mục Quyết định thiết kế).
        var background = variant == 0
            ? new Rgba32(224, 238, 236) // xanh ngọc nhạt
            : new Rgba32(226, 230, 236); // xanh xám nhạt
        var inkNavy = new Rgba32(41, 55, 82); // xanh than, nét chì mảnh

        var margin = width * 0.12f;
        var box = new RectangularPolygon(margin, margin, width - margin * 2, height - margin * 2);

        image.Mutate(ctx =>
        {
            ctx.Fill(background);
            ctx.Draw(Pens.Solid(inkNavy, 2f), box);

            if (variant == 0)
            {
                ctx.DrawLine(inkNavy, 2f,
                    new PointF(width / 2f, margin + 20),
                    new PointF(width / 2f, height - margin - 20));
            }
            else
            {
                ctx.DrawLine(inkNavy, 2f,
                    new PointF(margin + 20, height / 2f),
                    new PointF(width - margin - 20, height / 2f));
            }

            DrawSampleLabel(ctx, width, height, inkNavy);
        });

        using var output = new MemoryStream();
        await image.SaveAsPngAsync(output, cancellationToken);

        _logger.LogInformation(
            "FakeAiImageClient sinh ảnh mẫu (biến thể {Variant}), không gọi OpenAI.", variant);

        return AiImageResult.Ok(output.ToArray(), "fake-ai-image-client");
    }

    /// <summary>
    /// Chữ "SAMPLE SKETCH" chỉ để phân biệt rõ đây KHÔNG phải ảnh AI thật. Bỏ qua chữ
    /// (vẫn trả ảnh khung hợp lệ) nếu môi trường không có font hệ thống nào — container
    /// Linux tối giản trên Render có thể không cài font, không nên làm cả tính năng vỡ
    /// chỉ vì thiếu font trang trí.
    /// </summary>
    private void DrawSampleLabel(IImageProcessingContext ctx, int width, int height, Rgba32 inkNavy)
    {
        FontFamily fontFamily;
        try
        {
            if (!SystemFonts.Collection.Families.Any())
            {
                return;
            }

            fontFamily = SystemFonts.Collection.Families.First();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Không tìm được font hệ thống, bỏ qua chữ mẫu trên ảnh fake.");
            return;
        }

        var font = fontFamily.CreateFont(Math.Max(18f, width * 0.035f), FontStyle.Bold);
        var options = new RichTextOptions(font)
        {
            Origin = new PointF(width / 2f, height - (height * 0.08f)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        ctx.DrawText(options, "SAMPLE SKETCH", inkNavy);
    }

    private static (int Width, int Height) ParseSize(string size)
    {
        var parts = size.Split(['x', 'X']);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var width)
            && int.TryParse(parts[1], out var height)
            && width > 0 && height > 0)
        {
            return (width, height);
        }

        return (1024, 1536);
    }
}
