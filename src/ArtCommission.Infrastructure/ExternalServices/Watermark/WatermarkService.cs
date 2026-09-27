using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Commission.Interfaces;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;

namespace ArtCommission.Infrastructure.ExternalServices.Watermark;

public class WatermarkService : IWatermarkService
{
    public async Task<Stream> ApplyWatermarkAsync(Stream imageStream, string watermarkText, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(imageStream, cancellationToken);

        // 1. Resize ảnh preview vừa đủ nét (Max width 1920px)
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(1920, 0),
            Mode = ResizeMode.Max
        }));

        // 2. Vẽ Watermark chéo lặp lại khắp bức ảnh
        var font = SystemFonts.CreateFont("Arial", 32, FontStyle.Bold);

        image.Mutate(ctx =>
        {
            // Phủ mờ chữ nghiêng 45 độ
            for (int y = 0; y < image.Height; y += 300)
            {
                for (int x = -200; x < image.Width; x += 400)
                {
                    ctx.DrawText(
                        watermarkText,
                        font,
                        Color.FromRgba(255, 255, 255, 80), // Độ mờ Alpha = 80/255 (~30%)
                        new PointF(x, y)
                    );
                }
            }
        });

        // 3. Export ra MemoryStream
        var outputStream = new MemoryStream();
        await image.SaveAsJpegAsync(outputStream, cancellationToken);
        outputStream.Position = 0;

        return outputStream;
    }
}
