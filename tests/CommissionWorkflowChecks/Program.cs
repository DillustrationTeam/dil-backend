using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Domain.Entities.Payment;
using System.Text.Json;
using CommissionService = ArtCommission.Infrastructure.Services.CommissionService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var clientId = Guid.NewGuid();
var creatorId = Guid.NewGuid();
var roleId = Guid.NewGuid();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("commission-" + Guid.NewGuid()).Options);
db.Roles.Add(new IdentityRole<Guid>("Creator") { Id = roleId });
db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = creatorId, RoleId = roleId });
var packageId = Guid.NewGuid();
var profile = new CreatorProfile { UserId = creatorId, DisplayName = "Test creator", IsAcceptingOrders = true, RateCardJson = JsonSerializer.Serialize(new[] { new RateCardPackageDto(packageId, "Test package", null, 300m, Enumerable.Range(1, 3).Select(i => new RateCardMilestoneDto(i, $"Stage {i}", 100m)).ToList()) }) };
db.CreatorProfiles.Add(profile);
db.Wallets.Add(new Wallet { UserId = clientId, Balance = 10000m });
await db.SaveChangesAsync();
var service = new CommissionService(db, new WalletService(db), new TestWatermark(), new TestStorage(), new VoucherCheckService(db));
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
    catch (InvalidOperationException) { checks++; return; }
    catch (ArgumentException) { checks++; return; }
    throw new Exception($"{name}: invalid transition was accepted");
}

async Task<(Guid Id, Guid[] Milestones)> Create(string title)
{
    var created = await service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = profile.Id,
        Title = title,
        PackageId = packageId
    }, clientId);
    var detail = await service.GetCommissionByIdAsync(created.Id, clientId);
    return (created.Id, detail!.Milestones.OrderBy(m => m.Sequence).Select(m => m.Id).ToArray());
}

var normal = await Create("normal");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "deposit before accept");
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId), "submit before accept");
await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId), "submit before deposit");
await service.DepositEscrowAsync(normal.Id, clientId, "Wallet");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "duplicate deposit");
await Rejected(() => service.ApproveMilestoneAsync(normal.Id, normal.Milestones[0], clientId), "approve before submit");
await Rejected(() => service.RequestMilestoneRevisionAsync(normal.Id, normal.Milestones[0],
    new RequestRevisionRequest { FeedbackComment = "early" }, clientId), "revision before submit");
await Rejected(() => service.DeliverFinalWorkAsync(normal.Id, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", "final.png", creatorId), "deliver early");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "complete early");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId), "review early");

for (var i = 0; i < normal.Milestones.Length; i++)
{
    var milestoneId = normal.Milestones[i];
    await service.SubmitMilestoneWipAsync(normal.Id, milestoneId,
        null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId);
    if (i == 0)
    {
        await service.RequestMilestoneRevisionAsync(normal.Id, milestoneId,
            new RequestRevisionRequest { FeedbackComment = "revise" }, clientId);
        await service.SubmitMilestoneWipAsync(normal.Id, milestoneId,
            null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId);
    }
    await service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId);
    await Rejected(() => service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId), "duplicate approval");
    var state = await service.GetCommissionByIdAsync(normal.Id, clientId);
    Equal((i + 1) * 100m, state!.DisbursedAmount, "disbursement stays exact");
}
var approved = await service.GetCommissionByIdAsync(normal.Id, clientId);
Equal(0m, approved!.EscrowHeldAmount, "escrow exhausted");
Equal(EscrowStatus.Released.ToString(), approved.EscrowStatus, "escrow released");
await service.DeliverFinalWorkAsync(normal.Id, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", "final.png", creatorId);
await service.CompleteCommissionAsync(normal.Id, clientId);
await service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId);
await service.ReplyReviewAsync(normal.Id, "Thanks", creatorId);
await Rejected(() => service.ReplyReviewAsync(normal.Id, "Again", creatorId), "duplicate reply");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "duplicate complete");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 4 }, clientId), "duplicate review");
await Rejected(() => service.CancelCommissionAsync(normal.Id, new CancelWithPolicyRequest { CancellationReason = "late" }, clientId), "cancel completed");
Equal(CommissionStatus.Completed.ToString(), (await service.GetCommissionByIdAsync(normal.Id, clientId))!.Status,
    "completed state unchanged");

var rejected = await Create("rejected");
await service.RespondCommissionAsync(rejected.Id, new RespondCommissionRequest { Action = "Reject" }, creatorId);
await Rejected(() => service.RespondCommissionAsync(rejected.Id,
    new RespondCommissionRequest { Action = "Accept" }, creatorId), "accept after reject");

var negotiated = await Create("negotiated");
await service.RespondCommissionAsync(negotiated.Id,
    new RespondCommissionRequest { Action = "Negotiate", NegotiatePrice = 450 }, creatorId);
var repriced = await service.GetCommissionByIdAsync(negotiated.Id, clientId);
Equal(450m, repriced!.Milestones.Sum(m => m.Price), "milestones match negotiated total");
await Rejected(() => service.RespondCommissionAsync(negotiated.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId), "creator cannot accept their own counter-offer");
await service.RespondToCounterofferAsync(negotiated.Id, true, clientId);
await service.DepositEscrowAsync(negotiated.Id, clientId, "Wallet");
Equal(450m, (await service.GetCommissionByIdAsync(negotiated.Id, clientId))!.EscrowHeldAmount,
    "negotiated deposit matches total");

var disputed = await Create("disputed");
await service.CreateDisputeAsync(disputed.Id, new CreateDisputeRequest { Reason = "test" }, clientId);
await Rejected(() => service.CreateDisputeAsync(disputed.Id,
    new CreateDisputeRequest { Reason = "again" }, creatorId), "duplicate dispute");
await Rejected(() => service.CompleteCommissionAsync(disputed.Id, clientId), "complete disputed");

var cancelled = await Create("cancelled");
await service.CancelCommissionAsync(cancelled.Id, new CancelWithPolicyRequest { CancellationReason = "changed mind" }, clientId);
var cancelledState = await service.GetCommissionByIdAsync(cancelled.Id, clientId);
Equal(EscrowStatus.Pending.ToString(), cancelledState!.EscrowStatus, "no false refund");
await Rejected(() => service.DepositEscrowAsync(cancelled.Id, clientId, "Wallet"), "deposit cancelled");

await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest
{
    CreatorId = profile.Id,
    Title = "bad total",
    PackageId = Guid.NewGuid()
}, clientId), "unknown package cannot create fallback commission");

var validRates = profile.RateCardJson;
var countBeforeInvalid = await db.Commissions.CountAsync();
foreach (var invalidRates in new[] {
    "{invalid-json",
    "[]",
    JsonSerializer.Serialize(new[] { new RateCardPackageDto(packageId, "Mismatch", null, 300m, [new(1, "Work", 200m)]) }),
    JsonSerializer.Serialize(new[] { new RateCardPackageDto(packageId, "Bad sequence", null, 300m, [new(2, "Work", 300m)]) }),
    JsonSerializer.Serialize(new[] { new RateCardPackageDto(packageId, "Negative", null, -1m, [new(1, "Work", -1m)]) })
})
{
    profile.RateCardJson = invalidRates;
    await db.SaveChangesAsync();
    await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest { CreatorId = profile.Id, PackageId = packageId, Title = "invalid" }, clientId), "invalid rates never create a default-priced order");
}
Equal(countBeforeInvalid, await db.Commissions.CountAsync(), "invalid requests persist no commissions");
profile.RateCardJson = validRates;
await db.SaveChangesAsync();

var raceOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("approval-race-" + Guid.NewGuid()).Options;
var raceId = Guid.NewGuid();
await using (var seed = new AppDbContext(raceOptions))
{
    seed.Commissions.Add(new Commission
    {
        Id = raceId, ClientId = clientId, CreatorId = profile.Id, Title = "race",
        Status = CommissionStatus.InProgress, EscrowStatus = EscrowStatus.Deposited,
        TotalPrice = 100, FinalPrice = 100, EscrowHeldAmount = 100,
        Milestones = [new Milestone { Sequence = 1, Title = "Stage", Price = 100, Status = MilestoneStatus.Submitted }]
    });
    await seed.SaveChangesAsync();
}
await using (var first = new AppDbContext(raceOptions))
await using (var second = new AppDbContext(raceOptions))
{
    var one = await first.Commissions.Include(c => c.Milestones).SingleAsync(c => c.Id == raceId);
    var two = await second.Commissions.Include(c => c.Milestones).SingleAsync(c => c.Id == raceId);
    one.Milestones.Single().Status = MilestoneStatus.Approved;
    one.DisbursedAmount = 100;
    one.EscrowHeldAmount = 0;
    one.EscrowStatus = EscrowStatus.Released;
    await first.SaveChangesAsync();
    two.Milestones.Single().Status = MilestoneStatus.Approved;
    two.DisbursedAmount = 100;
    two.EscrowHeldAmount = 0;
    two.EscrowStatus = EscrowStatus.Released;
    try
    {
        await second.SaveChangesAsync();
        throw new Exception("stale approval was saved");
    }
    catch (DbUpdateConcurrencyException) { checks++; }
}

Console.WriteLine($"Passed {checks} commission workflow checks.");

sealed class TestWatermark : IWatermarkService
{
    public Task<Stream> ApplyWatermarkAsync(Stream stream, string text, CancellationToken ct = default) => Task.FromResult<Stream>(new MemoryStream(new byte[] { 4, 5, 6 }));
}
sealed class TestStorage : IStorageService
{
    public Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default) => Task.FromResult(key);
    public Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default) => Task.FromResult(key);
    public string GeneratePresignedDownloadUrl(string key, TimeSpan expiry) => "https://example.test/" + key;
}
