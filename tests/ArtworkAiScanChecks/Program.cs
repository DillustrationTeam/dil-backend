using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Application.ArtistStudio.Moderation;
using ArtCommission.Application.ArtistStudio.Moderation.Commands;
using ArtCommission.Application.ArtistStudio.Moderation.Queries;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Event;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Infrastructure.BackgroundJobs;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

var checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new Exception($"Assertion failed: {message}");
    checks++;
}

Console.WriteLine("Running Artwork AI Scan automated checks...");

// 1. Khởi tạo DB InMemory
var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase(Guid.NewGuid().ToString())
    .Options;
await using var db = new AppDbContext(dbOptions);

var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "artist1", FullName = "Test Artist" };
var creator = new CreatorProfile { Id = Guid.NewGuid(), UserId = user.Id, DisplayName = "Studio", User = user };
db.Users.Add(user);
db.CreatorProfiles.Add(creator);
await db.SaveChangesAsync();

// 2. Queue & Mock Scanner
var testQueue = new TestArtworkScanQueue();
var mockScanner = new MockArtworkScanService();
var scanJob = new ArtworkScanJob(db, mockScanner, NullLogger<ArtworkScanJob>.Instance);

// =========================================================================
// TEST 1: Tải tranh lên (UploadArtwork) tự động đưa vào hàng đợi quét AI
// =========================================================================
var uploadHandler = new UploadArtworkCommandHandler(db, testQueue);
var uploadResult = await uploadHandler.Handle(new UploadArtworkCommand(
    UserId: user.Id,
    Title: "Neon Cyber Samurai",
    Description: "Hand-painted cyber aesthetic",
    ImageUrl: "https://storage.example.com/artworks/samurai.png",
    ThumbnailUrl: "https://storage.example.com/artworks/samurai_thumb.png",
    Tags: new[] { "Anime", "Cyberpunk" }
), default);

Check(uploadResult.Success && uploadResult.Data != null, "UploadArtworkCommandHandler must succeed");
var artworkId = uploadResult.Data!.Id;
Check(testQueue.EnqueuedIds.Contains(artworkId), "Artwork ID must be enqueued into IArtworkScanQueue on upload");

var initialArtwork = await db.Artworks.SingleAsync(a => a.Id == artworkId);
Check(initialArtwork.ModerationStatus == "Pending", "New artwork must have ModerationStatus = Pending");
Check(initialArtwork.SafeScore == null && initialArtwork.AiDetectionScore == null, "Scores must be null prior to scan");

// =========================================================================
// TEST 2: ArtworkScanJob xử lý tranh an toàn, không phải AI và tự động gắn tag
// =========================================================================
mockScanner.NextResult = ArtworkScanResult.Ok(
    safeScore: 0.985m,
    adultScore: 0.012m,
    violenceScore: 0.005m,
    isAiGenerated: false,
    aiDetectionScore: 0.04m,
    suggestedTags: new[] { "SciFi", "DigitalArt" },
    flagReason: null
);

await scanJob.ProcessArtworkScanAsync(artworkId, default);

var scannedArtwork = await db.Artworks
    .Include(a => a.ArtworkTags).ThenInclude(at => at.Tag)
    .SingleAsync(a => a.Id == artworkId);

Check(scannedArtwork.SafeScore == 0.985m, "SafeScore must be 0.985");
Check(scannedArtwork.AdultScore == 0.012m, "AdultScore must be 0.012");
Check(scannedArtwork.ViolenceScore == 0.005m, "ViolenceScore must be 0.005");
Check(!scannedArtwork.IsAiGenerated, "IsAiGenerated must be false");
Check(scannedArtwork.AiDetectionScore == 0.04m, "AiDetectionScore must be 0.04");
Check(scannedArtwork.FlagReason == null, "FlagReason must be null for safe artwork");

var tagNames = scannedArtwork.ArtworkTags.Select(t => t.Tag!.Name).ToList();
Check(tagNames.Contains("SciFi") && tagNames.Contains("DigitalArt"), "Suggested tags must be added to ArtworkTags");
Check(tagNames.Contains("Anime") && tagNames.Contains("Cyberpunk"), "Original tags must be preserved");

// =========================================================================
// TEST 3: ArtworkScanJob gắn cờ tranh AI và vi phạm NSFW
// =========================================================================
var aiArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creator.Id,
    Title = "Synthetic Dream",
    ImageUrl = "https://storage.example.com/artworks/ai_gen.png",
    ModerationStatus = "Pending"
};
db.Artworks.Add(aiArtwork);
await db.SaveChangesAsync();

mockScanner.NextResult = ArtworkScanResult.Ok(
    safeScore: 0.35m,
    adultScore: 0.85m,
    violenceScore: 0.02m,
    isAiGenerated: true,
    aiDetectionScore: 0.94m,
    suggestedTags: new[] { "AIGenerated" },
    flagReason: "AI NSFW Threshold Exceeded"
);

await scanJob.ProcessArtworkScanAsync(aiArtwork.Id, default);

var scannedAiArtwork = await db.Artworks.SingleAsync(a => a.Id == aiArtwork.Id);
Check(scannedAiArtwork.IsAiGenerated, "Artwork must be marked as IsAiGenerated = true");
Check(scannedAiArtwork.AiDetectionScore == 0.94m, "AiDetectionScore must be 0.94");
Check(scannedAiArtwork.AdultScore == 0.85m, "AdultScore must be 0.85");
Check(scannedAiArtwork.FlagReason == "AI NSFW Threshold Exceeded", "FlagReason must be set");

// =========================================================================
// TEST 4: Đồng bộ trạng thái AiScanPassed cho bài dự thi sự kiện (EventSubmissions)
// =========================================================================
var platformEvent = new PlatformEvent
{
    Id = Guid.NewGuid(),
    Title = "Digital Art Contest 2026",
    CreatedByAdminId = user.Id
};
db.PlatformEvents.Add(platformEvent);

var submission = new EventSubmission
{
    Id = Guid.NewGuid(),
    EventId = platformEvent.Id,
    SubmitterId = user.Id,
    ArtworkId = aiArtwork.Id,
    AiScanPassed = true // Giả lập ban đầu
};
db.EventSubmissions.Add(submission);
await db.SaveChangesAsync();

// Chạy lại job quét với kết quả AI:
await scanJob.ProcessArtworkScanAsync(aiArtwork.Id, default);

var updatedSubmission = await db.EventSubmissions.SingleAsync(s => s.Id == submission.Id);
Check(!updatedSubmission.AiScanPassed, "Submission AiScanPassed must be false when linked artwork is AI-generated");

// Đổi artwork sang tranh an toàn do người vẽ:
mockScanner.NextResult = ArtworkScanResult.Ok(
    safeScore: 0.99m,
    adultScore: 0.01m,
    violenceScore: 0.01m,
    isAiGenerated: false,
    aiDetectionScore: 0.02m,
    flagReason: null
);
await scanJob.ProcessArtworkScanAsync(aiArtwork.Id, default);

var updatedSafeSubmission = await db.EventSubmissions.SingleAsync(s => s.Id == submission.Id);
Check(updatedSafeSubmission.AiScanPassed, "Submission AiScanPassed must become true when artwork is re-scanned as clean human art");

// =========================================================================
// TEST 5: Xem chi tiết thanh tra kiểm duyệt (GetArtworkInspectionDetailQuery)
// =========================================================================
var inspectionQueryHandler = new GetArtworkInspectionDetailQueryHandler(db);
var detail = await inspectionQueryHandler.Handle(new GetArtworkInspectionDetailQuery(scannedArtwork.Id), default);

Check(detail != null, "Inspection detail must exist");
Check(detail!.SafeScore == 0.985m, "Inspection detail must reflect SafeScore");
Check(detail.AdultScore == 0.012m, "Inspection detail must reflect AdultScore");
Check(detail.ViolenceScore == 0.005m, "Inspection detail must reflect ViolenceScore");
Check(detail.AiDetectionScore == 0.04m, "Inspection detail must reflect AiDetectionScore");
Check(!detail.IsAiGenerated, "Inspection detail must reflect IsAiGenerated");

// =========================================================================
// TEST 6: Quyết định kiểm duyệt (ExecuteModerationDecisionCommand)
// =========================================================================
var decisionHandler = new ExecuteModerationDecisionCommandHandler(db);

// Approve tranh an toàn (scannedArtwork đang ở trạng thái Pending)
var approveResult = await decisionHandler.Handle(new ExecuteModerationDecisionCommand(
    ArtworkId: scannedArtwork.Id,
    ModeratorId: user.Id,
    Action: "approve",
    ModerationNote: "All criteria met"
), default);

Check(approveResult.Success, "ExecuteModerationDecision with approve must succeed");
var approvedArtwork = await db.Artworks.SingleAsync(a => a.Id == scannedArtwork.Id);
Check(approvedArtwork.ModerationStatus == "Approved", "Artwork must be Approved");

// Thử duyệt lại tác phẩm đã được duyệt trước đó -> phải chặn để tránh xung đột
var doubleModerateResult = await decisionHandler.Handle(new ExecuteModerationDecisionCommand(
    ArtworkId: scannedArtwork.Id,
    ModeratorId: user.Id,
    Action: "reject",
    ModerationNote: "Attempt duplicate decision"
), default);
Check(!doubleModerateResult.Success && doubleModerateResult.Errors.Any(e => e.Contains("Xung đột")),
    "ExecuteModerationDecision must prevent duplicate/conflicting decisions on already moderated artwork");

// Gắn cờ tranh AI cho tác phẩm aiArtwork (đang ở trạng thái Pending)
var flagAiResult = await decisionHandler.Handle(new ExecuteModerationDecisionCommand(
    ArtworkId: aiArtwork.Id,
    ModeratorId: user.Id,
    Action: "flag_ai",
    ModerationNote: "Reviewer confirmed AI elements"
), default);

Check(flagAiResult.Success, "ExecuteModerationDecision with flag_ai must succeed");
var flaggedArtwork = await db.Artworks.SingleAsync(a => a.Id == aiArtwork.Id);
Check(flaggedArtwork.IsAiGenerated, "Artwork must be flagged as AI generated");
Check(flaggedArtwork.AiDetectionScore >= 0.02m, "AiDetectionScore must be set or preserved");

// =========================================================================
// TEST 7: Xử lý ngoại lệ và lỗi mạng an toàn, không làm crash ứng dụng
// =========================================================================
mockScanner.NextResult = ArtworkScanResult.Fail("Rate limit exceeded 429");
var errorArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creator.Id,
    Title = "Error Fallback Test",
    ImageUrl = "https://storage.example.com/artworks/error.png",
    ModerationStatus = "Pending"
};
db.Artworks.Add(errorArtwork);
await db.SaveChangesAsync();

// Không ném exception ra ngoài:
await scanJob.ProcessArtworkScanAsync(errorArtwork.Id, default);
var dbErrorArtwork = await db.Artworks.SingleAsync(a => a.Id == errorArtwork.Id);
Check(dbErrorArtwork.FlagReason != null && dbErrorArtwork.FlagReason.Contains("429"), 
    "Failed scan must record failure in FlagReason without crashing job");

// Tác phẩm không tồn tại:
await scanJob.ProcessArtworkScanAsync(Guid.NewGuid(), default); // Must not throw

// =========================================================================
// TEST 8: Cấu hình động qua PlatformConfig (SCR-18 / SCR-20)
// =========================================================================
// Giả lập Admin siết chặt ngưỡng Adult xuống 0.50 và tắt AutoTagging qua PlatformConfig:
db.PlatformConfigs.Add(new PlatformConfig
{
    Id = Guid.NewGuid(),
    Key = PlatformConfigKeys.AiScanAdultThreshold,
    Value = "0.50",
    Description = "Strict adult threshold"
});
db.PlatformConfigs.Add(new PlatformConfig
{
    Id = Guid.NewGuid(),
    Key = PlatformConfigKeys.AiScanAutoTaggingEnabled,
    Value = "false",
    Description = "Disable auto tagging"
});
await db.SaveChangesAsync();

var borderlineArtwork = new Artwork
{
    Id = Guid.NewGuid(),
    CreatorProfileId = creator.Id,
    Title = "Borderline Adult Art",
    ImageUrl = "https://storage.example.com/artworks/borderline.png",
    ModerationStatus = "Pending"
};
db.Artworks.Add(borderlineArtwork);
await db.SaveChangesAsync();

// AdultScore = 0.55 (vượt qua ngưỡng mới 0.50 nhưng dưới ngưỡng cũ 0.70):
mockScanner.NextResult = ArtworkScanResult.Ok(
    safeScore: 0.80m,
    adultScore: 0.55m,
    violenceScore: 0.05m,
    isAiGenerated: false,
    aiDetectionScore: 0.10m,
    suggestedTags: new[] { "ShouldNotBeAdded" },
    flagReason: null // scanner mặc định không cắm cờ vì < 0.70
);

await scanJob.ProcessArtworkScanAsync(borderlineArtwork.Id, default);

var updatedBorderline = await db.Artworks
    .Include(a => a.ArtworkTags).ThenInclude(at => at.Tag)
    .SingleAsync(a => a.Id == borderlineArtwork.Id);

Check(updatedBorderline.FlagReason == "AI NSFW Threshold Exceeded",
    "Artwork must be flagged when adult score exceeds dynamic PlatformConfig threshold (0.50)");
Check(!updatedBorderline.ArtworkTags.Any(t => t.Tag!.Name == "ShouldNotBeAdded"),
    "Auto-tagging must be bypassed when AiScanAutoTaggingEnabled = false in PlatformConfig");

Console.WriteLine($"All {checks} Artwork AI Scan checks passed successfully!");

// =========================================================================
// Helper Mock Classes
// =========================================================================
class TestArtworkScanQueue : IArtworkScanQueue
{
    public List<Guid> EnqueuedIds { get; } = new();
    public void Enqueue(Guid artworkId) => EnqueuedIds.Add(artworkId);
}

class MockArtworkScanService : IArtworkScanService
{
    public ArtworkScanResult NextResult { get; set; } = ArtworkScanResult.Ok(1m, 0m, 0m, false, 0m);

    public Task<ArtworkScanResult> ScanArtworkAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NextResult);
    }
}

