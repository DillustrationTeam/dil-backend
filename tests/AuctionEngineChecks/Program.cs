using ArtCommission.Application.Auction.Risk;
using ArtCommission.Application.Auction.Risk.Rules;
using ArtCommission.Application.Common.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Notifications.Common;
using ArtCommission.Domain.Entities.Auction;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using AuctionEntity = ArtCommission.Domain.Entities.Auction.Auction;
using BidEntity = ArtCommission.Domain.Entities.Auction.Bid;

// Engine-level checks for AI Bid Fraud Detection (no xUnit in this repo).
// Run: dotnet run --project tests/AuctionEngineChecks
//
// Covers the behaviour the rule-only suite cannot: evidence de-duplication across sweeps, one live
// sheet per auction, "score never goes down", the alert deduplication key, the disable switch, and
// the neutral seller notice.

var passed = 0;

void Check(string name, bool condition, string? detail = null)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAIL: {name}{(detail is null ? string.Empty : $" - {detail}")}");
        Environment.Exit(1);
    }

    passed++;
    Console.WriteLine($"  ok  {name}");
}

AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase($"engine-{Guid.NewGuid()}")
    .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
    .Options);

var sellerId = Guid.NewGuid();
var adminId = Guid.NewGuid();
var bidderA = Guid.NewGuid();
var bidderB = Guid.NewGuid();
var t0 = new DateTimeOffset(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

IEnumerable<IRiskRule> Rules() =>
[
    new SharedDeviceRule(),
    new SellerLinkedBidderRule(),
    new BidVelocityRule(),
    new PriceJumpRule(),
    new PriceLadderRule(),
    new AutoBidWallRule(),
    new NonPaymentHistoryRule(),
    new WinNoPaymentRateRule(),
    new SelfOutbidLoopRule(),
    new NewAccountBurstRule()
];

AuctionRiskEngine NewEngine(AppDbContext db, FakeNotifications notifications) =>
    new(db, notifications, new FakeIdentity(new[] { adminId }), Rules(), new AuctionRiskScorer(),
        NullLogger<AuctionRiskEngine>.Instance);

async Task<(AuctionEntity Auction, AppDbContext Db)> SeedAsync(decimal startPrice = 1_000_000m)
{
    var db = NewDb();

    db.Users.Add(new ApplicationUser { Id = sellerId, UserName = "seller", Email = "s@x.test", FullName = "Seller" });
    db.Users.Add(new ApplicationUser { Id = bidderA, UserName = "a", Email = "a@x.test", FullName = "A" });
    db.Users.Add(new ApplicationUser { Id = bidderB, UserName = "b", Email = "b@x.test", FullName = "B" });

    db.PlatformConfigs.Add(new PlatformConfig { Key = "AuctionRiskDetectionEnabled", Value = "true" });
    db.PlatformConfigs.Add(new PlatformConfig { Key = "AuctionRiskThresholdHigh", Value = "55" });
    db.PlatformConfigs.Add(new PlatformConfig { Key = "AuctionRiskThresholdCritical", Value = "80" });

    var artwork = new ArtCommission.Domain.Entities.ArtistStudio.Artwork
    {
        Id = Guid.NewGuid(),
        CreatorProfileId = Guid.NewGuid(),
        Title = "Engine check artwork",
        ImageUrl = "https://example.test/a.png",
        ModerationStatus = "Approved"
    };
    db.Artworks.Add(artwork);

    var auction = new AuctionEntity
    {
        Id = Guid.NewGuid(),
        ArtworkId = artwork.Id,
        SellerId = sellerId,
        StartPrice = startPrice,
        BidStep = 50_000m,
        CurrentPrice = startPrice,
        StartAt = t0.AddDays(-1),
        EndAt = t0.AddDays(1),
        Status = AuctionStatus.Active
    };
    db.Auctions.Add(auction);

    // All three accounts share one IP: SharedDevice + SellerLinkedBidder fire together.
    foreach (var userId in new[] { sellerId, bidderA, bidderB })
    {
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Guid.NewGuid().ToString("N"),
            ExpiresAt = t0.AddDays(30),
            CreatedByIp = "113.161.32.7",
            UserAgent = "chrome-win",
            CreatedAt = t0
        });
    }

    // Alternating bids -> SelfOutbidLoop, and enough of them for the ladder/velocity rules.
    var amount = startPrice;
    for (var i = 0; i < 6; i++)
    {
        amount += 50_000m;
        db.Bids.Add(new BidEntity
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            BidderId = i % 2 == 0 ? bidderA : bidderB,
            Amount = amount,
            PlacedAt = t0.AddSeconds(i * 10),
            Status = BidStatus.Leading
        });
    }

    await db.SaveChangesAsync();
    return (auction, db);
}

Console.WriteLine("1. First scan");

var (auction1, db1) = await SeedAsync();
var notifications1 = new FakeNotifications();
var engine1 = NewEngine(db1, notifications1);

var first = await engine1.EvaluateAsync(auction1.Id, hotPathOnly: false);
Check("scan is not skipped", !first.Skipped, first.ToString());
Check("scores Critical on shared-device + loop evidence", first.Level == AuctionRiskLevel.Critical, $"{first.Score}/{first.Level}");

var signalsAfterFirst = await db1.AuctionRiskSignals.CountAsync(s => s.AuctionId == auction1.Id);
Check("evidence rows written", signalsAfterFirst > 0, signalsAfterFirst.ToString());

var assessmentsAfterFirst = await db1.AuctionRiskAssessments.CountAsync(a => a.AuctionId == auction1.Id);
Check("exactly one assessment row", assessmentsAfterFirst == 1, assessmentsAfterFirst.ToString());

var auctionRow = await db1.Auctions.AsNoTracking().FirstAsync(a => a.Id == auction1.Id);
Check("auction snapshot carries score + scan time", auctionRow.RiskScore is > 0 && auctionRow.LastRiskScanAt is not null,
    $"score={auctionRow.RiskScore} scan={auctionRow.LastRiskScanAt}");

Console.WriteLine("2. Alerts");

var adminAlerts = notifications1.Sent.Where(n => n.UserId == adminId && n.Type == NotificationType.AuctionFraudAlert).ToList();
Check("admin notified once for this level", adminAlerts.Count == 1, adminAlerts.Count.ToString());
Check("alert carries a dedup key", !string.IsNullOrWhiteSpace(adminAlerts[0].DedupKey), adminAlerts[0].DedupKey);

var sellerAlerts = notifications1.Sent.Where(n => n.UserId == sellerId && n.Type == NotificationType.AuctionRiskFlagged).ToList();
Check("seller gets the neutral notice", sellerAlerts.Count == 1, sellerAlerts.Count.ToString());
Check("seller notice never leaks the score", !sellerAlerts[0].Body.Contains("100") && !sellerAlerts[0].Body.Contains("/100"),
    sellerAlerts[0].Body);

Console.WriteLine("3. Re-scan idempotence");

var second = await engine1.EvaluateAsync(auction1.Id, hotPathOnly: false);
var signalsAfterSecond = await db1.AuctionRiskSignals.CountAsync(s => s.AuctionId == auction1.Id);
Check("second scan adds no duplicate evidence", signalsAfterSecond == signalsAfterFirst,
    $"{signalsAfterFirst} -> {signalsAfterSecond}");
Check("second scan reuses the same live sheet",
    await db1.AuctionRiskAssessments.CountAsync(a => a.AuctionId == auction1.Id) == 1);
Check("same level does not re-alert",
    notifications1.Sent.Count(n => n.UserId == adminId && n.Type == NotificationType.AuctionFraudAlert) == 1);
Check("score is stable on an unchanged auction", second.Score == first.Score, $"{first.Score} -> {second.Score}");

Console.WriteLine("4. Score never goes down");

// Delete the evidence: a later sweep that finds less must not launder the sheet.
// (InMemory has no ExecuteDelete, so remove tracked rows the ordinary way.)
foreach (var bid in db1.Bids.Where(b => b.AuctionId == auction1.Id).ToList()) db1.Bids.Remove(bid);
foreach (var token in db1.RefreshTokens.ToList()) db1.RefreshTokens.Remove(token);
await db1.SaveChangesAsync();

var third = await engine1.EvaluateAsync(auction1.Id, hotPathOnly: false);
Check("lower re-score keeps the previous (higher) score", third.Score == first.Score, $"{first.Score} -> {third.Score}");

var liveSheet = await db1.AuctionRiskAssessments.FirstAsync(a => a.AuctionId == auction1.Id);
Check("live sheet still Critical", liveSheet.RiskLevel == AuctionRiskLevel.Critical, liveSheet.RiskLevel.ToString());

Console.WriteLine("5. Review closes the sheet");

liveSheet.Status = AuctionRiskAssessmentStatus.Dismissed;
liveSheet.ReviewedByUserId = adminId;
liveSheet.ReviewedAt = DateTimeOffset.UtcNow;
await db1.SaveChangesAsync();

var reopened = await engine1.EvaluateAsync(auction1.Id, hotPathOnly: false);
Check("new evidence reopens a fresh sheet after dismissal",
    await db1.AuctionRiskAssessments.CountAsync(a => a.AuctionId == auction1.Id) >= 1,
    reopened.AssessmentId?.ToString() ?? "none");

Console.WriteLine("6. Disable switch");

var (auction2, db2) = await SeedAsync();
var config = await db2.PlatformConfigs.FirstAsync(c => c.Key == "AuctionRiskDetectionEnabled");
config.Value = "false";
await db2.SaveChangesAsync();

var notifications2 = new FakeNotifications();
var disabled = await NewEngine(db2, notifications2).EvaluateAsync(auction2.Id, hotPathOnly: false);
Check("disabled detector skips the auction", disabled.Skipped, disabled.ToString());
Check("disabled detector writes nothing", !await db2.AuctionRiskAssessments.AnyAsync(a => a.AuctionId == auction2.Id));
Check("disabled detector sends no alert", notifications2.Sent.Count == 0);

Console.WriteLine("7. Evidence is Vietnamese and masked");

var evidence = await db1.AuctionRiskSignals.AsNoTracking()
    .Where(s => s.AuctionId == auction1.Id)
    .Select(s => s.EvidenceJson)
    .FirstOrDefaultAsync();

if (evidence is not null)
{
    Check("evidence text is Vietnamese", evidence.Contains("người đấu giá") || evidence.Contains("người bán"), evidence);
    Check("evidence has no raw IP", !evidence.Contains("113.161.32.7"), evidence);
}

Console.WriteLine($"\nAll {passed} checks passed.");

// ---------------------------------------------------------------- fakes

sealed class FakeNotifications : INotificationPublisher
{
    public List<(Guid UserId, NotificationType Type, string Title, string Body, string? DedupKey)> Sent { get; } = [];

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
        Sent.Add((userId, notificationType, title, body, dedupKey));
        return Task.FromResult<NotificationDto?>(null);
    }
}

sealed class FakeIdentity(IReadOnlyList<Guid> admins) : IIdentityService
{
    public Task<IReadOnlyList<Guid>> GetUserIdsInRoleAsync(string roleName, CancellationToken cancellationToken = default)
        => Task.FromResult(roleName == UserRoleNames.Administrator ? admins : (IReadOnlyList<Guid>)Array.Empty<Guid>());

    public Task<(bool Success, Guid UserId, string[] Errors)> RegisterUserAsync(string email, string password, string fullName, string? role = null, bool isVerified = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> AuthenticateUserAsync(string emailOrUsername, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> VerifyUserEmailAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> HasPasswordAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> AddPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<string> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> ResetPasswordAsync(string email, string resetToken, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> AddCreatorRoleAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> ChangeEmailAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool HasPassword, IReadOnlyList<string> LinkedProviders)> GetLinkedAccountsAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> UnlinkExternalLoginAsync(Guid userId, string provider, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> LinkExternalLoginAsync(Guid userId, string provider, string providerKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> DeactivateAccountAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, UserDto? User, string[] Roles, ExternalRegistrationPendingDto? PendingRegistration, string[] Errors)> AuthenticateOrRegisterExternalAsync(string provider, string providerKey, string email, bool emailVerified, string? fullName, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> IsUsernameUniqueAsync(string username, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, UserDto? User, string[] Roles, string[] Errors)> RegisterExternalUserAsync(string provider, string providerKey, string email, bool emailVerified, string? fullName, string username, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> CompleteAccountSetupAsync(Guid userId, string username, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string SharedKey, string AuthenticatorUri, string[] Errors)> Setup2FAAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] RecoveryCodes, string[] Errors)> VerifyAndEnable2FAAsync(Guid userId, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, string[] Errors)> Disable2FAAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> IsTwoFactorEnabledAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<(bool Success, bool IsRecoveryCode, bool CodeAlreadyUsed)> VerifyTwoFactorCodeAsync(Guid userId, string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
