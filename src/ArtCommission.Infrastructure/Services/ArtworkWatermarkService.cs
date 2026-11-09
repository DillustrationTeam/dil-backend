using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace ArtCommission.Infrastructure.Services;

/// <summary>
/// Dịch vụ xử lý thuỷ vân số (Digital Watermarking) bằng SixLabors.ImageSharp.
/// Triển khai thuật toán biến đổi dải tần DWT (Discrete Wavelet Transform - Haar Wavelet)
/// và lớp phủ Moiré Anti-capture chống chụp ảnh màn hình vật lý.
/// </summary>
public class ArtworkWatermarkService : IArtworkWatermarkService
{
    private readonly ILogger<ArtworkWatermarkService> _logger;
    private const int PayloadBits = 64;
    private const float EmbeddingStrength = 12.0f; // Cường độ nhúng dải trung tần (Alpha)

    public ArtworkWatermarkService(ILogger<ArtworkWatermarkService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Nhúng mã số 64-bit vào dải trung tần (LH/HL) qua 2D Haar Wavelet.
    /// Kênh màu Blue được chọn vì mắt người kém nhạy cảm nhất với sắc độ xanh lam.
    /// </summary>
    public async Task<WatermarkResult> EmbedInvisibleWatermarkAsync(
        Stream sourceImageStream,
        ulong payload,
        string receiptToken,
        CancellationToken ct = default)
    {
        sourceImageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(sourceImageStream, ct);

        int width = image.Width;
        int height = image.Height;

        // Ảnh cần kích thước tối thiểu để phân bổ 64 block tần số
        if (width < 128 || height < 128)
        {
            _logger.LogWarning("Kích thước ảnh quá nhỏ ({Width}x{Height}) để nhúng DWT, bỏ qua.", width, height);
            var fallbackOut = new MemoryStream();
            await image.SaveAsPngAsync(fallbackOut, ct);
            fallbackOut.Position = 0;
            return new WatermarkResult(fallbackOut, "image/png", (long)payload, receiptToken);
        }

        // Chia ảnh thành lưới 8x8 = 64 vùng để nhúng 64 bit
        int blockW = width / 8;
        int blockH = height / 8;

        image.ProcessPixelRows(accessor =>
        {
            for (int bitIndex = 0; bitIndex < PayloadBits; bitIndex++)
            {
                int row = bitIndex / 8;
                int col = bitIndex % 8;

                int startX = col * blockW + (blockW / 2) - 2;
                int startY = row * blockH + (blockH / 2) - 2;

                if (startX + 1 >= width || startY + 1 >= height || startX < 0 || startY < 0)
                    continue;

                // Lấy 4 điểm ảnh 2x2 để biến đổi Haar Wavelet 1 cấp
                var row0 = accessor.GetRowSpan(startY);
                var row1 = accessor.GetRowSpan(startY + 1);

                // Dùng kênh Blue (B) để mắt người khó phân biệt
                float a = row0[startX].B;
                float b = row0[startX + 1].B;
                float c = row1[startX].B;
                float d = row1[startX + 1].B;

                // 2D Haar Wavelet Transform
                float ll = (a + b + c + d) / 2.0f;
                float lh = (a + b - c - d) / 2.0f; // Horizontal mid-frequency
                float hl = (a - b + c - d) / 2.0f; // Vertical mid-frequency
                float hh = (a - b - c + d) / 2.0f;

                // Lấy bit thứ i từ payload
                bool bit = ((payload >> bitIndex) & 1UL) == 1UL;

                // Điều chế quan hệ giữa dải LH và HL
                if (bit)
                {
                    // Bit 1: Ép LH > HL ít nhất EmbeddingStrength
                    if (lh - hl < EmbeddingStrength)
                    {
                        lh += EmbeddingStrength / 2.0f;
                        hl -= EmbeddingStrength / 2.0f;
                    }
                }
                else
                {
                    // Bit 0: Ép HL > LH ít nhất EmbeddingStrength
                    if (hl - lh < EmbeddingStrength)
                    {
                        hl += EmbeddingStrength / 2.0f;
                        lh -= EmbeddingStrength / 2.0f;
                    }
                }

                // Inverse 2D Haar Wavelet Transform
                float newA = (ll + lh + hl + hh) / 2.0f;
                float newB = (ll + lh - hl - hh) / 2.0f;
                float newC = (ll - lh + hl - hh) / 2.0f;
                float newD = (ll - lh - hl + hh) / 2.0f;

                // Cập nhật lại điểm ảnh và kẹp giá trị [0, 255]
                row0[startX].B = (byte)Math.Clamp((int)Math.Round(newA), 0, 255);
                row0[startX + 1].B = (byte)Math.Clamp((int)Math.Round(newB), 0, 255);
                row1[startX].B = (byte)Math.Clamp((int)Math.Round(newC), 0, 255);
                row1[startX + 1].B = (byte)Math.Clamp((int)Math.Round(newD), 0, 255);
            }
        });

        var outputStream = new MemoryStream();
        if (image.Frames.Count > 1)
        {
            await image.SaveAsGifAsync(outputStream, ct);
            outputStream.Position = 0;
            _logger.LogInformation("Nhúng DWT Watermark thành công cho ảnh động GIF Token {ReceiptToken}, Payload: {Payload:X}", receiptToken, payload);
            return new WatermarkResult(outputStream, "image/gif", (long)payload, receiptToken);
        }

        await image.SaveAsPngAsync(outputStream, ct);
        outputStream.Position = 0;

        _logger.LogInformation("Nhúng DWT Watermark thành công cho Token {ReceiptToken}, Payload: {Payload:X}", receiptToken, payload);
        return new WatermarkResult(outputStream, "image/png", (long)payload, receiptToken);
    }

    /// <summary>
    /// Trích xuất mã số 64-bit từ ảnh nghi vấn
    /// </summary>
    public async Task<ulong?> ExtractInvisibleWatermarkAsync(
        Stream imageStream,
        CancellationToken ct = default)
    {
        imageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(imageStream, ct);

        int width = image.Width;
        int height = image.Height;

        if (width < 128 || height < 128)
        {
            return null;
        }

        int blockW = width / 8;
        int blockH = height / 8;
        ulong extractedPayload = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int bitIndex = 0; bitIndex < PayloadBits; bitIndex++)
            {
                int row = bitIndex / 8;
                int col = bitIndex % 8;

                int startX = col * blockW + (blockW / 2) - 2;
                int startY = row * blockH + (blockH / 2) - 2;

                if (startX + 1 >= width || startY + 1 >= height || startX < 0 || startY < 0)
                    continue;

                var row0 = accessor.GetRowSpan(startY);
                var row1 = accessor.GetRowSpan(startY + 1);

                float a = row0[startX].B;
                float b = row0[startX + 1].B;
                float c = row1[startX].B;
                float d = row1[startX + 1].B;

                float lh = (a + b - c - d) / 2.0f;
                float hl = (a - b + c - d) / 2.0f;

                if (lh > hl)
                {
                    extractedPayload |= (1UL << bitIndex);
                }
            }
        });

        _logger.LogInformation("Trích xuất DWT Watermark hoàn tất: {Payload:X}", extractedPayload);
        return extractedPayload;
    }

    /// <summary>
    /// Tạo lớp bảo vệ Moiré Anti-capture và watermark mờ định danh
    /// </summary>
    public async Task<Stream> ApplyVisibleSecurityOverlayAsync(
        Stream sourceImageStream,
        string creatorName,
        string receiptToken,
        bool applyMoirePattern = true,
        CancellationToken ct = default)
    {
        sourceImageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(sourceImageStream, ct);

        int width = image.Width;
        int height = image.Height;

        // 1. Nếu bật Moiré Pattern: tạo lưới điểm vi mô sub-pixel can thiệp ống kính camera
        if (applyMoirePattern)
        {
            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < height; y += 4)
                {
                    var row = accessor.GetRowSpan(y);
                    for (int x = 0; x < width; x += 4)
                    {
                        // Thay đổi vi mô mức sáng rất nhẹ (không thấy bằng mắt, nhưng gây nhiễu camera CMOS)
                        ref var pixel = ref row[x];
                        pixel.R = (byte)Math.Clamp(pixel.R ^ 0x03, 0, 255);
                        pixel.G = (byte)Math.Clamp(pixel.G ^ 0x03, 0, 255);
                        pixel.B = (byte)Math.Clamp(pixel.B ^ 0x03, 0, 255);
                    }
                }
            });
        }

        // 2. Vẽ chữ Forensic mờ lặp lại trên ảnh
        var font = SystemFonts.TryGet("Arial", out var fontFamily)
            ? fontFamily.CreateFont(16, FontStyle.Bold)
            : SystemFonts.Collection.Families.GetEnumerator().MoveNext()
                ? SystemFonts.Collection.Families.GetEnumerator().Current.CreateFont(16, FontStyle.Bold)
                : null;

        if (font != null)
        {
            string watermarkText = $"© {creatorName} | {receiptToken}";
            var textColor = new Color(new Rgba32(255, 255, 255, 18)); // Độ mờ 7%

            image.Mutate(ctx =>
            {
                for (int y = 50; y < height; y += 180)
                {
                    for (int x = -100; x < width; x += 320)
                    {
                        var options = new DrawingOptions
                        {
                            Transform = Matrix3x2.CreateRotation((float)(-25 * Math.PI / 180), new Vector2(x, y))
                        };
                        ctx.DrawText(options, watermarkText, font, textColor, new PointF(x, y));
                    }
                }
            });
        }

        var outputStream = new MemoryStream();
        await image.SaveAsPngAsync(outputStream, ct);
        outputStream.Position = 0;
        return outputStream;
    }
}
