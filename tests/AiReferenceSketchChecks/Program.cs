using ArtCommission.Application.Ai.Commands.StartReferenceSketchGeneration;
using ArtCommission.Application.Ai.Commands.SuggestLayoutHints;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Ai.Common;
using ArtCommission.Application.Ai.Queries.GetReferenceSketchQuota;
using ArtCommission.Application.Ai.ReferenceSketch;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Commission.Validators;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.Ai;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.BackgroundJobs;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using CommissionService = ArtCommission.Infrastructure.Services.CommissionService;

var checks = 0;

void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{name}: expected {expected}, got {actual}");
    checks++;
}

async Task Rejected(Func<Task> action, string name)
{
    try { await action(); }
    catch (ArgumentException) { checks++; return; }
    throw new Exception($"{name}: invalid operation was accepted");
}

AppDbContext NewDb(string name) => new(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("ai-ref-" + name + "-" + Guid.NewGuid())
    .Options);

async Task<(CreatorProfile Profile, CreatorTerms Terms)> SeedCreator(AppDbContext db, bool allowAiReferenceSketch)
{
    var profile = new CreatorProfile { UserId = Guid.NewGuid(), DisplayName = "Test creator" };
    db.CreatorProfiles.Add(profile);
    var terms = new CreatorTerms { CreatorProfileId = profile.Id, AllowAiReferenceSketch = allowAiReferenceSketch };
    db.CreatorTerms.Add(terms);
    await db.SaveChangesAsync();
    return (profile, terms);
}

// --- 1. Quota 3 lượt / 24h, chỉ đếm Succeeded (Blocked/Failed không tính) ---
{
    await using var db = NewDb("quota");
    var (profile, _) = await SeedCreator(db, allowAiReferenceSketch: true);
    var clientId = Guid.NewGuid();
    var handler = new StartReferenceSketchGenerationCommandHandler(db, new NoopQueue(), NullLogger<StartReferenceSketchGenerationCommandHandler>.Instance);
    var quotaHandler = new GetReferenceSketchQuotaQueryHandler(db);

    void SeedGeneration(AiReferenceGenerationStatus status, DateTimeOffset createdAt)
        => db.AiReferenceGenerations.Add(new AiReferenceGeneration
        {
            ClientId = clientId, CreatorProfileId = profile.Id, BriefText = "A valid brief description here",
            Status = status, CreatedAt = createdAt
        });

    SeedGeneration(AiReferenceGenerationStatus.Blocked, DateTimeOffset.UtcNow.AddHours(-1));
    SeedGeneration(AiReferenceGenerationStatus.Failed, DateTimeOffset.UtcNow.AddHours(-1));
    SeedGeneration(AiReferenceGenerationStatus.Succeeded, DateTimeOffset.UtcNow.AddHours(-20));
    SeedGeneration(AiReferenceGenerationStatus.Succeeded, DateTimeOffset.UtcNow.AddHours(-10));
    await db.SaveChangesAsync();

    var (quotaOk1, quota1, _) = await quotaHandler.Handle(new GetReferenceSketchQuotaQuery(clientId, profile.Id), default);
    Equal(true, quotaOk1, "AI-QUOTA-01: quota query succeeds");
    Equal(2, quota1!.Used, "AI-QUOTA-01: Blocked/Failed không tính vào quota đã dùng");

    var (startOk1, startData1, _) = await handler.Handle(
        new StartReferenceSketchGenerationCommand(clientId, profile.Id, "A valid brief description with enough length", null), default);
    Equal(true, startOk1, "AI-QUOTA-02: còn 1 lượt (2/3 Succeeded) thì vẫn tạo được lượt mới");
    Equal(1, NoopQueue.LastEnqueued == startData1!.GenerationId ? 1 : 0, "AI-QUOTA-02: lượt mới được enqueue vào job");

    SeedGeneration(AiReferenceGenerationStatus.Succeeded, DateTimeOffset.UtcNow.AddHours(-1));
    await db.SaveChangesAsync();

    var (quotaOk2, quota2, _) = await quotaHandler.Handle(new GetReferenceSketchQuotaQuery(clientId, profile.Id), default);
    Equal(3, quota2!.Used, "AI-QUOTA-03: 3 Succeeded trong 24h được tính đủ");

    var (startOk2, _, startErrors2) = await handler.Handle(
        new StartReferenceSketchGenerationCommand(clientId, profile.Id, "Another valid brief with enough length", null), default);
    Equal(false, startOk2, "AI-QUOTA-04: đủ 3 lượt Succeeded trong 24h thì bị chặn");
    Equal(true, startErrors2[0].Contains("hết lượt"), "AI-QUOTA-04: thông báo đúng lý do hết lượt");
}

// --- 2. Creator chưa bật AllowAiReferenceSketch -> bị chặn ở server, không chỉ ẩn nút FE ---
{
    await using var db = NewDb("toggle-off");
    var (profile, _) = await SeedCreator(db, allowAiReferenceSketch: false);
    var clientId = Guid.NewGuid();
    var handler = new StartReferenceSketchGenerationCommandHandler(db, new NoopQueue(), NullLogger<StartReferenceSketchGenerationCommandHandler>.Instance);

    var (ok, _, errors) = await handler.Handle(
        new StartReferenceSketchGenerationCommand(clientId, profile.Id, "A valid brief description with enough length", null), default);
    Equal(false, ok, "AI-TOGGLE-01: creator chưa bật tính năng -> server chặn (không chỉ ẩn nút FE)");
    Equal(true, errors[0].Contains("chưa cho phép"), "AI-TOGGLE-01: thông báo đúng lý do creator chưa bật");

    var (quotaOk, quota, _) = await new GetReferenceSketchQuotaQueryHandler(db).Handle(
        new GetReferenceSketchQuotaQuery(clientId, profile.Id), default);
    Equal(true, quotaOk, "AI-TOGGLE-02: quota query vẫn chạy được khi tắt");
    Equal(false, quota!.Enabled, "AI-TOGGLE-02: quota.enabled phản ánh đúng công tắc creator");
}

// --- 3. Hai phương án trùng pose/camera/composition -> bị từ chối (gọi lại 1 lần trước khi fail) ---
{
    const string duplicatePair = "{\"hasCharacter\":true,\"wontDrawEn\":\"\",\"variants\":[" +
                                  "{\"pose\":\"Standing\",\"camera\":\"Front\",\"composition\":\"Close-up\",\"promptEn\":\"a\",\"labelVi\":\"x\",\"labelEn\":\"x\"}," +
                                  "{\"pose\":\"Standing\",\"camera\":\"Front\",\"composition\":\"Close-up\",\"promptEn\":\"b\",\"labelVi\":\"y\",\"labelEn\":\"y\"}]}";
    const string distinctPairWithCharacter = "{\"hasCharacter\":true,\"wontDrawEn\":\"nudity, hate symbols.\",\"variants\":[" +
                                 "{\"pose\":\"Standing\",\"camera\":\"Front\",\"composition\":\"Close-up\",\"promptEn\":\"a\",\"labelVi\":\"x\",\"labelEn\":\"x\"}," +
                                 "{\"pose\":\"Sitting\",\"camera\":\"Side\",\"composition\":\"Wide shot\",\"promptEn\":\"b\",\"labelVi\":\"y\",\"labelEn\":\"y\"}]}";
    const string distinctPairNoCharacter = "{\"hasCharacter\":false,\"wontDrawEn\":\"\",\"variants\":[" +
                                 "{\"pose\":\"Standing\",\"camera\":\"Front\",\"composition\":\"Close-up\",\"promptEn\":\"a\",\"labelVi\":\"x\",\"labelEn\":\"x\"}," +
                                 "{\"pose\":\"Sitting\",\"camera\":\"Side\",\"composition\":\"Wide shot\",\"promptEn\":\"b\",\"labelVi\":\"y\",\"labelEn\":\"y\"}]}";

    var alwaysDuplicate = new FakeAiChatClient(duplicatePair, duplicatePair);
    var serviceAlwaysDuplicate = new ReferenceSketchPromptService(alwaysDuplicate, NullLogger<ReferenceSketchPromptService>.Instance);
    var failResult = await serviceAlwaysDuplicate.BuildVariantsAsync("brief", null, null);
    Equal(false, failResult.Success, "AI-VARIANTS-01: 2 lần gọi đều trùng cả 3 trường -> từ chối, không tính lượt");
    Equal(2, alwaysDuplicate.CallCount, "AI-VARIANTS-01: có gọi lại đúng 1 lần (tổng 2 lần) trước khi fail");

    var duplicateThenDistinct = new FakeAiChatClient(duplicatePair, distinctPairWithCharacter);
    var serviceRetrySucceeds = new ReferenceSketchPromptService(duplicateThenDistinct, NullLogger<ReferenceSketchPromptService>.Instance);
    var retryResult = await serviceRetrySucceeds.BuildVariantsAsync("brief", null, "Nội dung khiêu dâm, biểu tượng thù hận.");
    Equal(true, retryResult.Success, "AI-VARIANTS-02: lần đầu trùng, lần gọi lại khác biệt -> chấp nhận");
    Equal(2, retryResult.Variants!.Length, "AI-VARIANTS-02: trả đúng 2 phương án");
    Equal(true, retryResult.Variants[0].PromptEn.Contains("nudity, hate symbols"), "AI-VARIANTS-02: promptEn ghép câu cấm từ bản dịch tiếng Anh wontDrawEn (không phải wontDraw tiếng Việt gốc)");
    Equal(false, retryResult.Variants[0].PromptEn.Contains(".."), "AI-VARIANTS-02: không bị lặp dấu chấm thừa khi wontDrawEn đã có dấu . ở cuối");
    Equal(true, retryResult.Variants[0].PromptEn.Contains(ReferenceSketchPromptService.MannequinFigureClause), "AI-VARIANTS-02: hasCharacter=true thì ghép câu phác mặt");

    var alwaysDistinct = new FakeAiChatClient(distinctPairNoCharacter);
    var serviceFirstTrySucceeds = new ReferenceSketchPromptService(alwaysDistinct, NullLogger<ReferenceSketchPromptService>.Instance);
    var firstTryResult = await serviceFirstTrySucceeds.BuildVariantsAsync("brief", null, null);
    Equal(true, firstTryResult.Success, "AI-VARIANTS-03: khác biệt ngay lần đầu -> không cần gọi lại");
    Equal(1, alwaysDistinct.CallCount, "AI-VARIANTS-03: chỉ gọi AI 1 lần khi lần đầu đã đạt");
    Equal(false, firstTryResult.Variants![0].PromptEn.Contains(ReferenceSketchPromptService.MannequinFigureClause), "AI-VARIANTS-03: hasCharacter=false thì KHÔNG ghép câu phác mặt");
}

// --- 4. Tổng ảnh upload + ảnh AI vượt quá 10 -> 400 ---
{
    var validator = new CreateCommissionRequestValidator();
    var tooMany = new CreateCommissionRequest
    {
        CreatorId = Guid.NewGuid(), PackageId = Guid.NewGuid(), Title = "Order",
        ReferenceImages = Enumerable.Range(0, 8).Select(i => $"https://cdn.example.com/ref-{i}.png").ToList(),
        AiReferenceImageIds = [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]
    };
    Equal(false, validator.Validate(tooMany).IsValid, "AI-REFCOUNT-01: 8 ảnh upload + 3 ảnh AI = 11 > 10 bị từ chối");

    var exactlyTen = new CreateCommissionRequest
    {
        CreatorId = Guid.NewGuid(), PackageId = Guid.NewGuid(), Title = "Order",
        ReferenceImages = Enumerable.Range(0, 8).Select(i => $"https://cdn.example.com/ref-{i}.png").ToList(),
        AiReferenceImageIds = [Guid.NewGuid(), Guid.NewGuid()]
    };
    Equal(true, validator.Validate(exactlyTen).IsValid, "AI-REFCOUNT-02: đúng 10 ảnh (8 upload + 2 AI) được chấp nhận");

    var freestyleWithAi = new CreateCommissionRequest
    {
        CreatorId = Guid.NewGuid(), PackageId = Guid.NewGuid(), Title = "Order",
        IsFreestyle = true, AiReferenceImageIds = [Guid.NewGuid()]
    };
    Equal(false, validator.Validate(freestyleWithAi).IsValid, "AI-FREESTYLE-01: freestyle + chọn ảnh AI cùng lúc bị từ chối");
}

// --- 5. Ảnh AI thuộc client/creator khác -> không thể gắn vào đơn của mình ---
{
    await using var db = NewDb("cross-client");
    var packageId = Guid.NewGuid();
    var (profile, _) = await SeedCreator(db, allowAiReferenceSketch: true);
    profile.RateCardJson = JsonSerializer.Serialize(new[] { new RateCardPackageDto(packageId, "Package", null, 300m,
        Enumerable.Range(1, 3).Select(i => new RateCardMilestoneDto(i, $"Stage {i}", 100m)).ToList()) });
    var legitClientId = Guid.NewGuid();
    var attackerClientId = Guid.NewGuid();
    db.Wallets.Add(new Wallet { UserId = legitClientId, Balance = 10000m });
    db.Wallets.Add(new Wallet { UserId = attackerClientId, Balance = 10000m });

    var othersImage = new AiReferenceImage
    {
        GenerationId = Guid.NewGuid(), TempStorageKey = "ai-ref-temp/other/0.png", LabelVi = "a", LabelEn = "a",
        Generation = new AiReferenceGeneration
        {
            ClientId = legitClientId, CreatorProfileId = profile.Id, BriefText = "someone else's brief",
            Status = AiReferenceGenerationStatus.Succeeded
        }
    };
    var expiredOwnImage = new AiReferenceImage
    {
        GenerationId = Guid.NewGuid(), TempStorageKey = "ai-ref-temp/expired/0.png", LabelVi = "a", LabelEn = "a",
        CreatedAt = DateTimeOffset.UtcNow.AddHours(-25),
        Generation = new AiReferenceGeneration
        {
            ClientId = attackerClientId, CreatorProfileId = profile.Id, BriefText = "own expired brief",
            Status = AiReferenceGenerationStatus.Succeeded, CreatedAt = DateTimeOffset.UtcNow.AddHours(-25)
        }
    };
    db.AiReferenceImages.AddRange(othersImage, expiredOwnImage);
    await db.SaveChangesAsync();

    var service = new CommissionService(db, new WalletService(db), new TestWatermark(), new TestStorage(), new VoucherCheckService(db), new TestNotificationPublisher());

    await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = profile.Id, PackageId = packageId, Title = "stolen ai reference",
        AiReferenceImageIds = [othersImage.Id]
    }, attackerClientId), "AI-CROSSOWNER-01: ảnh AI của khách khác không thể gắn vào đơn của mình");

    await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = profile.Id, PackageId = packageId, Title = "expired ai reference",
        AiReferenceImageIds = [expiredOwnImage.Id]
    }, attackerClientId), "AI-CROSSOWNER-02: ảnh AI của chính mình nhưng đã quá 24h không thể gắn vào đơn");

    var created = await service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = profile.Id, PackageId = packageId, Title = "legit order"
    }, legitClientId);
    Equal(true, created.Id != Guid.Empty, "AI-CROSSOWNER-03: đơn không ảnh AI vẫn tạo bình thường (sanity check)");
}

// --- 6. Hangfire cleanup job: chỉ xoá ảnh temp quá 24h chưa gắn đơn ---
{
    await using var db = NewDb("cleanup");
    var storage = new TestStorage();
    var keepByCommission = new AiReferenceImage { TempStorageKey = "ai-ref-temp/a/0.png", LabelVi = "a", LabelEn = "a", CommissionId = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow.AddHours(-48) };
    var keepFresh = new AiReferenceImage { TempStorageKey = "ai-ref-temp/b/0.png", LabelVi = "b", LabelEn = "b", CreatedAt = DateTimeOffset.UtcNow.AddHours(-1) };
    var deleteExpired = new AiReferenceImage { TempStorageKey = "ai-ref-temp/c/0.png", LabelVi = "c", LabelEn = "c", CreatedAt = DateTimeOffset.UtcNow.AddHours(-25) };
    db.AiReferenceImages.AddRange(keepByCommission, keepFresh, deleteExpired);
    using var ms = new MemoryStream(new byte[] { 1 });
    await storage.UploadPrivateAsync(ms, deleteExpired.TempStorageKey, "image/png");
    await db.SaveChangesAsync();

    var job = new AiReferenceTempCleanupJob(db, storage, NullLogger<AiReferenceTempCleanupJob>.Instance);
    await job.RunAsync(default);

    Equal(false, (await db.AiReferenceImages.FindAsync(keepByCommission.Id))!.IsDeleted, "AI-CLEANUP-01: ảnh đã gắn đơn không bị xoá dù quá 24h");
    Equal(false, (await db.AiReferenceImages.FindAsync(keepFresh.Id))!.IsDeleted, "AI-CLEANUP-02: ảnh chưa quá 24h không bị xoá");
    Equal(true, (await db.AiReferenceImages.FindAsync(deleteExpired.Id))!.IsDeleted, "AI-CLEANUP-03: ảnh quá 24h chưa gắn đơn bị đánh dấu xoá");
    Equal(false, storage.UploadedPrivate.ContainsKey(deleteExpired.TempStorageKey), "AI-CLEANUP-03: object R2 tương ứng cũng bị xoá");
}

// --- 7. Nút "🎲 Gợi ý bố cục độc đáo" (T11) — không trừ lượt sinh ảnh ---
{
    // creator chưa bật tính năng -> bị chặn ở server (giống nút ✨)
    await using var dbOff = NewDb("hints-toggle-off");
    var (profileOff, _) = await SeedCreator(dbOff, allowAiReferenceSketch: false);
    var hintsServiceOff = new ReferenceSketchPromptService(new FakeAiChatClient("{}"), NullLogger<ReferenceSketchPromptService>.Instance);
    var handlerOff = new SuggestLayoutHintsCommandHandler(dbOff, hintsServiceOff);
    var (hintsOkOff, _, hintsErrorsOff) = await handlerOff.Handle(
        new SuggestLayoutHintsCommand(Guid.NewGuid(), profileOff.Id, "A valid brief description with enough length", null), default);
    Equal(false, hintsOkOff, "AI-HINTS-01: creator chưa bật tính năng -> server chặn");
    Equal(true, hintsErrorsOff[0].Contains("chưa cho phép"), "AI-HINTS-01: thông báo đúng lý do creator chưa bật");

    // brief < 30 ký tự -> bị chặn bởi validator (dùng chung giới hạn ReferenceSketchLimits.MinBriefLength)
    var shortBriefValid = new SuggestLayoutHintsCommandValidator()
        .Validate(new SuggestLayoutHintsCommand(Guid.NewGuid(), Guid.NewGuid(), "too short", null));
    Equal(false, shortBriefValid.IsValid, "AI-HINTS-02: brief < 30 ký tự bị validator chặn");

    // 3 kỹ thuật được chọn ngẫu nhiên luôn khác nhau
    var pickedTechniques = LayoutHintTechniques.PickRandom(3);
    Equal(3, pickedTechniques.Distinct().Count(), "AI-HINTS-03: 3 kỹ thuật được chọn khác nhau");

    // gợi ý > 300 ký tự bị cắt về đúng giới hạn ô "Gợi ý bố cục"
    var tooLongHint = new string('a', 350);
    var longHintJson = JsonSerializer.Serialize(new
    {
        hints = new[]
        {
            new { technique = "worm's-eye view", textVi = tooLongHint },
            new { technique = "bird's-eye view", textVi = "Gợi ý 2" },
            new { technique = "Dutch angle", textVi = "Gợi ý 3" },
        }
    });
    var hintsServiceLong = new ReferenceSketchPromptService(new FakeAiChatClient(longHintJson), NullLogger<ReferenceSketchPromptService>.Instance);
    var longResult = await hintsServiceLong.SuggestLayoutHintsAsync("brief", null, null, ["worm's-eye view", "bird's-eye view", "Dutch angle"]);
    Equal(true, longResult.Success, "AI-HINTS-04: 3 gợi ý hợp lệ -> chấp nhận");
    Equal(300, longResult.Hints![0].TextVi.Length, "AI-HINTS-04: gợi ý > 300 ký tự bị cắt về đúng 300");

    // không đụng tới quota sinh ảnh (3 lượt/24h chỉ tính StartReferenceSketchGenerationCommand)
    await using var dbQuota = NewDb("hints-no-quota");
    var (profileQuota, _) = await SeedCreator(dbQuota, allowAiReferenceSketch: true);
    var clientIdQuota = Guid.NewGuid();
    var hintsServiceOk = new ReferenceSketchPromptService(new FakeAiChatClient(longHintJson), NullLogger<ReferenceSketchPromptService>.Instance);
    var handlerOk = new SuggestLayoutHintsCommandHandler(dbQuota, hintsServiceOk);
    var (hintsOkOk, hintsDataOk, _) = await handlerOk.Handle(
        new SuggestLayoutHintsCommand(clientIdQuota, profileQuota.Id, "A valid brief description with enough length", null), default);
    Equal(true, hintsOkOk, "AI-HINTS-05: creator đã bật + brief hợp lệ -> thành công");
    Equal(3, hintsDataOk!.Hints.Length, "AI-HINTS-05: trả đúng 3 gợi ý");
    var (quotaOkAfterHints, quotaAfterHints, _) = await new GetReferenceSketchQuotaQueryHandler(dbQuota).Handle(
        new GetReferenceSketchQuotaQuery(clientIdQuota, profileQuota.Id), default);
    Equal(true, quotaOkAfterHints, "AI-HINTS-06: quota query vẫn chạy được sau khi gọi gợi ý bố cục");
    Equal(0, quotaAfterHints!.Used, "AI-HINTS-06: gọi gợi ý bố cục KHÔNG trừ/tính vào lượt sinh ảnh 3 lượt/24h");
}

// --- 8. Giới hạn lượt sketch cấu hình bởi admin tại /admin/fee-policy (docs/ai-reference/PLAN.md T12) ---
{
    await using var db = NewDb("quota-admin-configured");
    var (profile, _) = await SeedCreator(db, allowAiReferenceSketch: true);
    var clientId = Guid.NewGuid();
    db.PlatformConfigs.Add(new PlatformConfig { Key = PlatformConfigKeys.AiReferenceSketchMaxGenerationsPerWindow, Value = "5" });
    await db.SaveChangesAsync();

    var handler = new StartReferenceSketchGenerationCommandHandler(db, new NoopQueue(), NullLogger<StartReferenceSketchGenerationCommandHandler>.Instance);
    var quotaHandler = new GetReferenceSketchQuotaQueryHandler(db);

    void SeedGeneration(DateTimeOffset createdAt)
        => db.AiReferenceGenerations.Add(new AiReferenceGeneration
        {
            ClientId = clientId, CreatorProfileId = profile.Id, BriefText = "A valid brief description here",
            Status = AiReferenceGenerationStatus.Succeeded, CreatedAt = createdAt
        });

    for (var i = 0; i < 4; i++) SeedGeneration(DateTimeOffset.UtcNow.AddHours(-1));
    await db.SaveChangesAsync();

    var (quotaOk, quota, _) = await quotaHandler.Handle(new GetReferenceSketchQuotaQuery(clientId, profile.Id), default);
    Equal(true, quotaOk, "AI-QUOTA-ADMIN-01: quota query thành công");
    Equal(5, quota!.Limit, "AI-QUOTA-ADMIN-01: Limit đọc đúng từ PlatformConfig (5) thay vì mặc định 3");

    var (startOk, _, _) = await handler.Handle(
        new StartReferenceSketchGenerationCommand(clientId, profile.Id, "A valid brief description with enough length", null), default);
    Equal(true, startOk, "AI-QUOTA-ADMIN-02: còn 1 lượt (4/5 Succeeded) theo giới hạn admin cấu hình thì vẫn tạo được lượt mới");
}
{
    await using var db = NewDb("quota-admin-default");
    var (profile, _) = await SeedCreator(db, allowAiReferenceSketch: true);
    var clientId = Guid.NewGuid();
    // Không seed PlatformConfig nào — phải fallback về ReferenceSketchLimits.MaxGenerationsPerWindow (3).
    var quotaHandler = new GetReferenceSketchQuotaQueryHandler(db);
    var (quotaOk, quota, _) = await quotaHandler.Handle(new GetReferenceSketchQuotaQuery(clientId, profile.Id), default);
    Equal(true, quotaOk, "AI-QUOTA-ADMIN-03: quota query thành công khi chưa có config");
    Equal(ReferenceSketchLimits.MaxGenerationsPerWindow, quota!.Limit, "AI-QUOTA-ADMIN-03: chưa cấu hình -> fallback đúng mặc định 3");
    Equal(0m, quota.DiscountPercent, "AI-QUOTA-ADMIN-03: chưa cấu hình -> DiscountPercent fallback đúng mặc định 0");
}

// AI-TONES: storyboard post-processing leaves only the 4 allowed flat tones.
{
    using var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(96, 96);
    img.ProcessPixelRows(acc =>
    {
        for (var y = 0; y < acc.Height; y++)
        {
            var row = acc.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                // Colored gradient background + a thick dark stroke in the middle.
                row[x] = x is >= 44 and < 52
                    ? new SixLabors.ImageSharp.PixelFormats.Rgba32(10, 10, 20)
                    : new SixLabors.ImageSharp.PixelFormats.Rgba32((byte)(x * 2 + 60), (byte)(y * 2 + 60), 200);
            }
        }
    });
    ReferenceSketchImageProtector.SimplifyToStoryboardTones(img);

    var allowed = new[]
    {
        ReferenceSketchImageProtector.LineTone, ReferenceSketchImageProtector.ShadowTone,
        ReferenceSketchImageProtector.BackdropTone, ReferenceSketchImageProtector.PaperTone,
    };
    var outside = 0;
    var lineCount = 0;
    img.ProcessPixelRows(acc =>
    {
        for (var y = 0; y < acc.Height; y++)
            foreach (var p in acc.GetRowSpan(y))
            {
                if (!allowed.Contains(p)) outside++;
                if (p == ReferenceSketchImageProtector.LineTone) lineCount++;
            }
    });
    Equal(0, outside, "AI-TONES-01: mọi pixel thuộc 4 tông storyboard");
    Equal(true, lineCount > 0, "AI-TONES-02: nét đậm vẫn còn sau khi ép tông");
    Equal(ReferenceSketchImageProtector.ShadowTone, img[48, 48], "AI-TONES-03: lòng mảng tối dày thành xám phẳng, không thành khối xanh than");
}

Console.WriteLine($"Passed {checks} AI reference sketch checks.");

sealed class NoopQueue : IReferenceSketchGenerationQueue
{
    public static Guid LastEnqueued;
    public void Enqueue(Guid generationId) => LastEnqueued = generationId;
}

sealed class FakeAiChatClient : IAiChatClient
{
    private readonly Queue<string> _responses;
    public int CallCount { get; private set; }

    public FakeAiChatClient(params string[] responses) => _responses = new Queue<string>(responses);

    public Task<AiCompletionResult> CompleteAsync(string? systemInstruction, IReadOnlyList<AiChatTurn> history, string userMessage, CancellationToken cancellationToken = default, int? maxOutputTokens = null, bool jsonResponse = false)
    {
        CallCount++;
        var content = _responses.Count > 0 ? _responses.Dequeue() : _responses.Last();
        return Task.FromResult(AiCompletionResult.Ok(content, null, "fake-model"));
    }

    public Task<AiCompletionResult> CompleteWithToolsAsync(string? systemInstruction, IReadOnlyList<AiChatTurn> history, string userMessage, IReadOnlyList<IAiTool> tools, CancellationToken cancellationToken = default)
        => CompleteAsync(systemInstruction, history, userMessage, cancellationToken);
}

sealed class TestWatermark : IWatermarkService
{
    public Task<Stream> ApplyWatermarkAsync(Stream stream, string text, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream(new byte[] { 4, 5, 6 }));
}

sealed class TestStorage : IStorageService
{
    public Dictionary<string, byte[]> UploadedPrivate { get; } = [];
    public Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default) => Task.FromResult(key);
    public Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
    {
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        UploadedPrivate[key] = copy.ToArray();
        return Task.FromResult(key);
    }
    public Task<Stream?> OpenPrivateReadAsync(string key, CancellationToken ct = default)
        => Task.FromResult<Stream?>(UploadedPrivate.TryGetValue(key, out var bytes) ? new MemoryStream(bytes) : null);
    public string GeneratePresignedDownloadUrl(string key, TimeSpan expiry) => "https://example.test/" + key;
    public Task DeletePrivateAsync(string key, CancellationToken ct = default)
    {
        UploadedPrivate.Remove(key);
        return Task.CompletedTask;
    }
}

sealed class TestNotificationPublisher : INotificationPublisher
{
    public Task<NotificationDto?> PublishAsync(
        Guid userId, NotificationType notificationType, string title, string body,
        string? refType = null, Guid? refId = null, NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null, CancellationToken cancellationToken = default)
        => Task.FromResult<NotificationDto?>(null);
}
