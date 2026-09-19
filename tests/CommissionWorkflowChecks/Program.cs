using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var clientId = Guid.NewGuid();
var creatorId = Guid.NewGuid();
var roleId = Guid.NewGuid();
await using var identity = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("identity-" + Guid.NewGuid()).Options);
await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase("commission-" + Guid.NewGuid()).Options);
identity.Roles.Add(new IdentityRole<Guid>("Creator") { Id = roleId });
identity.UserRoles.Add(new IdentityUserRole<Guid> { UserId = creatorId, RoleId = roleId });
identity.CreatorProfiles.Add(new CreatorProfile { UserId = creatorId, DisplayName = "Test creator" });
await identity.SaveChangesAsync();
var service = new CommissionService(db, identity);
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
        CreatorId = creatorId,
        Title = title,
        TotalPrice = 300,
        Milestones = Enumerable.Range(1, 3)
            .Select(i => new MilestoneCreateDto { Sequence = i, Title = $"Stage {i}", Price = 100 }).ToList()
    }, clientId);
    var detail = await service.GetCommissionByIdAsync(created.Id, clientId);
    return (created.Id, detail!.Milestones.OrderBy(m => m.Sequence).Select(m => m.Id).ToArray());
}

var normal = await Create("normal");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "deposit before accept");
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    new SubmitMilestoneRequest { WipFileUrl = "https://example.test/wip" }, creatorId), "submit before accept");
await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    new SubmitMilestoneRequest { WipFileUrl = "https://example.test/wip" }, creatorId), "submit before deposit");
await service.DepositEscrowAsync(normal.Id, clientId, "Wallet");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "duplicate deposit");
await Rejected(() => service.ApproveMilestoneAsync(normal.Id, normal.Milestones[0], clientId), "approve before submit");
await Rejected(() => service.RequestMilestoneRevisionAsync(normal.Id, normal.Milestones[0],
    new RequestRevisionRequest { FeedbackComment = "early" }, clientId), "revision before submit");
await Rejected(() => service.DeliverFinalWorkAsync(normal.Id, "https://example.test/final", creatorId), "deliver early");
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
        await service.SubmitMilestoneWipAsync(normal.Id, milestoneId,
            new SubmitMilestoneRequest { WipFileUrl = "https://example.test/revised" }, creatorId);
    }
    await service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId);
    await Rejected(() => service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId), "duplicate approval");
    var state = await service.GetCommissionByIdAsync(normal.Id, clientId);
    Equal((i + 1) * 100m, state!.DisbursedAmount, "disbursement stays exact");
}
var approved = await service.GetCommissionByIdAsync(normal.Id, clientId);
Equal(0m, approved!.EscrowHeldAmount, "escrow exhausted");
Equal(EscrowStatus.Released.ToString(), approved.EscrowStatus, "escrow released");
await service.DeliverFinalWorkAsync(normal.Id, "https://example.test/final", creatorId);
await service.CompleteCommissionAsync(normal.Id, clientId);
await service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId);
await service.ReplyReviewAsync(normal.Id, "Thanks", creatorId);
await Rejected(() => service.ReplyReviewAsync(normal.Id, "Again", creatorId), "duplicate reply");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "duplicate complete");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 4 }, clientId), "duplicate review");
await Rejected(() => service.CancelCommissionAsync(normal.Id, "late", clientId), "cancel completed");
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
await service.RespondCommissionAsync(negotiated.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
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

await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest
{
    CreatorId = creatorId,
    Title = "bad total",
    TotalPrice = 300,
    Milestones = [new MilestoneCreateDto { Sequence = 1, Title = "Work", Price = 200 }]
}, clientId), "mismatched milestone total");

var raceOptions = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseInMemoryDatabase("approval-race-" + Guid.NewGuid()).Options;
var raceId = Guid.NewGuid();
await using (var seed = new ApplicationDbContext(raceOptions))
{
    seed.Commissions.Add(new Commission
    {
        Id = raceId, ClientId = clientId, CreatorId = creatorId, Title = "race",
        Status = CommissionStatus.InProgress, EscrowStatus = EscrowStatus.Deposited,
        TotalPrice = 100, FinalPrice = 100, EscrowHeldAmount = 100,
        Milestones = [new Milestone { Sequence = 1, Title = "Stage", Price = 100, Status = MilestoneStatus.Submitted }]
    });
    await seed.SaveChangesAsync();
}
await using (var first = new ApplicationDbContext(raceOptions))
await using (var second = new ApplicationDbContext(raceOptions))
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
