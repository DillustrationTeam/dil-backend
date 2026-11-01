using ArtCommission.Application.ArtistStudio.Ai;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace ArtCommission.Infrastructure.Services;

public class ArtworkAiScanService(AppDbContext db, IArtworkAiDetector detector)
{
    public async Task<ArtworkScanStatus> GetAsync(Guid id, Guid userId, bool moderator, CancellationToken ct)
    {
        var a = await OwnedAsync(id, userId, moderator, ct);
        var current = a.AiScanImageUrl == a.ImageUrl;
        var status = !detector.IsConfigured ? "NotConfigured"
            : a.ModerationStatus is not ("Pending" or "Flagged") && !(current && a.AiScanStatus == "Succeeded") ? "NotRequested"
            : current ? a.AiScanStatus : "Queued";
        return new(status,
            status == "Succeeded" ? a.AiDetectionScore : null,
            current ? a.AiScanProvider : null, current ? a.AiScanModel : null,
            current && a.AiScanStatus == "Succeeded" ? a.AiScanUpdatedAt : null,
            current ? a.AiScanError : null);
    }
    private async Task<Artwork> OwnedAsync(Guid id, Guid userId, bool moderator, CancellationToken ct)
    {
        var a = await db.Artworks.Include(x => x.CreatorProfile).SingleOrDefaultAsync(x => x.Id == id && !x.IsDeleted && !x.CreatorProfile!.IsDeleted, ct)
            ?? throw new KeyNotFoundException("Không tìm thấy tác phẩm.");
        if (!moderator && a.CreatorProfile?.UserId != userId) throw new UnauthorizedAccessException("Bạn không được xem kết quả kiểm tra này.");
        return a;
    }
    public async Task<ArtworkScanStatus> RetryAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var a = await OwnedAsync(id, userId, true, ct);
        if (a.ModerationStatus is not ("Pending" or "Flagged")) throw new InvalidOperationException("Chỉ kiểm tra lại tác phẩm đang chờ duyệt hoặc bị gắn cờ.");
        if (!detector.IsConfigured) throw new InvalidOperationException("Chưa cấu hình Sightengine API user / API secret.");
        if (a.AiScanUpdatedAt > DateTimeOffset.UtcNow.AddSeconds(-30) || (a.AiScanStatus == "Processing" && a.AiScanUpdatedAt > DateTimeOffset.UtcNow.AddMinutes(-5)))
            throw new InvalidOperationException("Đang xử lý hoặc vừa gửi kiểm tra. Vui lòng đợi trước khi thử lại.");
        a.AiScanStatus = "Queued"; a.AiScanAttempts = 0; a.AiScanError = null; a.AiDetectionScore = null;
        a.AiScanUpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, userId, true, ct);
    }
    public async Task<bool> ProcessNextAsync(CancellationToken ct)
    {
        if (!detector.IsConfigured) return false;
        var now = DateTimeOffset.UtcNow;
        var a = await db.Artworks.Where(x => !x.IsDeleted && !x.CreatorProfile!.IsDeleted && (x.ModerationStatus == "Pending" || x.ModerationStatus == "Flagged") &&
            (x.AiScanImageUrl != x.ImageUrl || x.AiScanImageUrl == null || x.AiScanStatus == "Queued" ||
             (x.AiScanStatus == "Retrying" && x.AiScanUpdatedAt < now.AddMinutes(-2)) ||
             (x.AiScanStatus == "Processing" && x.AiScanUpdatedAt < now.AddMinutes(-5))))
            .OrderBy(x => x.CreatedAt).FirstOrDefaultAsync(ct);
        if (a is null) return false;
        var source = a.ImageUrl;
        if (a.AiScanImageUrl != source) a.AiScanAttempts = 0;
        a.AiScanStatus = "Processing"; a.AiScanImageUrl = source; a.AiScanAttempts++;
        a.AiDetectionScore = null; a.AiScanError = null; a.AiScanUpdatedAt = now;
        a.AiScanProvider = "Sightengine"; a.AiScanModel = "genai";
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { return true; }
        decimal? result = null; string? failure = null;
        try { result = await detector.DetectAsync(source, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception) { failure = "Không kiểm tra được ảnh. Kiểm tra kết nối, cấu hình kho ảnh hoặc thử lại sau."; }
        await db.Entry(a).ReloadAsync(ct);
        // Discard an old result if the image changed, another worker reclaimed the lease, or the artwork was deleted.
        if (a.IsDeleted || a.ImageUrl != source || a.AiScanStatus != "Processing" || a.AiScanUpdatedAt != now) return true;
        a.AiDetectionScore = result;
        a.AiScanStatus = result.HasValue ? "Succeeded" : a.AiScanAttempts < 3 ? "Retrying" : "Failed";
        a.AiScanError = failure; a.AiScanUpdatedAt = DateTimeOffset.UtcNow;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { /* Another image/retry superseded this result. */ }
        return true;
    }
}
