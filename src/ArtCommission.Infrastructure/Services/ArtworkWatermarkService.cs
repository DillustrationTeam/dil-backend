using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
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
    private const float EmbeddingStrength = 14.0f; // Cường độ nhúng dải trung tần (Macro Haar 8x8)
    private const int QuadrantSize = 4; // Mỗi block DWT gồm 4 góc phần tư 4x4 (tổng 8x8 px)

    private static readonly HashSet<string> RasterExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp"
    };
    private const long MaxZipRasterProcessBytes = 80L * 1024 * 1024;

    public ArtworkWatermarkService(ILogger<ArtworkWatermarkService> logger)
    {
        _logger = logger;
    }

    public async Task<WatermarkResult> EmbedInvisibleWatermarkAsync(
        Stream sourceImageStream,
        ulong payload,
        string receiptToken,
        CancellationToken ct = default)
    {
        sourceImageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(sourceImageStream, ct);
        ApplyDwtPayload(image, payload);
        return await EncodeWatermarkResultAsync(image, payload, receiptToken, "DWT Watermark", ct);
    }

    public async Task<WatermarkResult> CreateReducedMarketplacePreviewAsync(
        Stream sourceImageStream,
        ulong payload,
        string receiptToken,
        int maxDimension = 1200,
        CancellationToken ct = default)
    {
        sourceImageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(sourceImageStream, ct);

        int longest = Math.Max(image.Width, image.Height);
        if (longest > maxDimension)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max
            }));
        }
        else if (longest > 760)
        {
            // Thu nhỏ nhẹ (~75%) khi ảnh nhỏ hơn maxDimension để tranh trưng bày luôn thấp hơn bản gốc
            int targetW = Math.Max(480, (int)Math.Round(image.Width * 0.75));
            int targetH = Math.Max(480, (int)Math.Round(image.Height * 0.75));
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(targetW, targetH),
                Mode = ResizeMode.Max
            }));
        }

        ApplyDwtPayload(image, payload);
        return await EncodeWatermarkResultAsync(image, payload, receiptToken, "Marketplace Reduced Preview", ct);
    }

    public async Task<DeliverableWatermarkOutput> ProcessDeliverableWatermarkAsync(
        Stream sourceStream,
        string fileName,
        string contentType,
        ulong payload,
        string receiptToken,
        string receiptSummaryText,
        CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (RasterExtensions.Contains(ext))
        {
            sourceStream.Position = 0;
            var result = await EmbedInvisibleWatermarkAsync(sourceStream, payload, receiptToken, ct);
            var outName = result.ContentType == "image/gif"
                ? Path.ChangeExtension(fileName, ".gif")
                : Path.ChangeExtension(fileName, ".png");
            result.ProcessedStream.Position = 0;
            var sha256 = await ComputeSha256HexAsync(result.ProcessedStream, ct);
            result.ProcessedStream.Position = 0;
            return new DeliverableWatermarkOutput(
                result.ProcessedStream,
                outName,
                result.ContentType,
                sha256,
                result.ProcessedStream.Length,
                new[] { new ProtectedFileRecord(outName, sha256, HasDwtWatermark: true) });
        }

        if (ext == ".zip")
        {
            return await ProcessZipDeliverableAsync(sourceStream, fileName, payload, receiptToken, receiptSummaryText, ct);
        }

        // File kỹ thuật số có layer/vector (.psd, .psb, .clip, .ai, .pdf): giữ nguyên nhị phân để không hỏng layer, ghi dấu SHA-256 truy vết
        sourceStream.Position = 0;
        var passthrough = new MemoryStream();
        await sourceStream.CopyToAsync(passthrough, ct);
        passthrough.Position = 0;
        var fileSha = await ComputeSha256HexAsync(passthrough, ct);
        passthrough.Position = 0;
        return new DeliverableWatermarkOutput(
            passthrough,
            fileName,
            contentType,
            fileSha,
            passthrough.Length,
            new[] { new ProtectedFileRecord(fileName, fileSha, HasDwtWatermark: false) });
    }

    private async Task<DeliverableWatermarkOutput> ProcessZipDeliverableAsync(
        Stream sourceStream,
        string zipFileName,
        ulong payload,
        string receiptToken,
        string receiptSummaryText,
        CancellationToken ct)
    {
        sourceStream.Position = 0;
        var tempOutPath = Path.Combine(Path.GetTempPath(), $"dil-wm-zip-{Guid.NewGuid():N}.zip");
        var outStream = new FileStream(
            tempOutPath,
            FileMode.Create,
            FileAccess.ReadWrite,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.DeleteOnClose);

        var records = new List<ProtectedFileRecord>();
        var usedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var inArchive = new ZipArchive(sourceStream, ZipArchiveMode.Read, leaveOpen: true);
            using (var outArchive = new ZipArchive(outStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in inArchive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        continue;
                    }

                    var entryExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                    await using var entryIn = entry.Open();

                    if (RasterExtensions.Contains(entryExt) && entry.Length <= MaxZipRasterProcessBytes)
                    {
                        using var buffered = new MemoryStream();
                        await entryIn.CopyToAsync(buffered, ct);
                        buffered.Position = 0;

                        try
                        {
                            var wm = await EmbedInvisibleWatermarkAsync(buffered, payload, receiptToken, ct);
                            await using var wmStream = wm.ProcessedStream;
                            wmStream.Position = 0;
                            var entrySha = await ComputeSha256HexAsync(wmStream, ct);
                            wmStream.Position = 0;

                            var newExt = wm.ContentType == "image/gif" ? ".gif" : ".png";
                            var targetName = EnsureUniqueArchivePath(Path.ChangeExtension(entry.FullName, newExt), usedEntries);
                            var outEntry = outArchive.CreateEntry(targetName, CompressionLevel.Fastest);
                            await using var entryOut = outEntry.Open();
                            await wmStream.CopyToAsync(entryOut, ct);

                            records.Add(new ProtectedFileRecord(targetName, entrySha, HasDwtWatermark: true));
                            continue;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Không thể nhúng DWT cho ảnh '{Entry}' trong ZIP, giữ nguyên bản gốc kèm SHA-256.", entry.FullName);
                            buffered.Position = 0;
                            var rawSha = await ComputeSha256HexAsync(buffered, ct);
                            buffered.Position = 0;
                            var fallbackName = EnsureUniqueArchivePath(entry.FullName, usedEntries);
                            var outEntry = outArchive.CreateEntry(fallbackName, CompressionLevel.Fastest);
                            await using var entryOut = outEntry.Open();
                            await buffered.CopyToAsync(entryOut, ct);
                            records.Add(new ProtectedFileRecord(fallbackName, rawSha, HasDwtWatermark: false));
                            continue;
                        }
                    }

                    var tempEntryPath = Path.Combine(Path.GetTempPath(), $"dil-entry-{Guid.NewGuid():N}");
                    string nonRasterSha;
                    try
                    {
                        await using var entryCopyFs = new FileStream(tempEntryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920, useAsync: true);
                        await entryIn.CopyToAsync(entryCopyFs, ct);
                        await entryCopyFs.FlushAsync(ct);
                        entryCopyFs.Position = 0;
                        nonRasterSha = await ComputeSha256HexAsync(entryCopyFs, ct);
                        entryCopyFs.Position = 0;

                        var intactName = EnsureUniqueArchivePath(entry.FullName, usedEntries);
                        var outEntry = outArchive.CreateEntry(intactName, CompressionLevel.Fastest);
                        await using var entryOut = outEntry.Open();
                        await entryCopyFs.CopyToAsync(entryOut, ct);
                        records.Add(new ProtectedFileRecord(intactName, nonRasterSha, HasDwtWatermark: false));
                    }
                    finally
                    {
                        try { if (File.Exists(tempEntryPath)) File.Delete(tempEntryPath); } catch { }
                    }
                }

                // Thêm biên lai xác thực nguồn gốc số vào bên trong file ZIP bàn giao
                var receiptEntryName = EnsureUniqueArchivePath("DIL-FORENSIC-RECEIPT.txt", usedEntries);
                var receiptEntry = outArchive.CreateEntry(receiptEntryName, CompressionLevel.Fastest);
                await using var receiptStream = receiptEntry.Open();
                var receiptBuilder = new StringBuilder();
                receiptBuilder.AppendLine("DILLUSTRATION DIGITAL FORENSIC WATERMARK RECEIPT");
                receiptBuilder.AppendLine($"ReceiptToken : {receiptToken}");
                receiptBuilder.AppendLine($"DwtPayload   : 0x{payload:X16}");
                receiptBuilder.AppendLine($"Summary      : {receiptSummaryText}");
                receiptBuilder.AppendLine($"GeneratedUtc : {DateTimeOffset.UtcNow:O}");
                receiptBuilder.AppendLine();
                receiptBuilder.AppendLine("PROTECTED FILES (DWT + SHA-256):");
                foreach (var rec in records)
                {
                    receiptBuilder.AppendLine($"- {rec.FileName} | {(rec.HasDwtWatermark ? "DWT+SHA256" : "SHA256")} | {rec.Sha256}");
                }
                var receiptBytes = Encoding.UTF8.GetBytes(receiptBuilder.ToString());
                await receiptStream.WriteAsync(receiptBytes, ct);
            }

            await outStream.FlushAsync(ct);
            outStream.Position = 0;
            var outerSha = await ComputeSha256HexAsync(outStream, ct);
            outStream.Position = 0;

            return new DeliverableWatermarkOutput(
                outStream,
                zipFileName,
                "application/zip",
                outerSha,
                outStream.Length,
                records);
        }
        catch
        {
            await outStream.DisposeAsync();
            throw;
        }
    }

    private static string EnsureUniqueArchivePath(string path, HashSet<string> used)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (used.Add(normalized)) return normalized;
        var dir = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
        var stem = Path.GetFileNameWithoutExtension(normalized);
        var ext = Path.GetExtension(normalized);
        var idx = 1;
        while (true)
        {
            var candidate = string.IsNullOrEmpty(dir) ? $"{stem}_{idx}{ext}" : $"{dir}/{stem}_{idx}{ext}";
            if (used.Add(candidate)) return candidate;
            idx++;
        }
    }

    private static async Task<string> ComputeSha256HexAsync(Stream stream, CancellationToken ct)
    {
        var hash = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private void ApplyDwtPayload(Image<Rgba32> image, ulong payload)
    {
        int width = image.Width;
        int height = image.Height;

        if (width < 128 || height < 128)
        {
            _logger.LogWarning("Kích thước ảnh quá nhỏ ({Width}x{Height}) để nhúng DWT, bỏ qua.", width, height);
            return;
        }

        int blockW = width / 8;
        int blockH = height / 8;
        int patchSpan = QuadrantSize * 2;

        image.ProcessPixelRows(accessor =>
        {
            for (int bitIndex = 0; bitIndex < PayloadBits; bitIndex++)
            {
                int row = bitIndex / 8;
                int col = bitIndex % 8;

                int startX = col * blockW + Math.Max(0, (blockW - patchSpan) / 2);
                int startY = row * blockH + Math.Max(0, (blockH - patchSpan) / 2);

                if (startX + patchSpan > width || startY + patchSpan > height)
                    continue;

                float sumB = 0;
                float sumC = 0;
                int count = QuadrantSize * QuadrantSize;

                for (int dy = 0; dy < QuadrantSize; dy++)
                {
                    var rowTop = accessor.GetRowSpan(startY + dy);
                    var rowBot = accessor.GetRowSpan(startY + QuadrantSize + dy);
                    for (int dx = 0; dx < QuadrantSize; dx++)
                    {
                        sumB += rowTop[startX + QuadrantSize + dx].B;
                        sumC += rowBot[startX + dx].B;
                    }
                }

                float avgB = sumB / count;
                float avgC = sumC / count;
                float diff = avgB - avgC; // Tương đương (lh - hl) trên macro-pixel 2x2

                bool bit = ((payload >> bitIndex) & 1UL) == 1UL;
                float delta = 0;
                if (bit && diff < EmbeddingStrength)
                {
                    delta = (EmbeddingStrength - diff) / 2.0f;
                }
                else if (!bit && -diff < EmbeddingStrength)
                {
                    delta = -(EmbeddingStrength + diff) / 2.0f;
                }

                if (Math.Abs(delta) < 0.01f)
                    continue;

                for (int dy = 0; dy < QuadrantSize; dy++)
                {
                    var rowTop = accessor.GetRowSpan(startY + dy);
                    var rowBot = accessor.GetRowSpan(startY + QuadrantSize + dy);
                    for (int dx = 0; dx < QuadrantSize; dx++)
                    {
                        ref var pxB = ref rowTop[startX + QuadrantSize + dx];
                        pxB.B = (byte)Math.Clamp((int)Math.Round(pxB.B + delta), 0, 255);

                        ref var pxC = ref rowBot[startX + dx];
                        pxC.B = (byte)Math.Clamp((int)Math.Round(pxC.B - delta), 0, 255);
                    }
                }
            }
        });
    }

    private async Task<WatermarkResult> EncodeWatermarkResultAsync(
        Image<Rgba32> image,
        ulong payload,
        string receiptToken,
        string tag,
        CancellationToken ct)
    {
        var outputStream = new MemoryStream();
        if (image.Frames.Count > 1)
        {
            await image.SaveAsGifAsync(outputStream, ct);
            outputStream.Position = 0;
            _logger.LogInformation("Nhúng {Tag} GIF Token {ReceiptToken}, Payload: {Payload:X}", tag, receiptToken, payload);
            return new WatermarkResult(outputStream, "image/gif", (long)payload, receiptToken);
        }

        await image.SaveAsPngAsync(outputStream, ct);
        outputStream.Position = 0;

        _logger.LogInformation("Nhúng {Tag} PNG Token {ReceiptToken}, Payload: {Payload:X}", tag, receiptToken, payload);
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
        var extractedPayload = ExtractDwtFromImage(image);
        if (extractedPayload.HasValue)
        {
            _logger.LogInformation("Trích xuất DWT Watermark hoàn tất: {Payload:X}", extractedPayload.Value);
        }
        return extractedPayload;
    }

    public async Task<long?> ComputeVisualHashAsync(
        Stream imageStream,
        bool trimBorders = false,
        CancellationToken ct = default)
    {
        imageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(imageStream, ct);
        if (trimBorders)
        {
            using var trimmed = TrimUniformBorders(image);
            return ComputeDHash(trimmed ?? image);
        }
        return ComputeDHash(image);
    }

    public async Task<(ulong? TrimmedDwtPayload, IReadOnlyList<long> VisualHashes)> AnalyzeScreenshotVariantsAsync(
        Stream imageStream,
        CancellationToken ct = default)
    {
        imageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(imageStream, ct);

        var hashes = new List<long>();
        var rawHash = ComputeDHash(image);
        if (rawHash.HasValue) hashes.Add(rawHash.Value);

        ulong? trimmedDwt = null;
        using var trimmed = TrimUniformBorders(image);
        if (trimmed is not null)
        {
            trimmedDwt = ExtractDwtFromImage(trimmed);
            var tHash = ComputeDHash(trimmed);
            if (tHash.HasValue && !hashes.Contains(tHash.Value)) hashes.Add(tHash.Value);

            // Cắt nhẹ thêm 4% rìa để tránh icon badge / bo góc card ảnh hưởng độ tương phản cạnh
            if (trimmed.Width >= 100 && trimmed.Height >= 100)
            {
                int mx = Math.Max(2, trimmed.Width / 25);
                int my = Math.Max(2, trimmed.Height / 25);
                if (trimmed.Width - 2 * mx >= 64 && trimmed.Height - 2 * my >= 64)
                {
                    using var inset = trimmed.Clone(ctx => ctx.Crop(new Rectangle(mx, my, trimmed.Width - 2 * mx, trimmed.Height - 2 * my)));
                    var iHash = ComputeDHash(inset);
                    if (iHash.HasValue && !hashes.Contains(iHash.Value)) hashes.Add(iHash.Value);
                }
            }
        }

        return (trimmedDwt, hashes);
    }

    private static ulong? ExtractDwtFromImage(Image<Rgba32> image)
    {
        int width = image.Width;
        int height = image.Height;

        if (width < 128 || height < 128)
        {
            return null;
        }

        int blockW = width / 8;
        int blockH = height / 8;
        int patchSpan = QuadrantSize * 2;
        ulong extractedPayload = 0;

        image.ProcessPixelRows(accessor =>
        {
            for (int bitIndex = 0; bitIndex < PayloadBits; bitIndex++)
            {
                int row = bitIndex / 8;
                int col = bitIndex % 8;

                int startX = col * blockW + Math.Max(0, (blockW - patchSpan) / 2);
                int startY = row * blockH + Math.Max(0, (blockH - patchSpan) / 2);

                if (startX + patchSpan > width || startY + patchSpan > height)
                    continue;

                float sumB = 0;
                float sumC = 0;
                for (int dy = 0; dy < QuadrantSize; dy++)
                {
                    var rowTop = accessor.GetRowSpan(startY + dy);
                    var rowBot = accessor.GetRowSpan(startY + QuadrantSize + dy);
                    for (int dx = 0; dx < QuadrantSize; dx++)
                    {
                        sumB += rowTop[startX + QuadrantSize + dx].B;
                        sumC += rowBot[startX + dx].B;
                    }
                }

                if (sumB > sumC)
                {
                    extractedPayload |= (1UL << bitIndex);
                }
            }
        });

        return extractedPayload;
    }

    private static long? ComputeDHash(Image<Rgba32> source)
    {
        if (source.Width < 32 || source.Height < 32) return null;

        using var tiny = source.Clone(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(9, 8),
            Mode = ResizeMode.Stretch,
            Sampler = KnownResamplers.Bicubic
        }));

        float minLum = 255f, maxLum = 0f;
        var lum = new float[8, 9];
        tiny.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < 8; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < 9; x++)
                {
                    var p = row[x];
                    float l = 0.299f * p.R + 0.587f * p.G + 0.114f * p.B;
                    lum[y, x] = l;
                    if (l < minLum) minLum = l;
                    if (l > maxLum) maxLum = l;
                }
            }
        });

        // Bỏ qua ảnh màu đơn sắc / trống không có chi tiết
        if (maxLum - minLum < 10f) return null;

        ulong hash = 0;
        int bit = 0;
        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                if (lum[y, x] > lum[y, x + 1])
                {
                    hash |= 1UL << bit;
                }
                bit++;
            }
        }

        int ones = BitOperations.PopCount(hash);
        if (ones < 5 || ones > 59) return null;
        return (long)hash;
    }

    private static bool NearColor(Rgba32 a, Rgba32 b, int tol) =>
        Math.Abs(a.R - b.R) <= tol && Math.Abs(a.G - b.G) <= tol && Math.Abs(a.B - b.B) <= tol;

    private static Image<Rgba32>? TrimUniformBorders(Image<Rgba32> image)
    {
        int w = image.Width;
        int h = image.Height;
        if (w < 80 || h < 80) return null;

        // Cắt từ 4 mép ngoài vào trong miễn >= 88% pixel của hàng/cột khớp màu nền góc UI (hoặc khung tối card)
        int top = 0, bottom = h - 1, left = 0, right = w - 1;
        image.ProcessPixelRows(accessor =>
        {
            var cTL = accessor.GetRowSpan(0)[0];
            var cTR = accessor.GetRowSpan(0)[w - 1];
            var cBL = accessor.GetRowSpan(h - 1)[0];
            var cBR = accessor.GetRowSpan(h - 1)[w - 1];

            bool IsUiBg(Rgba32 p) =>
                NearColor(p, cTL, 20) || NearColor(p, cTR, 20) || NearColor(p, cBL, 20) || NearColor(p, cBR, 20);

            for (int y = 0; y < h - 48; y++)
            {
                var r = accessor.GetRowSpan(y);
                int flat = 0;
                for (int x = 0; x < w; x++)
                {
                    if (IsUiBg(r[x])) flat++;
                }
                if (flat < w * 0.88) break;
                top = y + 1;
            }

            for (int y = h - 1; y > top + 48; y--)
            {
                var r = accessor.GetRowSpan(y);
                int flat = 0;
                for (int x = 0; x < w; x++)
                {
                    if (IsUiBg(r[x])) flat++;
                }
                if (flat < w * 0.88) break;
                bottom = y - 1;
            }

            int span = bottom - top + 1;
            for (int x = 0; x < w - 48; x++)
            {
                int flat = 0;
                for (int y = top; y <= bottom; y++)
                {
                    if (IsUiBg(accessor.GetRowSpan(y)[x])) flat++;
                }
                if (flat < span * 0.88) break;
                left = x + 1;
            }

            for (int x = w - 1; x > left + 48; x--)
            {
                int flat = 0;
                for (int y = top; y <= bottom; y++)
                {
                    if (IsUiBg(accessor.GetRowSpan(y)[x])) flat++;
                }
                if (flat < span * 0.88) break;
                right = x - 1;
            }
        });

        int cropW = right - left + 1;
        int cropH = bottom - top + 1;

        if ((left == 0 && top == 0 && right == w - 1 && bottom == h - 1)
            || cropW < 48 || cropH < 48
            || cropW < w / 5 || cropH < h / 5)
        {
            return null;
        }

        return image.Clone(ctx => ctx.Crop(new Rectangle(left, top, cropW, cropH)));
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
