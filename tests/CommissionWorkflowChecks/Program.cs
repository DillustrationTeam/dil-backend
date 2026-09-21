using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var clientId = Guid.NewGuid();
var creatorId = Guid.NewGuid();
var creatorProfileId = Guid.NewGuid();
var packageId = Guid.NewGuid();
var strangerId = Guid.NewGuid();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("commission-" + Guid.NewGuid()).Options);
db.CreatorProfiles.Add(new CreatorProfile { Id = creatorProfileId, UserId = creatorId, DisplayName = "Test creator",
    RateCardJson = CreatorRateCard.Write([new RateCardPackageDto { Id = packageId, Name = "Illustration", Price = 300,
        Milestones = Enumerable.Range(1, 3).Select(i => new RateCardMilestoneDto { Sequence = i, Title = $"Stage {i}", Price = 100 }).ToList() }]) });
db.Wallets.Add(new Wallet { UserId = clientId, Balance = 1000 });
await db.SaveChangesAsync();
var walletService = new WalletService(db);
var service = new CommissionService(db, walletService, new FakeCommissionFileStorage());
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
        CreatorId = creatorProfileId,
        PackageId = packageId,
        Title = title,
    }, clientId);
    Equal(300m, created.TotalPrice, "price comes from creator rate card");
    var detail = await service.GetCommissionByIdAsync(created.Id, clientId);
    Equal(300m, detail!.Milestones.Sum(m => m.Price), "milestones come from creator rate card");
    return (created.Id, detail!.Milestones.OrderBy(m => m.Sequence).Select(m => m.Id).ToArray());
}

try
{
    CreatorRateCard.Write([new RateCardPackageDto { Id = Guid.NewGuid(), Name = "Invalid", Price = 100,
        Milestones = [new RateCardMilestoneDto { Sequence = 2, Title = "Skipped", Price = 100 }] }]);
    throw new Exception("invalid creator milestone plan was accepted");
}
catch (ArgumentException) { checks++; }

var normal = await Create("normal");
try
{
    await service.CreateCommissionAsync(new CreateCommissionRequest { CreatorId = creatorProfileId, PackageId = Guid.NewGuid(), Title = "unlisted package" }, clientId);
    throw new Exception("unlisted package was accepted");
}
catch (ArgumentException) { checks++; }
Equal(null, await service.GetCommissionByIdAsync(normal.Id, strangerId), "stranger cannot read commission");
Equal(0, (await service.GetCommissionsAsync(null, null, strangerId)).TotalItems, "stranger list is empty");
try
{
    await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, strangerId);
    throw new Exception("stranger changed commission");
}
catch (KeyNotFoundException) { checks++; }
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "deposit before accept");
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    new SubmitMilestoneRequest { WipFileUrl = "https://example.test/wip" }, creatorId), "submit before accept");
await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    new SubmitMilestoneRequest { WipFileUrl = "https://example.test/wip" }, creatorId), "submit before deposit");
await service.DepositEscrowAsync(normal.Id, clientId, "Wallet");
Equal(700m, (await walletService.FindWalletAsync(clientId))!.Balance, "deposit debits client");
Equal(300m, (await walletService.FindWalletAsync(clientId))!.LockedBalance, "deposit locks funds");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "duplicate deposit");
await Rejected(() => service.ApproveMilestoneAsync(normal.Id, normal.Milestones[0], clientId), "approve before submit");
await Rejected(() => service.RequestMilestoneRevisionAsync(normal.Id, normal.Milestones[0],
    new RequestRevisionRequest { FeedbackComment = "early" }, clientId), "revision before submit");
await Rejected(() => service.DeliverFinalWorkAsync(normal.Id, "https://example.test/final", creatorId), "deliver early");
await Rejected(() => service.GetFinalDownloadUrlAsync(normal.Id, clientId), "download before delivery");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "complete early");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId), "review early");

for (var i = 0; i < normal.Milestones.Length; i++)
{
    var milestoneId = normal.Milestones[i];
    await service.SubmitMilestoneWipAsync(normal.Id, milestoneId,
        new SubmitMilestoneRequest { WipFileUrl = $"https://example.test/wip-{i}" }, creatorId);
    if (i == 0)
    {
        await service.RequestMilestoneRevisionAsync(normal.Id, milestoneId,
            new RequestRevisionRequest { FeedbackComment = "revise" }, clientId);
        Equal("revise", (await service.GetCommissionByIdAsync(normal.Id, creatorId))!.Milestones.Single(m => m.Id == milestoneId).RevisionFeedback,
            "creator sees revision feedback");
        await service.SubmitMilestoneWipAsync(normal.Id, milestoneId,
            new SubmitMilestoneRequest { WipFileUrl = "https://example.test/revised" }, creatorId);
    }
    await service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId);
    await Rejected(() => service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId), "duplicate approval");
    var state = await service.GetCommissionByIdAsync(normal.Id, clientId);
    Equal((i + 1) * 100m, state!.DisbursedAmount, "disbursement stays exact");
}
Equal(300m, (await walletService.FindWalletAsync(creatorId))!.Balance, "approved milestones credit creator");
var approved = await service.GetCommissionByIdAsync(normal.Id, clientId);
Equal(0m, approved!.EscrowHeldAmount, "escrow exhausted");
Equal(EscrowStatus.Released.ToString(), approved.EscrowStatus, "escrow released");
await service.DeliverFinalWorkAsync(normal.Id, "https://example.test/final", creatorId);
Equal(null, (await service.GetCommissionByIdAsync(normal.Id, clientId))!.Milestones.OrderByDescending(m => m.Sequence).First().FinalDeliverableUrl, "final storage key hidden in detail");
await Rejected(() => service.GetFinalDownloadUrlAsync(normal.Id, clientId), "download before acceptance");
await service.CompleteCommissionAsync(normal.Id, clientId);
Equal(true, (await service.GetFinalDownloadUrlAsync(normal.Id, clientId)).Contains("/final/"), "client can download after completion");
Equal(true, (await service.GetFinalDownloadUrlAsync(normal.Id, creatorId)).Contains("/final/"), "creator can download after completion");
try { await service.GetFinalDownloadUrlAsync(normal.Id, strangerId); throw new Exception("stranger downloaded final"); }
catch (KeyNotFoundException) { checks++; }
await service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId);
await service.ReplyReviewAsync(normal.Id, "Thanks", creatorId);
await Rejected(() => service.ReplyReviewAsync(normal.Id, "Again", creatorId), "duplicate reply");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "duplicate complete");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 4 }, clientId), "duplicate review");
await Rejected(() => service.CancelCommissionAsync(normal.Id, "late", clientId), "cancel completed");
Equal(CommissionStatus.Completed.ToString(), (await service.GetCommissionByIdAsync(normal.Id, clientId))!.Status,
    "completed state unchanged");

var legacy = await Create("legacy WIP");
await service.RespondCommissionAsync(legacy.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await walletService.CreditAsync((await walletService.FindWalletAsync(clientId))!, WalletTransactionType.Deposit,
    300m, "Test", null, "Legacy WIP test funding");
await service.DepositEscrowAsync(legacy.Id, clientId, "Wallet");
var oldWip = await db.Milestones.FindAsync(legacy.Milestones[0]);
oldWip!.Status = MilestoneStatus.Submitted;
oldWip.WipPreviewUrl = "https://example.test/old-wip";
await db.SaveChangesAsync();
await Rejected(() => service.ApproveMilestoneAsync(legacy.Id, legacy.Milestones[0], clientId), "legacy WIP cannot be approved without private copy");
await service.SubmitMilestoneWipAsync(legacy.Id, legacy.Milestones[0],
    new SubmitMilestoneRequest { WipFileUrl = "https://example.test/new-wip" }, creatorId);
await service.ApproveMilestoneAsync(legacy.Id, legacy.Milestones[0], clientId);
Equal(MilestoneStatus.Approved.ToString(), (await service.GetCommissionByIdAsync(legacy.Id, clientId))!.Milestones.First(m => m.Id == legacy.Milestones[0]).Status,
    "legacy WIP can be replaced then approved");
for (var i = 1; i < legacy.Milestones.Length; i++)
{
    await service.SubmitMilestoneWipAsync(legacy.Id, legacy.Milestones[i],
        new SubmitMilestoneRequest { WipFileUrl = $"https://example.test/legacy-{i}" }, creatorId);
    await service.ApproveMilestoneAsync(legacy.Id, legacy.Milestones[i], clientId);
}
var oldFinalCommission = await db.Commissions.FindAsync(legacy.Id);
var oldFinalMilestone = await db.Milestones.FindAsync(legacy.Milestones[^1]);
oldFinalCommission!.Status = CommissionStatus.SubmittedFinal;
oldFinalMilestone!.FinalDeliverableUrl = "https://example.test/old-final";
await db.SaveChangesAsync();
await Rejected(() => service.CompleteCommissionAsync(legacy.Id, clientId), "legacy final cannot be accepted before private copy");
await service.DeliverFinalWorkAsync(legacy.Id, "https://example.test/new-final", creatorId);
await service.CompleteCommissionAsync(legacy.Id, clientId);
Equal(CommissionStatus.Completed.ToString(), (await service.GetCommissionByIdAsync(legacy.Id, clientId))!.Status,
    "legacy final can be replaced then accepted");

var rejected = await Create("rejected");
await service.RespondCommissionAsync(rejected.Id, new RespondCommissionRequest { Action = "Reject" }, creatorId);
await Rejected(() => service.RespondCommissionAsync(rejected.Id,
    new RespondCommissionRequest { Action = "Accept" }, creatorId), "accept after reject");

var negotiated = await Create("negotiated");
try
{
    await service.RespondCommissionAsync(negotiated.Id, new RespondCommissionRequest { Action = "Negotiate" }, creatorId);
    throw new Exception("empty counteroffer was accepted");
}
catch (ArgumentException) { checks++; }
await service.RespondCommissionAsync(negotiated.Id,
    new RespondCommissionRequest { Action = "Negotiate", NegotiatePrice = 450 }, creatorId);
var repriced = await service.GetCommissionByIdAsync(negotiated.Id, clientId);
Equal(450m, repriced!.Milestones.Sum(m => m.Price), "milestones match negotiated total");
await Rejected(() => service.RespondCommissionAsync(negotiated.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId), "creator cannot accept own counteroffer");
await Rejected(() => service.DepositEscrowAsync(negotiated.Id, clientId, "Wallet"), "deposit before counteroffer approval");
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
await service.CancelCommissionAsync(cancelled.Id, "changed mind", clientId);
var cancelledState = await service.GetCommissionByIdAsync(cancelled.Id, clientId);
Equal(EscrowStatus.Pending.ToString(), cancelledState!.EscrowStatus, "no false refund");
await Rejected(() => service.DepositEscrowAsync(cancelled.Id, clientId, "Wallet"), "deposit cancelled");

var fundedCancel = await Create("funded cancellation");
await service.RespondCommissionAsync(fundedCancel.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await walletService.CreditAsync((await walletService.FindWalletAsync(clientId))!, WalletTransactionType.Deposit,
    300m, "Test", null, "Test funding");
var balanceBefore = (await walletService.FindWalletAsync(clientId))!.Balance;
var lockedBefore = (await walletService.FindWalletAsync(clientId))!.LockedBalance;
await service.DepositEscrowAsync(fundedCancel.Id, clientId, "Wallet");
await service.CancelCommissionAsync(fundedCancel.Id, "changed mind", clientId);
Equal(balanceBefore, (await walletService.FindWalletAsync(clientId))!.Balance, "funded cancellation refunds client");
Equal(lockedBefore, (await walletService.FindWalletAsync(clientId))!.LockedBalance, "funded cancellation releases hold");

await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest
{
    CreatorId = creatorProfileId,
    Title = "no package"
}, clientId), "missing creator package");

var raceOptions = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("approval-race-" + Guid.NewGuid()).Options;
var raceId = Guid.NewGuid();
await using (var seed = new AppDbContext(raceOptions))
{
    seed.Commissions.Add(new Commission
    {
        Id = raceId, ClientId = clientId, CreatorId = creatorProfileId, Title = "race",
        Status = CommissionStatus.InProgress, EscrowStatus = EscrowStatus.Deposited,
        TotalPrice = 100, FinalPrice = 100, EscrowHeldAmount = 100,
        Milestones = [new Milestone { Sequence = 1, Title = "Stage", Price = 100, Status = MilestoneStatus.Submitted }]
    });
    await seed.SaveChangesAsync();
}
await using (var first = new AppDbContext(raceOptions))
await using (var second = new AppDbContext(raceOptions))
{
    var one = await first.Commissions.SingleAsync(c => c.Id == raceId);
    var two = await second.Commissions.SingleAsync(c => c.Id == raceId);
    one.UpdatedAt = DateTimeOffset.UtcNow;
    await first.SaveChangesAsync();
    two.UpdatedAt = DateTimeOffset.UtcNow.AddSeconds(1);
    try
    {
        await second.SaveChangesAsync();
        throw new Exception("stale approval was saved");
    }
    catch (DbUpdateConcurrencyException) { checks++; }
}

Console.WriteLine($"Passed {checks} commission workflow checks.");

sealed class FakeCommissionFileStorage : ICommissionFileStorage
{
    public Task<string> SaveAsync(Guid commissionId, string kind, Stream? file, string? fileName, string? sourceUrl, CancellationToken ct) =>
        Task.FromResult($"commissions/{commissionId:N}/{kind}/{Guid.NewGuid():N}");

    public string GetReadUrl(string key, bool download) => $"https://example.test/{key}";
}
