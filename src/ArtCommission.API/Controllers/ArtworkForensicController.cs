using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using ArtCommission.API.Helpers;
using ArtCommission.Application.ArtistStudio.Commands.CreateArtworkViewReceipt;
using ArtCommission.Application.ArtistStudio.Queries.TraceArtworkForensic;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ArtCommission.API.Controllers;

[ApiController]
[Route("api/v1")]
public class ArtworkForensicController : ApiControllerBase
{
    private readonly IArtworkWatermarkService _watermarkService;
    private readonly IApplicationDbContext _db;
    private readonly IStorageService _storageService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".webp", ".gif", ".bmp"
    };

    public ArtworkForensicController(
        IArtworkWatermarkService watermarkService,
        IApplicationDbContext db,
        IStorageService storageService,
        IHttpClientFactory httpClientFactory,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _watermarkService = watermarkService;
        _db = db;
        _storageService = storageService;
        _httpClientFactory = httpClientFactory;
        _env = env;
        _config = config;
    }

    /// <summary>
    /// Tạo phiên xem tranh và sinh "Biên lai thuỷ vân số" (Forensic Watermark Receipt).
    /// Được gọi bởi Frontend (SCR-12 Viewer) để lấy URL ảnh đã nhúng DWT theo đúng phiên xem và lớp overlay.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("artworks/{id:guid}/view-session")]
    public async Task<IActionResult> CreateViewSession(Guid id, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
        var userAgent = Request.Headers.UserAgent.ToString();
        var sessionId = Request.Headers["X-Session-Id"].ToString();

        Guid? userId = CurrentUserId != Guid.Empty ? CurrentUserId : null;
        string? email = User.FindFirst(ClaimTypes.Email)?.Value;
        string? name = User.FindFirst(ClaimTypes.Name)?.Value;

        var command = new CreateArtworkViewReceiptCommand(
            ArtworkId: id,
            ViewerUserId: userId,
            ViewerEmail: email,
            ViewerName: name,
            IpAddress: ip,
            UserAgent: userAgent,
            SessionId: sessionId
        );

        var result = await Mediator.Send(command, ct);
        return OkEnvelope(result);
    }

    /// <summary>
    /// Trả về ảnh hiển thị công khai đã giảm độ phân giải (tối đa 1200px) và nhúng DWT payload của phiên xem.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("artworks/{id:guid}/protected-image")]
    public async Task<IActionResult> GetProtectedImage(Guid id, [FromQuery] string? token, CancellationToken ct)
    {
        var artwork = await _db.Artworks
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted, ct);
        if (artwork is null)
        {
            return NotFound();
        }

        var receipt = !string.IsNullOrWhiteSpace(token)
            ? await _db.ArtworkWatermarkReceipts
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.ArtworkId == id && r.ReceiptToken == token.Trim(), ct)
            : null;

        ulong payload = receipt?.NumericPayload is long np && np != 0
            ? (ulong)np
            : BitConverter.ToUInt64(id.ToByteArray(), 0);
        if (payload == 0) payload = 0x44494C5F50524F54UL;
        var receiptToken = receipt?.ReceiptToken ?? $"ART-{id.ToString("N")[..8].ToUpperInvariant()}";

        var sourceUrl = !string.IsNullOrWhiteSpace(artwork.WatermarkedUrl)
            ? artwork.WatermarkedUrl
            : artwork.ImageUrl;
        var alreadyReduced = !string.IsNullOrWhiteSpace(artwork.WatermarkedUrl);

        await using var sourceStream = await LoadSourceImageStreamAsync(sourceUrl, ct);
        if (sourceStream is null)
        {
            return NotFound();
        }

        try
        {
            var result = alreadyReduced
                ? await _watermarkService.EmbedInvisibleWatermarkAsync(sourceStream, payload, receiptToken, ct)
                : await _watermarkService.CreateReducedMarketplacePreviewAsync(sourceStream, payload, receiptToken, 1200, ct);

            await using var processed = result.ProcessedStream;
            processed.Position = 0;
            using var ms = new MemoryStream();
            await processed.CopyToAsync(ms, ct);
            Response.Headers.CacheControl = "private, no-store";
            return File(ms.ToArray(), result.ContentType);
        }
        catch
        {
            sourceStream.Position = 0;
            using var ms = new MemoryStream();
            await sourceStream.CopyToAsync(ms, ct);
            Response.Headers.CacheControl = "private, no-store";
            return File(ms.ToArray(), GuessContentType(sourceUrl));
        }
    }

    /// <summary>
    /// Tra cứu lịch sử biên lai để truy vết nguồn gốc tranh hoặc file bàn giao rò rỉ.
    /// </summary>
    [Authorize]
    [HttpGet("artworks/forensic/trace")]
    public async Task<IActionResult> TraceReceipt(
        [FromQuery] string? receiptToken,
        [FromQuery] long? numericPayload,
        [FromQuery] string? artworkId,
        [FromQuery] string? commissionId,
        [FromQuery] string? fileSha256,
        CancellationToken ct)
    {
        Guid? parsedArtworkId = null;
        string? artworkPrefix = null;
        if (!string.IsNullOrWhiteSpace(artworkId))
        {
            var raw = artworkId.Trim();
            if (Guid.TryParse(raw, out var aId))
            {
                parsedArtworkId = aId;
            }
            else
            {
                var clean = raw.StartsWith("#") ? raw[1..].Trim() : raw;
                clean = clean.Replace("-", "").ToUpperInvariant();
                artworkPrefix = clean;
            }
        }

        Guid? parsedCommissionId = null;
        string? commissionPrefix = null;
        if (!string.IsNullOrWhiteSpace(commissionId))
        {
            var raw = commissionId.Trim();
            if (Guid.TryParse(raw, out var cId))
            {
                parsedCommissionId = cId;
            }
            else
            {
                var clean = raw.StartsWith("#") ? raw[1..].Trim() : raw;
                if (clean.StartsWith("CMD-", StringComparison.OrdinalIgnoreCase))
                {
                    clean = clean[4..].Trim();
                }
                clean = clean.Replace("-", "").ToUpperInvariant();
                commissionPrefix = clean;
            }
        }

        // Hỗ trợ tìm kiếm thông minh: nếu receiptToken thực chất là mã đơn (#CMD-...)
        if (!string.IsNullOrWhiteSpace(receiptToken) && string.IsNullOrWhiteSpace(commissionPrefix) && !parsedCommissionId.HasValue)
        {
            var rawToken = receiptToken.Trim();
            if (rawToken.StartsWith("#CMD-", StringComparison.OrdinalIgnoreCase) || rawToken.StartsWith("CMD-", StringComparison.OrdinalIgnoreCase))
            {
                var clean = rawToken.StartsWith("#") ? rawToken[1..].Trim() : rawToken;
                commissionPrefix = clean[4..].Trim().Replace("-", "").ToUpperInvariant();
                receiptToken = null;
            }
        }

        var isAdmin = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);
        var query = new TraceArtworkForensicQuery(
            receiptToken,
            numericPayload,
            parsedArtworkId,
            parsedCommissionId,
            fileSha256,
            ArtworkPrefix: artworkPrefix,
            CommissionPrefix: commissionPrefix,
            RequestingUserId: CurrentUserId,
            IsAdmin: isAdmin);

        var results = await Mediator.Send(query, ct);
        return OkEnvelope(results);
    }

    /// <summary>
    /// Lấy danh sách biên lai thuỷ vân gần đây của tác phẩm/đơn hàng thuộc người dùng (hoặc toàn hệ thống nếu là Admin/Moderator).
    /// </summary>
    [Authorize]
    [HttpGet("artworks/forensic/recent")]
    public async Task<IActionResult> GetRecentReceipts(CancellationToken ct)
    {
        var isAdmin = User.IsInRole(UserRoleNames.Administrator) || User.IsInRole(UserRoleNames.Moderator);
        var list = await Mediator.Send(new GetRecentForensicReceiptsQuery(CurrentUserId, isAdmin), ct);
        return OkEnvelope(list);
    }

    /// <summary>
    /// Admin/Moderator tạo hàng loạt bản hiển thị giảm độ phân giải (tối đa 1200px) kèm DWT cho toàn bộ tranh cũ chưa có WatermarkedUrl.
    /// </summary>
    [Authorize(Roles = UserRoleNames.Administrator + "," + UserRoleNames.Moderator)]
    [HttpPost("artworks/forensic/backfill-previews")]
    public async Task<IActionResult> BackfillArtworkPreviews([FromQuery] bool refreshVisualHashes = false, CancellationToken ct = default)
    {
        var targets = await _db.Artworks
            .Include(a => a.CreatorProfile)
            .Where(a => !a.IsDeleted && string.IsNullOrEmpty(a.WatermarkedUrl))
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(ct);

        int processed = 0;
        int visualUpdated = 0;
        var skipped = new List<object>();

        foreach (var artwork in targets)
        {
            try
            {
                await using var sourceStream = await LoadSourceImageStreamAsync(artwork.ImageUrl, ct);
                if (sourceStream is null)
                {
                    skipped.Add(new { artwork.Id, artwork.Title, Reason = "Không đọc được file ảnh nguồn" });
                    continue;
                }

                ulong payload;
                do
                {
                    payload = BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8));
                } while (payload == 0);

                var suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
                var receiptToken = $"PUB-{artwork.Id.ToString("N")[..8].ToUpperInvariant()}-{suffix}";
                var preview = await _watermarkService.CreateReducedMarketplacePreviewAsync(
                    sourceStream, payload, receiptToken, 1200, ct);

                await using var previewStream = preview.ProcessedStream;
                previewStream.Position = 0;
                var previewSha = Convert.ToHexString(await SHA256.HashDataAsync(previewStream, ct)).ToLowerInvariant();
                previewStream.Position = 0;
                var visualHash = await _watermarkService.ComputeVisualHashAsync(previewStream, trimBorders: false, ct);
                previewStream.Position = 0;

                var previewExt = preview.ContentType == "image/gif" ? ".gif" : ".png";
                var key = $"artworks/backfill/{artwork.Id:N}-{suffix.ToLowerInvariant()}-preview{previewExt}";
                var watermarkedUrl = await _storageService.UploadPublicAsync(previewStream, key, preview.ContentType, ct);

                if (string.IsNullOrWhiteSpace(artwork.ThumbnailUrl)
                    || string.Equals(artwork.ThumbnailUrl, artwork.ImageUrl, StringComparison.Ordinal))
                {
                    artwork.ThumbnailUrl = watermarkedUrl;
                }

                artwork.WatermarkedUrl = watermarkedUrl;
                artwork.UpdatedAt = DateTimeOffset.UtcNow;

                _db.ArtworkWatermarkReceipts.Add(new ArtworkWatermarkReceipt
                {
                    ArtworkId = artwork.Id,
                    ReceiptToken = receiptToken,
                    NumericPayload = (long)payload,
                    VisualHash = visualHash,
                    FileSha256 = previewSha,
                    FileName = Path.GetFileName(key),
                    ViewerUserId = artwork.CreatorProfile?.UserId ?? (CurrentUserId != Guid.Empty ? CurrentUserId : null),
                    IpAddress = "backfill",
                    WatermarkType = "DwtPublish",
                    Status = "Active",
                    Notes = $"{artwork.ImageUrl}\n{watermarkedUrl}"
                });

                processed++;
            }
            catch (Exception ex)
            {
                skipped.Add(new { artwork.Id, artwork.Title, Reason = ex.Message });
            }
        }

        // Bổ sung / cập nhật VisualHash cho các biên lai DwtPublish
        var missingVisualReceipts = await _db.ArtworkWatermarkReceipts
            .Include(r => r.Artwork)
            .Where(r => r.WatermarkType == "DwtPublish"
                && (refreshVisualHashes || r.VisualHash == null)
                && r.Artwork != null
                && !r.Artwork.IsDeleted)
            .ToListAsync(ct);

        foreach (var rec in missingVisualReceipts)
        {
            var srcUrl = !string.IsNullOrWhiteSpace(rec.Artwork!.WatermarkedUrl) ? rec.Artwork.WatermarkedUrl : rec.Artwork.ImageUrl;
            try
            {
                await using var s = await LoadSourceImageStreamAsync(srcUrl, ct);
                if (s is null) continue;
                var vh = await _watermarkService.ComputeVisualHashAsync(s, trimBorders: false, ct);
                if (vh.HasValue && rec.VisualHash != vh.Value)
                {
                    rec.VisualHash = vh.Value;
                    visualUpdated++;
                }
            }
            catch
            {
                // skip unreadable image
            }
        }

        if (processed > 0 || visualUpdated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return OkEnvelope(new
        {
            TotalPending = targets.Count,
            Processed = processed,
            VisualHashesUpdated = visualUpdated,
            SkippedCount = skipped.Count,
            Skipped = skipped
        });
    }

    /// <summary>
    /// Tải lên ảnh, file kỹ thuật số (.psd/.clip/.pdf...) hoặc file .zip nghi rò rỉ để trích xuất DWT / đối chiếu SHA-256 và truy vết danh tính.
    /// </summary>
    [Authorize]
    [HttpPost("artworks/forensic/decode-image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> DecodeSuspectImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length <= 0)
        {
            return BadRequestEnvelope("Vui lòng tải lên tệp ảnh, file kỹ thuật số hoặc .zip cần kiểm tra.");
        }

        await using var uploadStream = file.OpenReadStream();
        using var buffered = new MemoryStream();
        await uploadStream.CopyToAsync(buffered, ct);
        buffered.Position = 0;

        var fileSha256 = Convert.ToHexString(await SHA256.HashDataAsync(buffered, ct)).ToLowerInvariant();
        buffered.Position = 0;

        var allMatches = new List<ArtworkForensicTraceDto>();
        var methods = new List<string>();

        var shaMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, null, null, null, fileSha256), ct);
        if (shaMatches.Count > 0)
        {
            allMatches.AddRange(shaMatches);
            methods.Add("SHA-256");
        }

        ulong? extractedPayload = null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (ext == ".zip")
        {
            try
            {
                buffered.Position = 0;
                using var zip = new ZipArchive(buffered, ZipArchiveMode.Read, leaveOpen: true);
                foreach (var entry in zip.Entries.Where(e => !string.IsNullOrEmpty(e.Name)).Take(25))
                {
                    await using var entryStream = entry.Open();
                    using var entryMem = new MemoryStream();
                    await entryStream.CopyToAsync(entryMem, ct);
                    entryMem.Position = 0;

                    if (string.Equals(entry.Name, "DIL-FORENSIC-RECEIPT.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(entryMem, leaveOpen: true);
                        while (await reader.ReadLineAsync(ct) is { } line)
                        {
                            if (line.StartsWith("ReceiptToken", StringComparison.OrdinalIgnoreCase))
                            {
                                var colonIdx = line.IndexOf(':');
                                if (colonIdx >= 0)
                                {
                                    var tokenVal = line[(colonIdx + 1)..].Trim();
                                    if (!string.IsNullOrWhiteSpace(tokenVal))
                                    {
                                        var tokenMatches = await Mediator.Send(new TraceArtworkForensicQuery(tokenVal, null, null), ct);
                                        if (tokenMatches.Count > 0)
                                        {
                                            allMatches.AddRange(tokenMatches);
                                            if (!methods.Contains("ReceiptFile")) methods.Add("ReceiptFile");
                                        }
                                    }
                                }
                            }
                        }
                        continue;
                    }

                    var entrySha = Convert.ToHexString(await SHA256.HashDataAsync(entryMem, ct)).ToLowerInvariant();
                    var entryShaMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, null, null, null, entrySha), ct);
                    if (entryShaMatches.Count > 0)
                    {
                        allMatches.AddRange(entryShaMatches);
                        if (!methods.Contains("ZipInnerSHA-256")) methods.Add("ZipInnerSHA-256");
                    }

                    var innerExt = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (!extractedPayload.HasValue && ImageExtensions.Contains(innerExt))
                    {
                        entryMem.Position = 0;
                        var innerDwt = await _watermarkService.ExtractInvisibleWatermarkAsync(entryMem, ct);
                        if (innerDwt.HasValue && innerDwt.Value != 0)
                        {
                            extractedPayload = innerDwt;
                            var dwtMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, (long)innerDwt.Value, null, AllowFuzzyDwtMatch: true), ct);
                            if (dwtMatches.Count > 0)
                            {
                                allMatches.AddRange(dwtMatches);
                                if (!methods.Contains("ZipInnerDWT")) methods.Add("ZipInnerDWT");
                            }
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua nếu ZIP lỗi định dạng
            }
        }
        else if (ImageExtensions.Contains(ext) || string.IsNullOrEmpty(ext))
        {
            try
            {
                buffered.Position = 0;
                extractedPayload = await _watermarkService.ExtractInvisibleWatermarkAsync(buffered, ct);
                if (extractedPayload.HasValue && extractedPayload.Value != 0)
                {
                    var dwtMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, (long)extractedPayload.Value, null, AllowFuzzyDwtMatch: true), ct);
                    if (dwtMatches.Count > 0)
                    {
                        allMatches.AddRange(dwtMatches);
                        methods.Add("DWT");
                    }
                }

                // Nếu là ảnh chụp màn hình dính viền UI tối / bị crop: thử tự cắt viền đọc lại DWT và đối chiếu VisualHash (dHash)
                if (allMatches.Count == 0)
                {
                    buffered.Position = 0;
                    var (trimmedDwt, visualHashes) = await _watermarkService.AnalyzeScreenshotVariantsAsync(buffered, ct);
                    if (trimmedDwt.HasValue && trimmedDwt.Value != 0)
                    {
                        var trimmedMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, (long)trimmedDwt.Value, null, AllowFuzzyDwtMatch: true), ct);
                        if (trimmedMatches.Count > 0)
                        {
                            extractedPayload = trimmedDwt;
                            allMatches.AddRange(trimmedMatches);
                            methods.Add("TrimmedDWT");
                        }
                    }

                    if (allMatches.Count == 0)
                    {
                        foreach (var vh in visualHashes)
                        {
                            var visualMatches = await Mediator.Send(new TraceArtworkForensicQuery(null, null, null, VisualHash: vh), ct);
                            if (visualMatches.Count > 0)
                            {
                                allMatches.AddRange(visualMatches);
                                methods.Add("VisualHash (Screenshot)");
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Không phải ảnh raster đọc được bởi ImageSharp (ví dụ PSD/CLIP đổi đuôi)
            }
        }

        var distinctMatches = allMatches
            .GroupBy(m => m.ReceiptId)
            .Select(g => g.First())
            .OrderByDescending(m => m.ViewedAt)
            .ToList();

        var hasDwt = extractedPayload.HasValue && extractedPayload.Value != 0;
        long? payloadLong = hasDwt ? (long)extractedPayload!.Value : null;
        var found = distinctMatches.Count > 0;

        if (!found)
        {
            return OkEnvelope(new
            {
                Found = false,
                MatchMethod = "None",
                ExtractedPayload = hasDwt ? $"0x{extractedPayload!.Value:X16}" : null,
                NumericPayload = payloadLong,
                FileSha256 = fileSha256,
                Message = "Không tìm thấy biên lai khớp. Ảnh chụp màn hình dính viền hoặc co giãn làm lệch lưới toạ độ DWT; bạn có thể bật 'Soi chữ chìm ảnh chụp màn hình' hoặc dùng trực tiếp file tải từ hệ thống.",
                TotalMatches = 0,
                Matches = Array.Empty<ArtworkForensicTraceDto>()
            });
        }

        var matchMessage = methods.Contains("VisualHash (Screenshot)")
            ? "Khớp theo hình ảnh sau khi cắt bỏ viền tối xung quanh. Cách này xác định tác phẩm gốc; muốn truy vết đúng tài khoản người xem theo phiên, cần file ảnh tải từ hệ thống hoặc đọc mã chữ chìm trên ảnh chụp."
            : null;

        return OkEnvelope(new
        {
            Found = true,
            MatchMethod = methods.Count > 0 ? string.Join(" + ", methods) : "DWT",
            ExtractedPayload = hasDwt ? $"0x{extractedPayload!.Value:X16}" : null,
            NumericPayload = payloadLong,
            FileSha256 = fileSha256,
            Message = matchMessage,
            TotalMatches = distinctMatches.Count,
            Matches = distinctMatches
        });
    }

    private Task<Stream?> LoadSourceImageStreamAsync(string sourceUrl, CancellationToken ct) =>
        ArtworkSourceStreamHelper.LoadSourceImageStreamAsync(sourceUrl, _httpClientFactory, _env, _config, ct);

    private static string GuessContentType(string url)
    {
        if (url.EndsWith(".gif", StringComparison.OrdinalIgnoreCase)) return "image/gif";
        if (url.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return "image/webp";
        if (url.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)) return "image/jpeg";
        return "image/png";
    }
}
