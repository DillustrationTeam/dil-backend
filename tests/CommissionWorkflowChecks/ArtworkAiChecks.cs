using System.Net;
using System.Text.Json;
using ArtCommission.Infrastructure.ExternalServices.Gemini;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ArtCommission.Application.ArtistStudio.Ai;
using ArtCommission.Application.ArtistStudio.Commands.UploadArtwork;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using ArtCommission.Infrastructure.ExternalServices.ArtworkAi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

static class ArtworkAiChecks
{
    public static async Task RunAsync()
    {
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; }
        async Task Reject(Func<Task> action, string name) { try { await action(); } catch (Exception e) when (e is ArgumentException or InvalidOperationException or UnauthorizedAccessException) { checks++; return; } throw new Exception(name); }
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase("artwork-ai-" + Guid.NewGuid()).Options;
        await using var db = new AppDbContext(options);
        var creator = new CreatorProfile { UserId = Guid.NewGuid(), DisplayName = "AI checks" };
        var buyer = Guid.NewGuid();
        db.CreatorProfiles.Add(creator);
        var seed = new Artwork { CreatorProfile = creator, Title = "Liked fantasy", ImageUrl = "https://res.cloudinary.com/seed.png", ModerationStatus = "Approved" };
        var match = new Artwork { CreatorProfile = creator, Title = "Fantasy recommendation", ImageUrl = "https://res.cloudinary.com/match.png", ModerationStatus = "Approved" };
        var hidden = new Artwork { CreatorProfile = creator, Title = "Hidden fantasy", ImageUrl = "https://res.cloudinary.com/hidden.png", ModerationStatus = "Hidden" };
        var pending = new Artwork { CreatorProfile = creator, Title = "Pending fantasy", ImageUrl = "https://res.cloudinary.com/pending.png" };
        var tag = new Tag { Name = "fantasy" }; db.Add(tag);
        foreach (var a in new[] { seed, match, hidden, pending }) { db.Artworks.Add(a); db.Add(new ArtworkTag { ArtworkId = a.Id, Tag = tag }); }
        db.ArtworkFavorites.Add(new ArtworkFavorite { ArtworkId = seed.Id, UserId = buyer });
        await db.SaveChangesAsync();
        var recommend = new GetArtworkRecommendationsHandler(db);
        var recommendations = await recommend.Handle(new(buyer), default);
        Check(recommendations.Count == 1 && recommendations[0].Artwork.Id == match.Id, "recommendations exclude hidden/pending/liked");
        Check(recommendations[0].Reason.Contains("fantasy"), "recommendation reason explains matching tag");
        var cold = await recommend.Handle(new(Guid.NewGuid()), default);
        Check(cold.Count == 2 && cold.All(x => x.Artwork.ModerationStatus == "Approved"), "cold start returns only approved artworks");
        Check((await recommend.Handle(new(creator.UserId), default)).Count == 0, "creator's own artwork not recommended to self");
        var detector = new StubDetector();
        var service = new ArtworkAiScanService(db, detector);
        await Reject(() => service.GetAsync(pending.Id, buyer, false, default), "outsider cannot read scan");
        Check((await service.GetAsync(pending.Id, creator.UserId, false, default)).Score is null, "unscanned artwork has no fake score");
        Check((await service.GetAsync(seed.Id, creator.UserId, false, default)).Status == "NotRequested", "legacy approved artwork does not pretend to have a queued scan");
        detector.IsConfigured = false;
        Check((await service.GetAsync(pending.Id, creator.UserId, false, default)).Status == "NotConfigured", "missing key has explicit status");
        Check(!await service.ProcessNextAsync(default), "missing key never calls detector");
        detector.IsConfigured = true;
        Check(await service.ProcessNextAsync(default), "worker processes pending artwork");
        Check(pending.AiScanStatus == "Succeeded" && pending.AiDetectionScore == 0.1234m, "worker persists actual score");
        Check(pending.ModerationStatus == "Pending" && !pending.IsAiGenerated, "detector never auto-approves or overwrites creator declaration");
        await Reject(() => service.RetryAsync(pending.Id, Guid.NewGuid(), default), "retry has cooldown");
        pending.AiScanUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10); await db.SaveChangesAsync();
        await service.RetryAsync(pending.Id, Guid.NewGuid(), default);
        Check(pending.AiScanStatus == "Queued" && pending.AiDetectionScore is null, "manual retry clears previous score");
        detector.Fail = true;
        for (var i = 0; i < 3; i++) {
            Check(await service.ProcessNextAsync(default), "worker retry attempt");
            pending.AiScanUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10); await db.SaveChangesAsync();
        }
        Check(pending.AiScanStatus == "Failed" && pending.AiDetectionScore is null && pending.AiScanAttempts == 3, "bounded retry never fabricates a success score");
        Check(!await service.ProcessNextAsync(default), "failed scan is not retried endlessly");
        detector.Fail = false;
        pending.ImageUrl = "https://res.cloudinary.com/new.png"; await db.SaveChangesAsync();
        Check((await service.GetAsync(pending.Id, creator.UserId, false, default)).Status == "Queued", "image replacement invalidates old result");
        Check(await service.ProcessNextAsync(default) && pending.AiScanAttempts == 1, "new image resets retry budget");
        pending.AiScanStatus = "Queued"; pending.AiScanUpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-10); await db.SaveChangesAsync();
        detector.During = async () => {
            await using var other = new AppDbContext(options);
            var a = await other.Artworks.SingleAsync(x => x.Id == pending.Id);
            a.ImageUrl = "https://res.cloudinary.com/changed-during-scan.png";
            a.AiScanStatus = "Queued"; a.AiScanUpdatedAt = DateTimeOffset.UtcNow;
            await other.SaveChangesAsync();
        };
        await service.ProcessNextAsync(default);
        Check(pending.ImageUrl.EndsWith("changed-during-scan.png") && pending.AiScanStatus == "Queued", "late result cannot overwrite replaced image state");
        detector.During = null;
        var valid = GeminiArtworkMetadataService.Validate(new(" Title ", " Description ", new[] { "fantasy", "Fantasy" }, "anime"));
        Check(valid.Title == "Title" && valid.Tags.Count == 1, "metadata trims and deduplicates tags");
        await Reject(() => { GeminiArtworkMetadataService.Validate(new("x", "d", Enumerable.Repeat("tag", 11).ToArray(), "anime")); return Task.CompletedTask; }, "metadata rejects malformed output");
        var upload = new UploadArtworkCommandHandler(db);
        var created = await upload.Handle(new(creator.UserId, "Do not trust browser score", null, "https://res.cloudinary.com/new-upload.png", null, false, 0.99m), default);
        Check(created.Success && created.Data!.AiDetectionScore is null, "upload ignores client-supplied detector score");
        var transport = new StubTransport("{\"status\":\"success\",\"type\":{\"ai_generated\":0.4321}}");
        var provider = new SightengineArtworkDetector(new HttpClient(transport), Options.Create(new ArtworkAiOptions { SightengineApiUser = "test-user", SightengineApiSecret = "test-secret" }), new ConfigurationBuilder().Build());
        Check(await provider.DetectAsync("https://res.cloudinary.com/check.png", default) == 0.4321m, "provider parses score contract");
        Check(transport.LastUri == "https://api.sightengine.com/1.0/check.json", "provider does not put secrets in request URL");
        await Reject(() => provider.DetectAsync("http://127.0.0.1/private", default), "detector rejects arbitrary private URL");
        transport.Json = "{\"status\":\"success\",\"type\":{\"ai_generated\":2}}";
        await Reject(() => provider.DetectAsync("https://res.cloudinary.com/check.png", default), "provider rejects out-of-range score");
        // Provider failure and legacy upload compatibility audit; no external requests.
        using var sample = new MemoryStream();
        using (var image = new Image<Rgba32>(16, 16)) await image.SaveAsPngAsync(sample);
        var goodSuggestion = JsonSerializer.Serialize(new { title = "Audit artwork", description = "Description", tags = new[] { "fantasy" }, style = "anime" });
        var goodEnvelope = JsonSerializer.Serialize(new { candidates = new[] { new { content = new { parts = new[] { new { text = goodSuggestion } } } } } });
        var attempts = 0;
        var fallbackTransport = new MetadataTransport((request, ct) => Task.FromResult(++attempts == 1
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(goodEnvelope) }));
        var metadata = new GeminiArtworkMetadataService(new HttpClient(fallbackTransport), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
        sample.Position = 0;
        Check((await metadata.SuggestAsync(sample, default)).Title == "Audit artwork" && attempts == 2, "metadata falls back after provider 503");
        var malformed = new GeminiArtworkMetadataService(new HttpClient(new MetadataTransport((r, ct) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not-json") }))), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
        sample.Position = 0;
        await Reject(() => malformed.SuggestAsync(sample, default), "malformed outer JSON returns controlled error");
        var timeout = new GeminiArtworkMetadataService(new HttpClient(new MetadataTransport((r, ct) => Task.FromException<HttpResponseMessage>(new TaskCanceledException("simulated provider timeout")))), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
        sample.Position = 0;
        await Reject(() => timeout.SuggestAsync(sample, default), "provider timeout returns controlled error");
        foreach (var badResponse in new[] { "not-json", "{\"candidates\":[]}", "{\"candidates\":[{\"content\":{\"parts\":[{\"text\":\"not-json\"}]}}]}" })
        {
            attempts = 0;
            var recovering = new GeminiArtworkMetadataService(new HttpClient(new MetadataTransport((r, ct) =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(++attempts == 1 ? badResponse : goodEnvelope) }))), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
            sample.Position = 0;
            Check((await recovering.SuggestAsync(sample, default)).Title == "Audit artwork" && attempts == 2, "invalid response falls back successfully");
        }
        attempts = 0;
        var recoveringTimeout = new GeminiArtworkMetadataService(new HttpClient(new MetadataTransport((r, ct) => ++attempts == 1
            ? Task.FromException<HttpResponseMessage>(new TaskCanceledException())
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(goodEnvelope) }))), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
        sample.Position = 0;
        Check((await recoveringTimeout.SuggestAsync(sample, default)).Title == "Audit artwork" && attempts == 2, "provider timeout can recover on fallback");
        using var callerCancellation = new CancellationTokenSource();
        var cancelled = new GeminiArtworkMetadataService(new HttpClient(new MetadataTransport((r, ct) => {
            callerCancellation.Cancel();
            return Task.FromCanceled<HttpResponseMessage>(ct);
        })), Options.Create(new GeminiOptions { ApiKey = "test-only-key" }), Options.Create(new ArtworkAiOptions()));
        sample.Position = 0;
        try { await cancelled.SuggestAsync(sample, callerCancellation.Token); throw new Exception("caller cancellation swallowed"); }
        catch (OperationCanceledException) { Check(callerCancellation.IsCancellationRequested, "caller cancellation propagates"); }
        var localRoot = Path.Combine(Path.GetTempPath(), "artwork-ai-audit-" + Guid.NewGuid().ToString("N"));
        var localFolder = Path.Combine(localRoot, "public", "artworks"); Directory.CreateDirectory(localFolder);
        var localFile = Path.Combine(localFolder, "large.png");
        try {
            await using (var file = File.Create(localFile)) { sample.Position = 0; await sample.CopyToAsync(file); file.SetLength(11 * 1024 * 1024); }
            var localConfiguration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LocalStorage:RootPath"] = localRoot, ["LocalStorage:BaseUrl"] = "http://localhost:5075" }).Build();
            long outboundBytes = 0;
            var localDetector = new SightengineArtworkDetector(new HttpClient(new MetadataTransport(async (request, ct) => {
                var media = ((MultipartFormDataContent)request.Content!).Single(x => x.Headers.ContentDisposition?.Name?.Trim('"') == "media");
                var bytes = await media.ReadAsByteArrayAsync(ct);
                outboundBytes = bytes.Length;
                using var decoded = Image.Load(bytes);
                Check(media.Headers.ContentType?.MediaType == "image/jpeg" && decoded.Width == 16, "large upload sends decodable prepared JPEG");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"success\",\"type\":{\"ai_generated\":0.1}}") };
            })), Options.Create(new ArtworkAiOptions { SightengineApiUser = "test-user", SightengineApiSecret = "test-secret" }), localConfiguration);
            Check(await localDetector.DetectAsync("http://localhost:5075/demo-uploads/public/artworks/large.png", default) == 0.1m, "11 MB upload can be scanned");
            Check(outboundBytes < 10 * 1024 * 1024 && new FileInfo(localFile).Length == 11 * 1024 * 1024, "provider copy is small and original is unchanged");
            await using (var file = File.OpenWrite(localFile)) file.SetLength(25 * 1024 * 1024);
            Check(await localDetector.DetectAsync("http://localhost:5075/demo-uploads/public/artworks/large.png", default) == 0.1m, "25 MB upload boundary can be scanned");
            await using (var file = File.OpenWrite(localFile)) file.SetLength(25 * 1024 * 1024 + 1);
            await Reject(() => localDetector.DetectAsync("http://localhost:5075/demo-uploads/public/artworks/large.png", default), "over 25 MB is rejected");
        } finally { File.Delete(localFile); Directory.Delete(localFolder); Directory.Delete(Path.Combine(localRoot, "public")); Directory.Delete(localRoot); }
        Console.WriteLine($"Passed {checks} artwork AI checks.");
    }
    sealed class MetadataTransport(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
    }
    sealed class StubDetector : IArtworkAiDetector
    {
        public bool IsConfigured { get; set; } = true;
        public bool Fail { get; set; }
        public Func<Task>? During { get; set; }
        public async Task<decimal> DetectAsync(string imageUrl, CancellationToken ct) { if (During != null) await During(); if (Fail) throw new HttpRequestException(); return 0.1234m; }
    }
    sealed class StubTransport(string json) : HttpMessageHandler
    {
        public string Json { get; set; } = json;
        public string? LastUri { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { LastUri = request.RequestUri?.ToString(); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json) }); }
    }
}
