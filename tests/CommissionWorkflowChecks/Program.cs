using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Domain.Entities.Payment;
using System.Text.Json;
using CommissionService = ArtCommission.Infrastructure.Services.CommissionService;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ArtCommission.Infrastructure.ExternalServices.R2;

if (args.Contains("--sql-addons")) { await SqlAddonChecks.RunAsync(); return; }

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
var notifications = new TestNotificationPublisher();
var service = new CommissionService(db, new WalletService(db), new TestWatermark(), new TestStorage(), new VoucherCheckService(db), notifications);
var checks = 0;

void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new Exception($"{name}: expected {expected}, got {actual}");
    checks++;
}

var signedUrlNow = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);
var signedUrlExpiry = signedUrlNow.AddMinutes(15).ToUnixTimeSeconds();
var signedUrlSignature = LocalPresignedUrlSigner.Sign("commissions/demo/final.zip", signedUrlExpiry, "test-signing-key");
Equal(true, LocalPresignedUrlSigner.IsValid("commissions/demo/final.zip", signedUrlExpiry.ToString(), signedUrlSignature, "test-signing-key", signedUrlNow), "local signed URL is valid before expiry");
Equal(false, LocalPresignedUrlSigner.IsValid("commissions/demo/final.zip", signedUrlExpiry.ToString(), signedUrlSignature, "test-signing-key", signedUrlNow.AddMinutes(16)), "local signed URL expires");
Equal(false, LocalPresignedUrlSigner.IsValid("commissions/demo/other.zip", signedUrlExpiry.ToString(), signedUrlSignature, "test-signing-key", signedUrlNow), "local signed URL rejects another key");

async Task Rejected(Func<Task> action, string name)
{
    try { await action(); }
    catch (InvalidOperationException) { checks++; return; }
    catch (ArgumentException) { checks++; return; }
    throw new Exception($"{name}: invalid transition was accepted");
}

async Task Denied(Func<Task> action, string name)
{
    try { await action(); }
    catch (UnauthorizedAccessException) { checks++; return; }
    throw new Exception($"{name}: unauthorized action was accepted");
}

async Task<(Guid Id, Guid[] Milestones)> Create(string title, bool acceptTerms = true)
{
    var created = await service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = profile.Id,
        Title = title,
        PackageId = packageId
    }, clientId);
    if (acceptTerms)
    {
        await service.AcceptWorkroomTermsAsync(created.Id, clientId);
        await service.AcceptWorkroomTermsAsync(created.Id, creatorId);
    }
    var detail = await service.GetCommissionByIdAsync(created.Id, clientId);
    return (created.Id, detail!.Milestones.OrderBy(m => m.Sequence).Select(m => m.Id).ToArray());
}

var normal = await Create("normal", acceptTerms: false);
var normalDetail = await service.GetCommissionByIdAsync(normal.Id, clientId);
Equal("Test creator", normalDetail!.CreatorName, "commission detail includes creator display name");
var (clientCommissions, _) = await service.GetCommissionsAsync(null, "Client", clientId);
Equal("Test creator", clientCommissions.Single(x => x.Id == normal.Id).CreatorName, "commission list includes creator display name");
Equal((creatorId, NotificationType.CommissionStatusChanged),
    (notifications.Items[^1].UserId, notifications.Items[^1].Type), "new request notifies creator");
Equal("CreatorCommissionRequests", notifications.Items[^1].RefType, "new request opens creator request queue");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "deposit before accept");
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId), "submit before accept");
await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
Equal((clientId, NotificationType.CommissionStatusChanged),
    (notifications.Items[^1].UserId, notifications.Items[^1].Type), "creator response notifies client");
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0],
    null, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", creatorId), "submit before deposit");
var clientTerms = await service.AcceptWorkroomTermsAsync(normal.Id, clientId);
Equal("1.0", clientTerms.ClientTermsVersion, "client terms acceptance persisted");
await service.DepositEscrowAsync(normal.Id, clientId, "Wallet");
Equal((creatorId, NotificationType.EscrowStatusChanged),
    (notifications.Items[^1].UserId, notifications.Items[^1].Type), "escrow deposit notifies creator");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "duplicate deposit");
await Rejected(() => service.ApproveMilestoneAsync(normal.Id, normal.Milestones[0], clientId), "approve before submit");
await Rejected(() => service.RequestMilestoneRevisionAsync(normal.Id, normal.Milestones[0],
    new RequestRevisionRequest { FeedbackComment = "early" }, clientId), "revision before submit");
await Rejected(() => service.DeliverFinalWorkAsync(normal.Id, new MemoryStream(new byte[] { 1, 2, 3 }), "image/png", "final.png", creatorId), "deliver early");
await Rejected(() => service.GetFinalDownloadUrlAsync(normal.Id, clientId), "download before delivery");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "complete early");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId), "review early");
var creatorTerms = await service.AcceptWorkroomTermsAsync(normal.Id, creatorId);
Equal("1.0", creatorTerms.CreatorTermsVersion, "creator terms acceptance persisted");

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
var deliveryDownload = await service.GetFinalDownloadUrlAsync(normal.Id, clientId);
Equal(true, deliveryDownload.DownloadUrl.StartsWith("https://example.test/"), "client can inspect final delivery before completion");
try
{
    await service.GetFinalDownloadUrlAsync(normal.Id, Guid.NewGuid());
    throw new Exception("unrelated user can download final delivery");
}
catch (KeyNotFoundException) { checks++; }
await service.CompleteCommissionAsync(normal.Id, clientId);
Equal(deliveryDownload.DownloadUrl, (await service.GetFinalDownloadUrlAsync(normal.Id, clientId)).DownloadUrl, "delivery remains available after completion");
await service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId);
await service.ReplyReviewAsync(normal.Id, "Thanks", creatorId);
var publicReviews = await new ArtCommission.Application.ArtistStudio.Marketplace.MarketplaceQueryHandler(db)
    .Handle(new ArtCommission.Application.ArtistStudio.Marketplace.GetCreatorReviewsQuery(profile.Id), default);
Equal("Thanks", publicReviews.Single().ReviewerReply, "public creator profile includes saved reply");
await Rejected(() => service.ReplyReviewAsync(normal.Id, "Again", creatorId), "duplicate reply");
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "duplicate complete");
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 4 }, clientId), "duplicate review");
await Rejected(() => service.CancelCommissionAsync(normal.Id, new CancelWithPolicyRequest { CancellationReason = "late" }, clientId), "cancel completed");
Equal(CommissionStatus.Completed.ToString(), (await service.GetCommissionByIdAsync(normal.Id, clientId))!.Status,
    "completed state unchanged");
Equal(normal.Id, (await service.GetCommissionByIdAsync(normal.Id, creatorId))!.Id,
    "AAMCS-041 UTCID06 creator can read own commission detail");
Equal(true, (await service.GetMilestoneWipPreviewUrlAsync(normal.Id, normal.Milestones[0], clientId)).Contains("commissions/"),
    "AAMCS-047 UTCID06 participant can obtain protected WIP URL");
Equal(true, (await service.GetFinalDownloadUrlAsync(normal.Id, creatorId)).DownloadUrl.Contains("commissions/"),
    "AAMCS-051 UTCID06 creator can obtain protected final URL");

var rejected = await Create("rejected");
await service.RespondCommissionAsync(rejected.Id, new RespondCommissionRequest { Action = "Reject", RejectReason = "Không phù hợp" }, creatorId);
await Rejected(() => service.RespondCommissionAsync(rejected.Id,
    new RespondCommissionRequest { Action = "Accept" }, creatorId), "accept after reject");

var invalidReject = await Create("invalid reject");
await Rejected(() => service.RespondCommissionAsync(invalidReject.Id,
    new RespondCommissionRequest { Action = "Reject", RejectReason = " " }, creatorId),
    "AAMCS-043 UTCID02 blank reject reason");
await Denied(() => service.RespondCommissionAsync(invalidReject.Id,
    new RespondCommissionRequest { Action = "Reject", RejectReason = "client cannot reject" }, clientId),
    "AAMCS-043 UTCID03 client cannot reject creator request");

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

var invalidNegotiation = await Create("invalid negotiation");
await Rejected(() => service.RespondCommissionAsync(invalidNegotiation.Id,
    new RespondCommissionRequest { Action = "Negotiate", NegotiatePrice = 0 }, creatorId),
    "AAMCS-044 UTCID03 zero negotiation price");

var disputed = await Create("disputed");
await service.CreateDisputeAsync(disputed.Id, new CreateDisputeRequest { Reason = "test" }, clientId);
Equal((creatorId, NotificationType.DisputeStatusChanged),
    (notifications.Items[^1].UserId, notifications.Items[^1].Type), "new dispute notifies other party");
await Rejected(() => service.CreateDisputeAsync(disputed.Id,
    new CreateDisputeRequest { Reason = "again" }, creatorId), "duplicate dispute");
await Rejected(() => service.CompleteCommissionAsync(disputed.Id, clientId), "complete disputed");
Equal(disputed.Id, (await service.GetDisputeByCommissionIdAsync(disputed.Id, clientId))!.CommissionId,
    "AAMCS-058 UTCID06 participant can read dispute detail");

var cancelled = await Create("cancelled");
await service.CancelCommissionAsync(cancelled.Id, new CancelWithPolicyRequest { CancellationReason = "changed mind", Details = "budget changed" }, clientId);
var cancelledDetail = await service.GetCommissionByIdAsync(cancelled.Id, clientId);
Equal("changed mind", cancelledDetail!.CancellationReason, "cancellation reason persisted");
Equal("budget changed", cancelledDetail.CancellationDetails, "cancellation details persisted");
var cancelledState = await service.GetCommissionByIdAsync(cancelled.Id, clientId);
Equal(EscrowStatus.Pending.ToString(), cancelledState!.EscrowStatus, "no false refund");
await Rejected(() => service.DepositEscrowAsync(cancelled.Id, clientId, "Wallet"), "deposit cancelled");

var mutualCancellation = await Create("mutual cancellation");
await service.RespondCommissionAsync(mutualCancellation.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.DepositEscrowAsync(mutualCancellation.Id, clientId, "Wallet");
var requestedByCreator = await service.CancelCommissionAsync(mutualCancellation.Id,
    new CancelWithPolicyRequest { CancellationReason = "Creator unavailable" }, creatorId);
Equal("Pending", requestedByCreator.CancellationRequestStatus, "started work creates cancellation request");
await Denied(() => service.RespondCancellationRequestAsync(mutualCancellation.Id, true, creatorId),
    "requester cannot approve own cancellation request");
var mutuallyCancelled = await service.RespondCancellationRequestAsync(mutualCancellation.Id, true, clientId);
Equal(CommissionStatus.Cancelled.ToString(), mutuallyCancelled.Status, "counterparty approval cancels commission");
Equal(0m, mutuallyCancelled.EscrowHeldAmount, "mutual cancellation refunds remaining escrow");

var clientTimeout = await Create("client cancellation timeout");
await service.RespondCommissionAsync(clientTimeout.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.DepositEscrowAsync(clientTimeout.Id, clientId, "Wallet");
await service.CancelCommissionAsync(clientTimeout.Id,
    new CancelWithPolicyRequest { CancellationReason = "Changed requirements" }, clientId);
var clientTimeoutEntity = await db.Commissions.FindAsync(clientTimeout.Id);
clientTimeoutEntity!.CancellationRequestExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
await db.SaveChangesAsync();
await service.ProcessCancellationDeadlinesAsync();
var expiredClientRequest = await service.GetCommissionByIdAsync(clientTimeout.Id, clientId);
Equal("Expired", expiredClientRequest!.CancellationRequestStatus, "silent creator expires client request for dispute");
Equal(CommissionStatus.InProgress.ToString(), expiredClientRequest.Status, "client timeout does not auto cancel active work");

var creatorTimeout = await Create("creator cancellation timeout");
await service.RespondCommissionAsync(creatorTimeout.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.DepositEscrowAsync(creatorTimeout.Id, clientId, "Wallet");
await service.CancelCommissionAsync(creatorTimeout.Id,
    new CancelWithPolicyRequest { CancellationReason = "Cannot continue" }, creatorId);
var creatorTimeoutEntity = await db.Commissions.FindAsync(creatorTimeout.Id);
creatorTimeoutEntity!.CancellationRequestExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);
await db.SaveChangesAsync();
await service.ProcessCancellationDeadlinesAsync();
var autoCancelledCreatorRequest = await service.GetCommissionByIdAsync(creatorTimeout.Id, clientId);
Equal(CommissionStatus.Cancelled.ToString(), autoCancelledCreatorRequest!.Status, "silent client auto accepts creator request");
Equal(0m, autoCancelledCreatorRequest.EscrowHeldAmount, "creator timeout refunds remaining escrow");

var overdue = await Create("overdue cancellation");
await service.RespondCommissionAsync(overdue.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.DepositEscrowAsync(overdue.Id, clientId, "Wallet");
var overdueEntity = await db.Commissions.FindAsync(overdue.Id);
overdueEntity!.DeadlineAt = DateTimeOffset.UtcNow.AddHours(-25);
await db.SaveChangesAsync();
var overdueCancellation = await service.CancelCommissionAsync(overdue.Id,
    new CancelWithPolicyRequest { CancellationReason = "Creator ghosting after deadline" }, clientId);
Equal(CommissionStatus.Cancelled.ToString(), overdueCancellation.Status, "overdue grace expiry auto cancels");
Equal(0m, overdueCancellation.EscrowHeldAmount, "overdue auto cancellation refunds escrow");

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

// Add-on pricing snapshots, milestone gating, wallet accounting and idempotency.
var addonService = new AddonService(db, new WalletService(db));
var extra = await addonService.SaveAsync(new SaveCreatorAddonRequest {
    PresetKey = "extra-character", Title = "ignored", Price = 150, AllowedMilestone = "Final", ExtendDays = 1
}, creatorId, default);
Equal("Sketch", extra.AllowedMilestone, "preset stage cannot be overridden");
var addonOrder = await service.CreateCommissionAsync(new CreateCommissionRequest {
    CreatorId = profile.Id, PackageId = packageId, Title = "addons", DeadlineAt = DateTimeOffset.UtcNow.AddDays(10)
}, clientId);
await service.RespondCommissionAsync(addonOrder.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.AcceptWorkroomTermsAsync(addonOrder.Id, clientId);
await service.AcceptWorkroomTermsAsync(addonOrder.Id, creatorId);
await service.DepositEscrowAsync(addonOrder.Id, clientId, "Wallet");
var addonCommission = await db.Commissions.Include(x => x.Milestones).SingleAsync(x => x.Id == addonOrder.Id);
var deadlineBeforeAddon = addonCommission.DeadlineAt;
var addonWallet = await db.Wallets.SingleAsync(x => x.UserId == clientId);
var balanceBeforeAddon = addonWallet.Balance;
var lockedBeforeAddon = addonWallet.LockedBalance;
var proposed = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, creatorId, default);
Equal("Proposed", proposed.Status, "proposal awaits client consent");
Equal(balanceBeforeAddon, addonWallet.Balance, "proposing does not charge");
await addonService.RespondAsync(addonOrder.Id, proposed.Id, new("Accept"), clientId, default);
Equal(balanceBeforeAddon - 150, addonWallet.Balance, "addon debits available funds");
Equal(lockedBeforeAddon + 150, addonWallet.LockedBalance, "addon locks funds");
Equal(450m, addonCommission.FinalPrice, "addon increases contract price");
Equal(450m, addonCommission.EscrowHeldAmount, "addon increases escrow");
Equal(250m, addonCommission.Milestones.Single(x => x.Sequence == 1).Price, "addon paid through target milestone");
Equal(deadlineBeforeAddon!.Value.AddDays(1), addonCommission.DeadlineAt!.Value, "addon extends deadline");
await addonService.RespondAsync(addonOrder.Id, proposed.Id, new("Accept"), clientId, default);
Equal(balanceBeforeAddon - 150, addonWallet.Balance, "accept retry does not charge twice");
Equal(1, await db.WalletTransactions.CountAsync(x => x.RefId == proposed.Id), "one ledger entry per addon");
var declined = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { Title = "custom", Price = 25, AllowedMilestone = "Sketch" }, creatorId, default);
await addonService.RespondAsync(addonOrder.Id, declined.Id, new("Reject"), clientId, default);
Equal("Rejected", declined.Status, "reject marks proposal");
Equal(450m, addonCommission.FinalPrice, "reject leaves price unchanged");
Equal(1, addonCommission.CurrentStage, "reject leaves progress unchanged");
Equal(balanceBeforeAddon - 150, addonWallet.Balance, "reject leaves wallet unchanged");
var staleAddon = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, creatorId, default);
var sketch = addonCommission.Milestones.Single(x => x.Sequence == 1);
await service.SubmitMilestoneWipAsync(addonOrder.Id, sketch.Id, null, new MemoryStream(new byte[] { 1 }), "image/png", creatorId);
await service.ApproveMilestoneAsync(addonOrder.Id, sketch.Id, clientId);
Equal(200m, addonCommission.EscrowHeldAmount, "approval releases base and addon funds");
await Rejected(() => addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, creatorId, default), "Sketch addon cannot be proposed at Lineart");
await Rejected(() => addonService.RespondAsync(addonOrder.Id, staleAddon.Id, new("Accept"), clientId, default), "stale Sketch proposal cannot be accepted at Lineart");
var unfundedAddon = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { Title = "unaffordable", Price = 999999, AllowedMilestone = "Lineart" }, creatorId, default);
var priceBeforeFailure = addonCommission.FinalPrice;
await Rejected(() => addonService.RespondAsync(addonOrder.Id, unfundedAddon.Id, new("Accept"), clientId, default), "insufficient wallet rejects addon");
Equal("Proposed", unfundedAddon.Status, "failed payment preserves proposal");
Equal(priceBeforeFailure, addonCommission.FinalPrice, "failed payment preserves contract");
try { await addonService.RespondAsync(addonOrder.Id, unfundedAddon.Id, new("Accept"), creatorId, default); throw new Exception("creator accepted own price"); }
catch (UnauthorizedAccessException) { checks++; }
try { await addonService.ListAsync(addonOrder.Id, Guid.NewGuid(), default); throw new Exception("outsider read addons"); }
catch (UnauthorizedAccessException) { checks++; }
var booked = await service.CreateCommissionAsync(new CreateCommissionRequest {
    CreatorId = profile.Id, PackageId = packageId, Title = "booking extras", AddonIds = [extra.Id], DeadlineAt = DateTimeOffset.UtcNow.AddDays(7)
}, clientId);
Equal(450m, booked.FinalPrice, "booking uses stored addon price");
Equal(1, await db.Set<CommissionAddon>().CountAsync(x => x.CommissionId == booked.Id && x.Status == "Accepted"), "booking snapshots addon");
await Rejected(() => service.CreateCommissionAsync(new CreateCommissionRequest {
    CreatorId = profile.Id, PackageId = packageId, Title = "duplicate extras", AddonIds = [extra.Id, extra.Id]
}, clientId), "duplicate booking extras rejected");
Equal("Lineart", AddonRules.Stage(3, 4), "four-stage package coloring stays Lineart");
Equal("Final", AddonRules.Stage(4, 4), "four-stage final resolves correctly");

var customConfig = await addonService.SaveAsync(new SaveCreatorAddonRequest {
    Title = "Layer separation", Price = 50, AllowedMilestone = "Final", PackageId = packageId
}, creatorId, default);
Equal("Final", customConfig.AllowedMilestone, "custom configuration persists stage");
await Rejected(() => addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = customConfig.Id }, clientId, default), "Final extras unavailable in Lineart");
await Rejected(() => addonService.SaveAsync(new SaveCreatorAddonRequest { Title = "Invalid", Price = -1 }, creatorId, default), "negative custom price rejected");
await Rejected(() => addonService.SaveAsync(new SaveCreatorAddonRequest { Title = "Invalid", Price = 1.5m }, creatorId, default), "fractional VND rejected");
var lineartOption = await addonService.SaveAsync(new SaveCreatorAddonRequest { Title = "Alt expression", Price = 40, AllowedMilestone = "Lineart" }, creatorId, default);
var clientProposal = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = lineartOption.Id, Price = 1, AllowedMilestone = "Final" }, clientId, default);
Equal(40m, clientProposal.Price, "client cannot override configured price");
Equal("Lineart", clientProposal.AllowedMilestone, "client cannot override configured stage");
await addonService.SaveAsync(new SaveCreatorAddonRequest { Id = lineartOption.Id, Title = lineartOption.Title, Price = 70, AllowedMilestone = "Lineart", IsActive = false }, creatorId, default);
await addonService.RespondAsync(addonOrder.Id, clientProposal.Id, new("Accept"), clientId, default);
Equal(490m, addonCommission.FinalPrice, "accepted proposal keeps price snapshot after config edits");
await Rejected(() => addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = lineartOption.Id }, creatorId, default), "disabled addon unavailable");
await Rejected(() => addonService.RespondAsync(addonOrder.Id, declined.Id, new("Accept"), clientId, default), "rejected quote cannot be paid");
var anyProposal = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { Title = "Any extra", Price = 10, AllowedMilestone = "Any" }, creatorId, default);
var lineartMilestone = addonCommission.Milestones.Single(x => x.Sequence == 2);
await service.SubmitMilestoneWipAsync(addonOrder.Id, lineartMilestone.Id, null, new MemoryStream(new byte[] { 1 }), "image/png", creatorId);
await service.ApproveMilestoneAsync(addonOrder.Id, lineartMilestone.Id, clientId);
await Rejected(() => addonService.RespondAsync(addonOrder.Id, anyProposal.Id, new("Accept"), clientId, default), "Any proposal still expires when sequence changes");
await Rejected(() => addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, creatorId, default), "extra character blocked at Final");
var finalProposal = await addonService.ProposeAsync(addonOrder.Id, new ProposeAddonRequest { CreatorAddonId = customConfig.Id }, creatorId, default);
await addonService.RespondAsync(addonOrder.Id, finalProposal.Id, new("Accept"), clientId, default);
Equal(3, finalProposal.MilestoneSequence, "Final source addon targets final milestone");
Equal(150m, addonCommission.EscrowHeldAmount, "Final addon included in remaining escrow");
var finalMilestone = addonCommission.Milestones.Single(x => x.Sequence == 3);
await service.SubmitMilestoneWipAsync(addonOrder.Id, finalMilestone.Id, null, new MemoryStream(new byte[] { 1 }), "image/png", creatorId);
await service.ApproveMilestoneAsync(addonOrder.Id, finalMilestone.Id, clientId);
Equal(0m, addonCommission.EscrowHeldAmount, "all funded extras disbursed at completion of milestones");
Equal(540m, addonCommission.DisbursedAmount, "creator receives all base and extra funds");

db.Vouchers.Add(new Voucher {
    VoucherCode = "ADDON10", DiscountType = DiscountType.Percent, DiscountValue = 10,
    Scope = VoucherScope.Commission, IsActive = true,
    StartDate = VoucherCheckService.TodayInVietnam().AddDays(-1), EndDate = VoucherCheckService.TodayInVietnam().AddDays(1)
});
await db.SaveChangesAsync();
var withVoucher = await service.CreateCommissionAsync(new CreateCommissionRequest {
    CreatorId = profile.Id, PackageId = packageId, Title = "extras with voucher", AddonIds = [extra.Id],
    VoucherCode = "ADDON10", DeadlineAt = DateTimeOffset.UtcNow.AddDays(7)
}, clientId);
Equal(420m, withVoucher.FinalPrice, "voucher discounts base price and leaves quoted extra intact");
await service.RespondCommissionAsync(withVoucher.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
await service.AcceptWorkroomTermsAsync(withVoucher.Id, clientId);
await service.AcceptWorkroomTermsAsync(withVoucher.Id, creatorId);
await service.DepositEscrowAsync(withVoucher.Id, clientId, "Wallet");
var voucherDetail = (await service.GetCommissionByIdAsync(withVoucher.Id, clientId))!;
Equal(420m, voucherDetail.FinalPrice, "deposit revalidation preserves base-only discount");
Equal(420m, voucherDetail.Milestones.Sum(x => x.Price), "funded milestones reconcile to discounted contract plus extras");
Equal(240m, voucherDetail.Milestones.Single(x => x.Sequence == 1).Price, "Sketch retains full extra plus discounted base payment");
foreach (var m in voucherDetail.Milestones.OrderBy(x => x.Sequence)) {
    await service.SubmitMilestoneWipAsync(withVoucher.Id, m.Id, null, new MemoryStream(new byte[] { 1 }), "image/png", creatorId);
    await service.ApproveMilestoneAsync(withVoucher.Id, m.Id, clientId);
}
Equal(0m, (await service.GetCommissionByIdAsync(withVoucher.Id, clientId))!.EscrowHeldAmount, "voucher plus extras releases all escrow safely");

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

sealed class TestNotificationPublisher : INotificationPublisher
{
    public List<(Guid UserId, NotificationType Type, string? RefType, string? DedupKey)> Items { get; } = [];

    public Task<NotificationDto?> PublishAsync(
        Guid userId,
        NotificationType notificationType,
        string title,
        string body,
        string? refType = null,
        Guid? refId = null,
        NotificationChannel channel = NotificationChannel.InApp,
        string? dedupKey = null,
        CancellationToken cancellationToken = default)
    {
        Items.Add((userId, notificationType, refType, dedupKey));
        return Task.FromResult<NotificationDto?>(null);
    }
}
