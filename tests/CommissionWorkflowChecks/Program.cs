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

// --- Security & Business Audit Fixes Verification ---

// Test B05: Chặn số tiền thập phân VND
{
    var paymentValidator = new ArtCommission.Application.Payment.Commands.CreatePaymentOrder.CreatePaymentOrderCommandValidator();
    var invalidPayment = paymentValidator.Validate(new ArtCommission.Application.Payment.Commands.CreatePaymentOrder.CreatePaymentOrderCommand(
        Guid.NewGuid(), 100000.99m, "PayOS"));
    Equal(false, invalidPayment.IsValid, "B05: Payment order validator must reject decimal VND");

    var validPayment = paymentValidator.Validate(new ArtCommission.Application.Payment.Commands.CreatePaymentOrder.CreatePaymentOrderCommand(
        Guid.NewGuid(), 100000m, "PayOS"));
    Equal(true, validPayment.IsValid, "B05: Payment order validator must accept integer VND");

    var payoutValidator = new ArtCommission.Application.Payment.Commands.CreatePayoutRequest.CreatePayoutRequestCommandValidator();
    var invalidPayout = payoutValidator.Validate(new ArtCommission.Application.Payment.Commands.CreatePayoutRequest.CreatePayoutRequestCommand(
        Guid.NewGuid(), 50000.50m, Guid.NewGuid(), null));
    Equal(false, invalidPayout.IsValid, "B05: Payout request validator must reject decimal VND");

    var validPayout = payoutValidator.Validate(new ArtCommission.Application.Payment.Commands.CreatePayoutRequest.CreatePayoutRequestCommand(
        Guid.NewGuid(), 50000m, Guid.NewGuid(), null));
    Equal(true, validPayout.IsValid, "B05: Payout request validator must accept integer VND");
}

// Test S05: Che số tài khoản ngân hàng
{
    Equal("******7890", BankAccountMasker.Mask("1234567890"), "S05: Bank account masker masks standard length");
    Equal("****", BankAccountMasker.Mask("1234"), "S05: Bank account masker masks short length");
    Equal(string.Empty, BankAccountMasker.Mask(null), "S05: Bank account masker handles null");
}

// Test B07: Keyset pagination thông báo không bỏ sót bản ghi trùng timestamp
{
    await using var notifDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("notif-cursor-" + Guid.NewGuid()).Options);
    var notifUser = Guid.NewGuid();
    var fixedTimestamp = DateTimeOffset.UtcNow;
    for (int i = 0; i < 25; i++)
    {
        notifDb.Notifications.Add(new ArtCommission.Domain.Entities.Notifications.Notification
        {
            UserId = notifUser,
            NotificationTitle = $"Notif {i}",
            Body = $"Body {i}",
            NotificationType = NotificationType.PaymentSucceeded,
            CreatedAt = fixedTimestamp
        });
    }
    await notifDb.SaveChangesAsync();

    var notifHandler = new ArtCommission.Application.Notifications.Queries.GetMyNotifications.GetMyNotificationsQueryHandler(notifDb);
    var (p1Ok, p1Data, _, _) = await notifHandler.Handle(
        new ArtCommission.Application.Notifications.Queries.GetMyNotifications.GetMyNotificationsQuery(notifUser, null, null, null, null, 20),
        CancellationToken.None);
    Equal(true, p1Ok, "B07: Page 1 success");
    Equal(20, p1Data!.Items.Count, "B07: Page 1 has 20 items");
    Equal(true, p1Data.NextCursor != null && p1Data.NextCursor.Contains('|'), "B07: Page 1 nextCursor has composite format");

    var (p2Ok, p2Data, _, _) = await notifHandler.Handle(
        new ArtCommission.Application.Notifications.Queries.GetMyNotifications.GetMyNotificationsQuery(notifUser, null, null, null, p1Data.NextCursor, 20),
        CancellationToken.None);
    Equal(true, p2Ok, "B07: Page 2 success");
    Equal(5, p2Data!.Items.Count, "B07: Page 2 has remaining 5 items");

    var allIds = p1Data.Items.Concat(p2Data.Items).Select(x => x.NotificationId).Distinct().Count();
    Equal(25, allIds, "B07: Exactly 25 unique notifications retrieved across pages without skipping");
}

// Test B04: Phân giải CreatorProfile.Id sang UserId trong AI Conversation
{
    await using var aiDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("ai-creator-" + Guid.NewGuid()).Options);
    var clientUser = Guid.NewGuid();
    var creatorUser = Guid.NewGuid();
    var creatorProf = new CreatorProfile { UserId = creatorUser, DisplayName = "AI Test Creator" };
    aiDb.CreatorProfiles.Add(creatorProf);
    var comm = new Commission
    {
        ClientId = clientUser,
        CreatorId = creatorProf.Id, // CreatorId != creatorUser
        Title = "AI Commission Test"
    };
    aiDb.Commissions.Add(comm);
    await aiDb.SaveChangesAsync();

    var aiHandler = new ArtCommission.Application.Ai.Commands.CreateAiConversation.CreateAiConversationCommandHandler(aiDb);
    var (aiOk, aiDto, _) = await aiHandler.Handle(
        new ArtCommission.Application.Ai.Commands.CreateAiConversation.CreateAiConversationCommand(
            creatorUser, "Test Topic", "Commission", comm.Id),
        CancellationToken.None);
    Equal(true, aiOk, "B04: Creator can create AI conversation when CreatorId is CreatorProfile.Id");
    Equal(comm.Id, aiDto!.ContextId, "B04: AI conversation correctly bound to Commission");

    var stranger = Guid.NewGuid();
    var (strangerOk, _, _) = await aiHandler.Handle(
        new ArtCommission.Application.Ai.Commands.CreateAiConversation.CreateAiConversationCommand(
            stranger, "Test Topic", "Commission", comm.Id),
        CancellationToken.None);
    Equal(false, strangerOk, "B04: Stranger rejected from creating AI conversation on commission");
}

// Test S02: Chặn chiếm chỗ voucher trên Commission của người khác
{
    await using var voucherDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("voucher-s02-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);
    var legitClient = Guid.NewGuid();
    var attacker = Guid.NewGuid();
    var commOfLegit = new Commission
    {
        ClientId = legitClient,
        CreatorId = Guid.NewGuid(),
        Title = "Legit Commission",
        TotalPrice = 500000m
    };
    voucherDb.Commissions.Add(commOfLegit);

    var voucher = new Voucher
    {
        VoucherCode = "DISCOUNT10",
        DiscountType = DiscountType.Fixed,
        DiscountValue = 50000m,
        StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
        IsActive = true,
        UsageLimit = 10,
        UsedCount = 0
    };
    voucherDb.Vouchers.Add(voucher);
    await voucherDb.SaveChangesAsync();

    var voucherHandler = new ArtCommission.Application.Payment.Commands.RedeemVoucher.RedeemVoucherCommandHandler(
        voucherDb,
        new VoucherCheckService(voucherDb),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<ArtCommission.Application.Payment.Commands.RedeemVoucher.RedeemVoucherCommandHandler>.Instance);

    var (attOk, _, _, attErrors) = await voucherHandler.Handle(
        new ArtCommission.Application.Payment.Commands.RedeemVoucher.RedeemVoucherCommand(
            attacker, "DISCOUNT10", "Commission", commOfLegit.Id, 500000m),
        CancellationToken.None);

    Equal(false, attOk, "S02: Attacker cannot redeem voucher on another user's commission");
    Equal("Bạn không có quyền áp dụng mã giảm giá cho đơn đặt vẽ này.", attErrors[0], "S02: Returns permission denied error");

    // Chủ đơn dùng hợp lệ lần đầu
    var (legitOk, _, _, _) = await voucherHandler.Handle(
        new ArtCommission.Application.Payment.Commands.RedeemVoucher.RedeemVoucherCommand(
            legitClient, "DISCOUNT10", "Commission", commOfLegit.Id, 500000m),
        CancellationToken.None);
    Equal(true, legitOk, "S02/B03: Owner can redeem voucher on their commission");

    // B03: Chủ đơn thử dùng lại cùng mã cho đơn khác (comm2)
    var comm2OfLegit = new Commission
    {
        ClientId = legitClient,
        CreatorId = Guid.NewGuid(),
        Title = "Second Commission",
        TotalPrice = 500000m
    };
    voucherDb.Commissions.Add(comm2OfLegit);
    await voucherDb.SaveChangesAsync();

    var (secondOk, secondConflict, _, secondErrors) = await voucherHandler.Handle(
        new ArtCommission.Application.Payment.Commands.RedeemVoucher.RedeemVoucherCommand(
            legitClient, "DISCOUNT10", "Commission", comm2OfLegit.Id, 500000m),
        CancellationToken.None);
    Equal(false, secondOk, "B03: User cannot redeem same voucher twice across different commissions");
    Equal("Bạn đã sử dụng mã giảm giá này rồi.", secondErrors[0], "B03: Correct error when voucher reused by same user");
}

// Test B01: Chống race condition duyệt / từ chối payout
{
    await using var payoutDb = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase("payout-b01-" + Guid.NewGuid())
        .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
        .Options);
    var creatorUser = Guid.NewGuid();
    var adminUser = Guid.NewGuid();
    var bankAccount = new BankAccount
    {
        UserId = creatorUser,
        BankName = "MBBank",
        AccountNumber = "0987654321",
        AccountHolder = "CREATOR TEST"
    };
    payoutDb.BankAccounts.Add(bankAccount);
    var payoutReq = new PayoutRequest
    {
        UserId = creatorUser,
        BankAccountId = bankAccount.Id,
        Amount = 100000m,
        Status = PayoutStatus.Pending
    };
    payoutDb.PayoutRequests.Add(payoutReq);
    payoutDb.Wallets.Add(new Wallet { UserId = creatorUser, Balance = 0m });
    await payoutDb.SaveChangesAsync();

    var payoutHandler = new ArtCommission.Application.Payment.Commands.UpdatePayoutRequestStatus.UpdatePayoutRequestStatusCommandHandler(
        payoutDb,
        new WalletService(payoutDb),
        new TestPayOsPayoutService());

    // Lần 1: Admin từ chối thành công -> hoàn tiền
    var (firstOk, _, firstDto, _) = await payoutHandler.Handle(
        new ArtCommission.Application.Payment.Commands.UpdatePayoutRequestStatus.UpdatePayoutRequestStatusCommand(
            adminUser, payoutReq.Id, "Rejected", "Lý do từ chối"),
        CancellationToken.None);
    Equal(true, firstOk, "B01: First rejection succeeds");
    Equal("Rejected", firstDto!.PayoutStatus, "B01: Payout status is Rejected");

    // Lần 2: Admin khác cố xử lý lại -> bị từ chối, không thể xử lý lại
    var (secondOk, _, _, secondErrors) = await payoutHandler.Handle(
        new ArtCommission.Application.Payment.Commands.UpdatePayoutRequestStatus.UpdatePayoutRequestStatusCommand(
            Guid.NewGuid(), payoutReq.Id, "Rejected", "Lý do khác"),
        CancellationToken.None);
    Equal(false, secondOk, "B01: Second duplicate status update rejected");
    Equal(true, secondErrors[0].Contains("không thể xử lý lại"), "B01: Correct error message on duplicate payout update");
}

Console.WriteLine($"Passed {checks} commission workflow and security audit checks.");

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

sealed class TestPayOsPayoutService : IPayOsPayoutService
{
    public Task<PayOsPayoutResult> CreatePayoutAsync(PayOsPayoutRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(new PayOsPayoutResult(true, "TX-TEST-01", null, "OK"));
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
