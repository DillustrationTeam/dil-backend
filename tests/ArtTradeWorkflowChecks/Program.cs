using ArtCommission.Application.ArtTrade.Commands;
using ArtCommission.Application.ArtTrade.Common;
using ArtCommission.Application.ArtTrade.DTOs;
using ArtCommission.Application.ArtTrade.Queries;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Dispute.TradeDispute.Commands;
using ArtCommission.Application.Dispute.TradeDispute.DTOs;
using ArtCommission.Application.Dispute.TradeDispute.Queries;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Trade;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text;

Console.WriteLine("=================================================================");
Console.WriteLine("STARTING ART TRADE & TRADE DISPUTE FUNCTIONAL VERIFICATION SUITE");
Console.WriteLine("=================================================================\n");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("art-trade-test-" + Guid.NewGuid())
    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
    .Options;

await using var db = new AppDbContext(options);

var storageService = new TestStorageService();
var watermarkService = new TestWatermarkService();
var slotService = new TradeSlotService(db);

int passedChecks = 0;

void AssertEqual<T>(T expected, T actual, string testName)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[FAIL] {testName} - Expected: '{expected}', Got: '{actual}'");
        Console.ResetColor();
        throw new Exception($"Test assertion failed: {testName}");
    }
    passedChecks++;
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[PASS] {testName}");
    Console.ResetColor();
}

void AssertTrue(bool condition, string testName)
{
    AssertEqual(true, condition, testName);
}

// -------------------------------------------------------------
// SEED USERS
// -------------------------------------------------------------
var artistA = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "artist_a",
    FullName = "Artist Alpha",
    Email = "artist_a@test.com"
};

var artistB = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "artist_b",
    FullName = "Artist Beta",
    Email = "artist_b@test.com"
};

var artistC = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "artist_c",
    FullName = "Artist Charlie",
    Email = "artist_c@test.com"
};

var adminUser = new ApplicationUser
{
    Id = Guid.NewGuid(),
    UserName = "admin_user",
    FullName = "Administrator",
    Email = "admin@test.com"
};

db.Users.AddRange(artistA, artistB, artistC, adminUser);
await db.SaveChangesAsync();

// =============================================================
// TEST 1: INITIAL TRADE SLOT STATUS
// =============================================================
Console.WriteLine("\n--- TEST 1: INITIAL TRADE SLOT STATUS ---");
var initialSlotStatus = await slotService.GetUserSlotStatusAsync(artistA.Id);
AssertEqual(2, initialSlotStatus.TotalSlots, "TotalSlots default is 2");
AssertEqual(2, initialSlotStatus.AvailableSlots, "Initial AvailableSlots is 2");
AssertEqual(0, initialSlotStatus.ActiveTradesCount, "Initial ActiveTradesCount is 0");
AssertEqual(0, initialSlotStatus.FrozenSlotsCount, "Initial FrozenSlotsCount is 0");
AssertTrue(initialSlotStatus.CanTrade, "Artist A CanTrade is true");

// =============================================================
// TEST 2: PROPOSE TRADE VALIDATIONS
// =============================================================
Console.WriteLine("\n--- TEST 2: PROPOSE TRADE VALIDATIONS ---");
var proposeHandler = new ProposeTradeCommandHandler(db, slotService);

// Cannot self-trade
var selfTradeCmd = new ProposeTradeCommand(artistA.Id, artistA.Id, "Self Trade", null, null);
var (selfSuccess, _, selfErrors) = await proposeHandler.Handle(selfTradeCmd, default);
AssertTrue(!selfSuccess, "Cannot self trade succeeds is false");
AssertTrue(selfErrors.Any(e => e.Contains("chính mình")), "Error contains cannot self-trade message");

// Empty title validation
var emptyTitleCmd = new ProposeTradeCommand(artistA.Id, artistB.Id, "", null, null);
var (emptySuccess, _, emptyErrors) = await proposeHandler.Handle(emptyTitleCmd, default);
AssertTrue(!emptySuccess, "Empty title fails validation");

// =============================================================
// TEST 3: PROPOSE TRADE SUCCESS & SLOT RESERVATION
// =============================================================
Console.WriteLine("\n--- TEST 3: PROPOSE TRADE SUCCESS & SLOT RESERVATION ---");
var proposeCmd = new ProposeTradeCommand(
    InitiatorId: artistA.Id,
    ReceiverId: artistB.Id,
    Title: "Fantasy Character Art Trade",
    Description: "Let's draw each other's OC",
    RequestNote: "Please draw my elf mage",
    DurationDays: 14
);

var (propSuccess, propData, propErrors) = await proposeHandler.Handle(proposeCmd, default);
AssertTrue(propSuccess, "Trade proposal succeeded");
AssertTrue(propData != null, "Trade DTO returned");
AssertEqual(TradeStatus.Proposed, propData!.Status, "Trade status is Proposed");
AssertEqual(artistA.Id, propData.Initiator.Id, "Initiator is Artist A");
AssertEqual(artistB.Id, propData.Receiver.Id, "Receiver is Artist B");

// Cannot send duplicate pending proposal to the same user
var dupProposalCmd = new ProposeTradeCommand(artistA.Id, artistB.Id, "Another Trade", null, null);
var (dupSuccess, _, dupErrors) = await proposeHandler.Handle(dupProposalCmd, default);
AssertTrue(!dupSuccess, "Duplicate proposal blocked");

// =============================================================
// TEST 4: RESPOND TO PROPOSAL (REJECTION FREES SLOT)
// =============================================================
Console.WriteLine("\n--- TEST 4: RESPOND TO PROPOSAL (REJECTION FREES SLOT) ---");
var respondHandler = new RespondTradeProposalCommandHandler(db, slotService);

var rejectCmd = new RespondTradeProposalCommand(
    TradeId: propData.Id,
    UserId: artistB.Id,
    Accept: false,
    RejectionReason: "Currently too busy"
);
var (rejSuccess, rejData, _) = await respondHandler.Handle(rejectCmd, default);
AssertTrue(rejSuccess, "Rejection processed");
AssertEqual(TradeStatus.Rejected, rejData!.Status, "Status updated to Rejected");

// Artist A slot freed up
var aSlotAfterRejection = await slotService.GetUserSlotStatusAsync(artistA.Id);
AssertEqual(2, aSlotAfterRejection.AvailableSlots, "Artist A available slots restored to 2");

// =============================================================
// TEST 5: ACCEPT TRADE PROPOSAL
// =============================================================
Console.WriteLine("\n--- TEST 5: ACCEPT TRADE PROPOSAL ---");
var validProposal = await proposeHandler.Handle(new ProposeTradeCommand(
    artistA.Id, artistB.Id, "Valid Trade", "Description", "A request note", 20
), default);
var tradeId = validProposal.Data!.Id;

var acceptCmd = new RespondTradeProposalCommand(
    TradeId: tradeId,
    UserId: artistB.Id,
    Accept: true,
    RequestNote: "Draw my knight warrior"
);
var (accSuccess, accData, _) = await respondHandler.Handle(acceptCmd, default);
AssertTrue(accSuccess, "Proposal accepted");
AssertEqual(TradeStatus.Accepted, accData!.Status, "Trade status is Accepted");
AssertTrue(accData.SubmissionDeadlineAt.HasValue, "Submission deadline is set");

// Both users have 1 active trade
var aSlotAccepted = await slotService.GetUserSlotStatusAsync(artistA.Id);
var bSlotAccepted = await slotService.GetUserSlotStatusAsync(artistB.Id);
AssertEqual(1, aSlotAccepted.AvailableSlots, "Artist A available slots is 1");
AssertEqual(1, bSlotAccepted.AvailableSlots, "Artist B available slots is 1");

// =============================================================
// TEST 6: MAX 2 TRADES ENFORCEMENT
// =============================================================
Console.WriteLine("\n--- TEST 6: MAX 2 TRADES ENFORCEMENT ---");
// Artist A initiates second trade with Artist C, and Artist C accepts it
var trade2 = await proposeHandler.Handle(new ProposeTradeCommand(
    artistA.Id, artistC.Id, "Second Trade", null, null
), default);
AssertTrue(trade2.Success, "Second trade proposed");
await respondHandler.Handle(new RespondTradeProposalCommand(trade2.Data!.Id, artistC.Id, true), default);

var aSlotFull = await slotService.GetUserSlotStatusAsync(artistA.Id);
AssertEqual(0, aSlotFull.AvailableSlots, "Artist A available slots is 0 (2 active)");
AssertTrue(!aSlotFull.CanTrade, "Artist A CanTrade is false");

// Artist A tries to propose a 3rd trade -> blocked by quota
var trade3 = await proposeHandler.Handle(new ProposeTradeCommand(
    artistA.Id, artistC.Id, "Third Trade", null, null
), default);
AssertTrue(!trade3.Success, "3rd trade proposal blocked by quota");
AssertTrue(trade3.Errors.Any(e => e.Contains("slot", StringComparison.OrdinalIgnoreCase)), "Error message mentions slot quota");

// Clean up trade 2 so A has 1 slot available
var cancelHandler = new CancelTradeCommandHandler(db, slotService);
await cancelHandler.Handle(new CancelTradeCommand(trade2.Data!.Id, artistA.Id, "Cancelled second"), default);

// =============================================================
// TEST 7: SUBMIT ARTWORK (BOTH SIDES)
// =============================================================
Console.WriteLine("\n--- TEST 7: SUBMIT ARTWORK (BOTH SIDES) ---");
var submitHandler = new SubmitTradeArtworkCommandHandler(db, storageService, watermarkService);

// 1. Artist A submits artwork (PNG)
using var aStream = new MemoryStream(Encoding.UTF8.GetBytes("Fake PNG data"));
var submitACmd = new SubmitTradeArtworkCommand(
    TradeId: tradeId,
    UserId: artistA.Id,
    FileStream: aStream,
    ContentType: "image/png",
    FileName: "alpha_art.png",
    ArtworkTitle: "Elf Mage Masterpiece"
);
var (subASuccess, subAData, subAErrors) = await submitHandler.Handle(submitACmd, default);
AssertTrue(subASuccess, "Artist A artwork submission succeeded");
AssertEqual(TradeStatus.Accepted, subAData!.Status, "Status still Accepted after only 1 side submitted");
AssertTrue(subAData.InitiatorSubmittedAt.HasValue, "InitiatorSubmittedAt is set");
AssertTrue(!string.IsNullOrEmpty(subAData.InitiatorPreviewUrl), "Watermarked preview URL is generated");

// 2. Stranger tries to submit -> blocked
using var cStream = new MemoryStream(Encoding.UTF8.GetBytes("Fake PNG data"));
var submitCCmd = new SubmitTradeArtworkCommand(tradeId, artistC.Id, cStream, "image/png", "fake.png");
var (subCSuccess, _, _) = await submitHandler.Handle(submitCCmd, default);
AssertTrue(!subCSuccess, "Stranger cannot submit artwork to this trade");

// 3. Artist B submits artwork (PSD) -> triggers BothSubmitted status
using var bStream = new MemoryStream(Encoding.UTF8.GetBytes("Fake PSD data"));
var submitBCmd = new SubmitTradeArtworkCommand(
    TradeId: tradeId,
    UserId: artistB.Id,
    FileStream: bStream,
    ContentType: "image/vnd.adobe.photoshop",
    FileName: "beta_art.psd",
    ArtworkTitle: "Knight Warrior PSD"
);
var (subBSuccess, subBData, _) = await submitHandler.Handle(submitBCmd, default);
AssertTrue(subBSuccess, "Artist B artwork submission succeeded");
AssertEqual(TradeStatus.BothSubmitted, subBData!.Status, "Status transitioned to BothSubmitted!");
AssertTrue(subBData.ReceiverSubmittedAt.HasValue, "ReceiverSubmittedAt is set");
AssertTrue(subBData.ConfirmationDeadlineAt.HasValue, "ConfirmationDeadlineAt is set (7 days)");

// Link artwork entities for cross-ownership transfer test
var artworkA = new Artwork { Id = Guid.NewGuid(), Title = "A Masterpiece", CreatorProfileId = Guid.NewGuid(), ImageUrl = "https://cdn.example.com/a.png", CreatedAt = DateTimeOffset.UtcNow };
var artworkB = new Artwork { Id = Guid.NewGuid(), Title = "B Masterpiece", CreatorProfileId = Guid.NewGuid(), ImageUrl = "https://cdn.example.com/b.png", CreatedAt = DateTimeOffset.UtcNow };
db.Artworks.AddRange(artworkA, artworkB);

var activeTradeDb = await db.ArtTrades.FindAsync(tradeId);
activeTradeDb!.InitiatorArtworkId = artworkA.Id;
activeTradeDb.ReceiverArtworkId = artworkB.Id;
await db.SaveChangesAsync();

// =============================================================
// TEST 8: DUAL CONFIRMATION & COMPLETION & OWNERSHIP TRANSFER
// =============================================================
Console.WriteLine("\n--- TEST 8: DUAL CONFIRMATION & COMPLETION ---");
var confirmHandler = new ConfirmTradeCommandHandler(db);
var downloadHandler = new GetTradeDownloadUrlQueryHandler(db, storageService);

// Artist A tries to download original file BEFORE completion -> blocked
var preDownload = await downloadHandler.Handle(new GetTradeDownloadUrlQuery(tradeId, artistA.Id), default);
AssertTrue(!preDownload.Success, "Download URL blocked before trade Completed");

// 1. Artist A confirms satisfaction
var confirmA = await confirmHandler.Handle(new ConfirmTradeCommand(tradeId, artistA.Id), default);
AssertTrue(confirmA.Success, "Artist A confirmed satisfaction");
AssertEqual(TradeStatus.BothSubmitted, confirmA.Data!.Status, "Trade status remains BothSubmitted until B confirms");
AssertTrue(confirmA.Data.IsInitiatorConfirmed, "IsInitiatorConfirmed is true");
AssertTrue(!confirmA.Data.IsReceiverConfirmed, "IsReceiverConfirmed is still false");

// 2. Artist B confirms satisfaction -> completes trade!
var confirmB = await confirmHandler.Handle(new ConfirmTradeCommand(tradeId, artistB.Id), default);
AssertTrue(confirmB.Success, "Artist B confirmed satisfaction");
AssertEqual(TradeStatus.Completed, confirmB.Data!.Status, "Trade status transitioned to Completed!");
AssertTrue(confirmB.Data.CompletedAt.HasValue, "CompletedAt timestamp is set");

// 3. Verify cross-ownership creation in ArtworkOwnerships
var aArtworkId = confirmB.Data.InitiatorArtworkId!.Value;
var bArtworkId = confirmB.Data.ReceiverArtworkId!.Value;

var ownerships = await db.ArtworkOwnerships.Where(o => o.ArtTradeId == tradeId).ToListAsync();
AssertEqual(2, ownerships.Count, "Exactly 2 ownership records created for cross trade");

var aReceivedBArt = ownerships.FirstOrDefault(o => o.ArtworkId == bArtworkId && o.OwnerId == artistA.Id);
AssertTrue(aReceivedBArt != null, "Artist A now owns Artist B's artwork");
AssertEqual(OwnershipTransferReason.ArtTrade, aReceivedBArt!.TransferReason, "TransferReason is ArtTrade");
AssertTrue(aReceivedBArt.IsCurrent, "A's ownership is marked IsCurrent = true");

var bReceivedAArt = ownerships.FirstOrDefault(o => o.ArtworkId == aArtworkId && o.OwnerId == artistB.Id);
AssertTrue(bReceivedAArt != null, "Artist B now owns Artist A's artwork");
AssertEqual(OwnershipTransferReason.ArtTrade, bReceivedAArt!.TransferReason, "TransferReason is ArtTrade");
AssertTrue(bReceivedAArt.IsCurrent, "B's ownership is marked IsCurrent = true");

// 4. Verify completed trades release active slots
var aSlotCompleted = await slotService.GetUserSlotStatusAsync(artistA.Id);
var bSlotCompleted = await slotService.GetUserSlotStatusAsync(artistB.Id);
AssertEqual(2, aSlotCompleted.AvailableSlots, "Artist A slots restored to 2 after completion");
AssertEqual(2, bSlotCompleted.AvailableSlots, "Artist B slots restored to 2 after completion");

// =============================================================
// TEST 9: PRESIGNED DOWNLOAD URL FOR COMPLETED TRADE
// =============================================================
Console.WriteLine("\n--- TEST 9: PRESIGNED DOWNLOAD URL FOR COMPLETED TRADE ---");
// Artist A requests download URL (should get B's original file: beta_art.psd)
var downloadA = await downloadHandler.Handle(new GetTradeDownloadUrlQuery(tradeId, artistA.Id), default);
AssertTrue(downloadA.Success, "Artist A gets presigned download URL");
AssertTrue(downloadA.Data!.DownloadUrl.Contains("https://"), "Download URL is valid presigned link");
AssertTrue(downloadA.Data.FileName.EndsWith(".psd"), "Artist A receives partner's file extension (.psd)");

// Artist B requests download URL (should get A's original file: alpha_art.png)
var downloadB = await downloadHandler.Handle(new GetTradeDownloadUrlQuery(tradeId, artistB.Id), default);
AssertTrue(downloadB.Success, "Artist B gets presigned download URL");
AssertTrue(downloadB.Data!.FileName.EndsWith(".png"), "Artist B receives partner's file extension (.png)");

// Stranger (Artist C) requests download URL -> blocked
var downloadC = await downloadHandler.Handle(new GetTradeDownloadUrlQuery(tradeId, artistC.Id), default);
AssertTrue(!downloadC.Success, "Stranger cannot get download URL");

// =============================================================
// TEST 10: VIOLATION CANCELLATION & 30-DAY SLOT FREEZE
// =============================================================
Console.WriteLine("\n--- TEST 10: VIOLATION CANCELLATION & 30-DAY SLOT FREEZE ---");
// Create a new trade between B and C
var violationTrade = await proposeHandler.Handle(new ProposeTradeCommand(
    artistB.Id, artistC.Id, "Trade with Flaker", null, null
), default);
var vTradeId = violationTrade.Data!.Id;
await respondHandler.Handle(new RespondTradeProposalCommand(vTradeId, artistC.Id, true), default);

// Artist C flakes / misses deadline -> cancelled with violation flagged against C
var cancelViolationCmd = new CancelTradeCommand(
    TradeId: vTradeId,
    UserId: artistB.Id,
    Reason: "Artist C flaked and submitted nothing after deadline",
    IsViolation: true,
    ViolatorUserId: artistC.Id
);
var cancelRes = await cancelHandler.Handle(cancelViolationCmd, default);
AssertTrue(cancelRes.Success, "Cancellation with violation processed");

// Verify Artist C has 1 frozen slot for 30 days!
var cSlotFrozen = await slotService.GetUserSlotStatusAsync(artistC.Id);
AssertEqual(1, cSlotFrozen.FrozenSlotsCount, "Artist C has 1 frozen slot");
AssertEqual(1, cSlotFrozen.AvailableSlots, "Artist C available slots is 1 (2 total - 1 frozen)");
AssertEqual(1, cSlotFrozen.FrozenDetails.Count, "Artist C frozen details list has 1 entry");
AssertTrue(cSlotFrozen.FrozenDetails[0].DaysRemaining >= 29, "Days remaining is ~30 days");

// =============================================================
// TEST 11: TRADE DISPUTE CREATION & ADMIN RESOLUTION
// =============================================================
Console.WriteLine("\n--- TEST 11: TRADE DISPUTE CREATION & ADMIN RESOLUTION ---");
var disputeCreateHandler = new CreateTradeDisputeCommandHandler(db);
var disputeResolveHandler = new ResolveTradeDisputeCommandHandler(db, slotService);
var disputeGetByIdHandler = new GetTradeDisputeByIdQueryHandler(db);
var disputeListHandler = new GetTradeDisputesQueryHandler(db);

// Create another trade between A and B
var disputeTrade = await proposeHandler.Handle(new ProposeTradeCommand(
    artistA.Id, artistB.Id, "AI Suspicion Trade", null, null
), default);
var dTradeId = disputeTrade.Data!.Id;
await respondHandler.Handle(new RespondTradeProposalCommand(dTradeId, artistB.Id, true), default);

// Artist B files dispute alleging Artist A submitted stolen or AI art
var createDisputeCmd = new CreateTradeDisputeCommand(
    ArtTradeId: dTradeId,
    RaisedById: artistB.Id,
    Reason: "Partner submitted AI generated artwork contrary to rules",
    EvidenceUrls: new List<string> { "https://evidence.com/ai_detector.png" }
);
var (dispSuccess, dispMsg, dispId, _) = await disputeCreateHandler.Handle(createDisputeCmd, default);
AssertTrue(dispSuccess, "Trade dispute created successfully");
AssertTrue(dispId.HasValue, "Dispute ID generated");

// Trade status transitioned to Disputed
var dTradeEntity = await db.ArtTrades.FindAsync(dTradeId);
AssertEqual(TradeStatus.Disputed, dTradeEntity!.Status, "Trade status transitioned to Disputed");

// Check dispute detail query
var dispDetail = await disputeGetByIdHandler.Handle(new GetTradeDisputeByIdQuery(dispId!.Value, artistB.Id), default);
AssertTrue(dispDetail.Success, "Dispute query by ID succeeded");
AssertEqual("Pending", dispDetail.Data!.Status, "Dispute status is Pending");
AssertEqual(1, dispDetail.Data.EvidenceUrlList.Count, "Evidence URL parsed correctly");

// Admin reviews and finds Artist A at fault (FaultInitiator)
var resolveCmd = new ResolveTradeDisputeCommand(
    DisputeId: dispId.Value,
    AdminId: adminUser.Id,
    Resolution: TradeDisputeResolutions.FaultInitiator,
    AdminNote: "Verified AI generation artifacts in submitted file. Initiator penalized."
);
var (resolveSuccess, resolveMsg, _) = await disputeResolveHandler.Handle(resolveCmd, default);
AssertTrue(resolveSuccess, "Dispute resolved by Admin");

// Verify dispute status = Resolved
var resolvedDispute = await db.TradeDisputes.FindAsync(dispId.Value);
AssertEqual("Resolved", resolvedDispute!.Status, "Dispute status is Resolved");
AssertEqual(TradeDisputeResolutions.FaultInitiator, resolvedDispute.Resolution, "Resolution is FaultInitiator");

// Verify Artist A (Initiator) got 1 slot frozen for 30 days
var aSlotAfterDispute = await slotService.GetUserSlotStatusAsync(artistA.Id);
AssertEqual(1, aSlotAfterDispute.FrozenSlotsCount, "Artist A has 1 frozen slot due to dispute fault");
AssertEqual(1, aSlotAfterDispute.AvailableSlots, "Artist A available slots is reduced to 1");

// Artist B (the victim) is NOT penalized
var bSlotAfterDispute = await slotService.GetUserSlotStatusAsync(artistB.Id);
AssertEqual(0, bSlotAfterDispute.FrozenSlotsCount, "Artist B has 0 frozen slots");
AssertEqual(2, bSlotAfterDispute.AvailableSlots, "Artist B has full 2 slots available");

// Query disputes list
var listDisputes = await disputeListHandler.Handle(new GetTradeDisputesQuery("Resolved"), default);
AssertTrue(listDisputes.Success, "List disputes query succeeded");
AssertTrue(listDisputes.Items.Count >= 1, "Resolved dispute is in list");

// =============================================================
// TEST 12: SELF-HEALING / EXPIRED FREEZE CALCULATION
// =============================================================
Console.WriteLine("\n--- TEST 12: SELF-HEALING EXPIRED FREEZE DYNAMICS ---");
// Simulate an expired freeze from 35 days ago for Artist C
var oldExpiredFreeze = new TradeSlotFreeze
{
    UserId = artistC.Id,
    ArtTradeId = vTradeId,
    FrozenFrom = DateTimeOffset.UtcNow.AddDays(-35),
    FrozenUntil = DateTimeOffset.UtcNow.AddDays(-5), // Expired 5 days ago
    Reason = "Old violation from last month"
};
db.TradeSlotFreezes.Add(oldExpiredFreeze);
await db.SaveChangesAsync();

// Check Artist C slots: only the active freeze should be counted, expired freeze is ignored
var cSlotCheck = await slotService.GetUserSlotStatusAsync(artistC.Id);
AssertEqual(1, cSlotCheck.FrozenSlotsCount, "Only the 1 active non-expired freeze is counted");
AssertEqual(1, cSlotCheck.AvailableSlots, "Available slots calculation cleanly ignores expired freezes");

// Test Admin manual freeze revocation
var activeFreeze = await db.TradeSlotFreezes.FirstOrDefaultAsync(f => f.UserId == artistC.Id && f.FrozenUntil > DateTimeOffset.UtcNow);
AssertTrue(activeFreeze != null, "Found active freeze for Artist C");
activeFreeze!.IsRevoked = true;
activeFreeze.RevokedByAdminId = adminUser.Id;
activeFreeze.RevokedReason = "Admin pardoned user";
activeFreeze.RevokedAt = DateTimeOffset.UtcNow;
await db.SaveChangesAsync();

var cSlotPardoned = await slotService.GetUserSlotStatusAsync(artistC.Id);
AssertEqual(0, cSlotPardoned.FrozenSlotsCount, "Frozen slots count is 0 after revocation");
AssertEqual(2, cSlotPardoned.AvailableSlots, "Artist C has 2 available slots after pardon");

// =============================================================
// ALL TESTS PASSED SUMMARY
// =============================================================
Console.WriteLine("\n=================================================================");
Console.ForegroundColor = ConsoleColor.Green;
Console.WriteLine($"ALL {passedChecks} CHECKS PASSED SUCCESSFULLY!");
Console.ResetColor();
Console.WriteLine("=================================================================\n");

// -------------------------------------------------------------
// TEST MOCK CLASSES
// -------------------------------------------------------------
public class TestStorageService : IStorageService
{
    public Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
        => Task.FromResult($"https://cdn.example.com/{key}");

    public Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default)
        => Task.FromResult($"private/{key}");

    public string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry)
        => $"https://private-bucket.example.com/{privateKey}?expires={DateTimeOffset.UtcNow.Add(expiry).ToUnixTimeSeconds()}";
}

public class TestWatermarkService : IWatermarkService
{
    public Task<Stream> ApplyWatermarkAsync(Stream imageStream, string watermarkText, CancellationToken cancellationToken = default)
    {
        var ms = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG header bytes
        return Task.FromResult<Stream>(ms);
    }
}
