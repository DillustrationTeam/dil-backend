using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var clientId = Guid.NewGuid();
var creatorUserId = Guid.NewGuid();
var creatorProfileId = Guid.NewGuid();
var packageId = Guid.NewGuid();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("commission-" + Guid.NewGuid()).Options);

db.CreatorProfiles.Add(new CreatorProfile
{
    Id = creatorProfileId,
    UserId = creatorUserId,
    DisplayName = "Test creator",
    IsAcceptingOrders = true,
    RateCardJson = JsonSerializer.Serialize(new[]
    {
        new RateCardPackageDto(packageId, "Test package", null, 300m,
        [new(1, "Sketch", 100m), new(2, "Line", 100m), new(3, "Final", 100m)])
    })
});
db.Wallets.Add(new Wallet { UserId = clientId, Balance = 1_000m, Currency = "VND", Status = WalletStatus.Active });
db.Vouchers.Add(new Voucher
{
    VoucherCode = "COMMISSION10",
    DiscountType = DiscountType.Percent,
    DiscountValue = 10m,
    Scope = VoucherScope.Commission,
    StartDate = VoucherCheckService.TodayInVietnam().AddDays(-1),
    EndDate = VoucherCheckService.TodayInVietnam().AddDays(1),
    UsageLimit = 1,
    IsActive = true
});
await db.SaveChangesAsync();

var service = new ArtCommission.Infrastructure.Services.CommissionService(
    db, new WalletService(db), new FakeWatermarkService(), new FakeStorageService(), new VoucherCheckService(db));
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

async Task<(Guid Id, Guid[] Milestones)> Create(string title, string? voucherCode = null)
{
    var created = await service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = creatorProfileId,
        PackageId = packageId,
        LicenseType = "Personal",
        Title = title,
        VoucherCode = voucherCode
    }, clientId);
    var detail = await service.GetCommissionByIdAsync(created.Id, clientId);
    return (created.Id, detail!.Milestones.OrderBy(m => m.Sequence).Select(m => m.Id).ToArray());
}

MemoryStream Upload() => new([1, 2, 3]);

var normal = await Create("normal");
await Rejected(async () => { await service.GetCommissionsAsync("not-a-status", null, clientId); }, "invalid status filter");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "deposit before accept");
await service.RespondCommissionAsync(normal.Id, new RespondCommissionRequest { Action = "Accept" }, creatorUserId);
await Rejected(() => service.SubmitMilestoneWipAsync(normal.Id, normal.Milestones[0], null, Upload(), "image/png", creatorUserId), "submit before deposit");
await service.DepositEscrowAsync(normal.Id, clientId, "Wallet");
await Rejected(() => service.DepositEscrowAsync(normal.Id, clientId, "Wallet"), "duplicate deposit");

for (var i = 0; i < normal.Milestones.Length; i++)
{
    var milestoneId = normal.Milestones[i];
    await Rejected(() => service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId), "approve before submit");
    await service.SubmitMilestoneWipAsync(normal.Id, milestoneId, null, Upload(), "image/png", creatorUserId);
    if (i == 0)
    {
        await service.RequestMilestoneRevisionAsync(normal.Id, milestoneId,
            new RequestRevisionRequest { FeedbackComment = "revise" }, clientId);
        await service.SubmitMilestoneWipAsync(normal.Id, milestoneId, null, Upload(), "image/png", creatorUserId);
    }
    await service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId);
    await Rejected(() => service.ApproveMilestoneAsync(normal.Id, milestoneId, clientId), "duplicate approval");
    Equal((i + 1) * 100m, (await service.GetCommissionByIdAsync(normal.Id, clientId))!.DisbursedAmount, "exact disbursement");
}

var approved = await service.GetCommissionByIdAsync(normal.Id, clientId);
Equal(0m, approved!.EscrowHeldAmount, "escrow exhausted");
Equal(EscrowStatus.Released.ToString(), approved.EscrowStatus, "escrow released");
await service.DeliverFinalWorkAsync(normal.Id, Upload(), "application/zip", "final.zip", creatorUserId);
await service.CompleteCommissionAsync(normal.Id, clientId);
await Rejected(() => service.CompleteCommissionAsync(normal.Id, clientId), "duplicate complete");
await service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 5 }, clientId);
await Rejected(() => service.CreateReviewAsync(normal.Id, new CreateReviewRequest { Rating = 4 }, clientId), "duplicate review");

var negotiated = await Create("negotiated");
await service.RespondCommissionAsync(negotiated.Id,
    new RespondCommissionRequest { Action = "Negotiate", NegotiatePrice = 450m }, creatorUserId);
Equal(450m, (await service.GetCommissionByIdAsync(negotiated.Id, clientId))!.Milestones.Sum(m => m.Price), "negotiated milestone total");
await service.RespondToCounterofferAsync(negotiated.Id, true, clientId);

var cancelled = await Create("cancelled");
await service.CancelCommissionAsync(cancelled.Id, new CancelWithPolicyRequest { CancellationReason = "changed mind" }, clientId);
await Rejected(() => service.DepositEscrowAsync(cancelled.Id, clientId, "Wallet"), "deposit cancelled");

var disputed = await Create("disputed");
await service.CreateDisputeAsync(disputed.Id, new CreateDisputeRequest { Reason = "test" }, clientId);
await Rejected(() => service.CreateDisputeAsync(disputed.Id, new CreateDisputeRequest { Reason = "again" }, creatorUserId), "duplicate dispute");

var discounted = await Create("discounted", "COMMISSION10");
Equal(270m, (await service.GetCommissionByIdAsync(discounted.Id, clientId))!.FinalPrice, "voucher discount");
await service.RespondCommissionAsync(discounted.Id, new RespondCommissionRequest { Action = "Accept" }, creatorUserId);
await service.DepositEscrowAsync(discounted.Id, clientId, "Wallet");
Equal(1, await db.VoucherRedemptions.CountAsync(), "voucher redeemed once");
await Rejected(() => service.CancelCommissionAsync(discounted.Id,
    new CancelWithPolicyRequest { CancellationReason = "too late" }, clientId), "funded cancellation");

Console.WriteLine($"Passed {checks} commission workflow checks.");

sealed class FakeWatermarkService : IWatermarkService
{
    public async Task<Stream> ApplyWatermarkAsync(Stream imageStream, string watermarkText, CancellationToken cancellationToken = default)
    {
        var output = new MemoryStream();
        await imageStream.CopyToAsync(output, cancellationToken);
        output.Position = 0;
        return output;
    }
}

sealed class FakeStorageService : IStorageService
{
    public Task<string> UploadPublicAsync(Stream stream, string key, string contentType, CancellationToken ct = default) => Task.FromResult(key);
    public Task<string> UploadPrivateAsync(Stream stream, string key, string contentType, CancellationToken ct = default) => Task.FromResult(key);
    public string GeneratePresignedDownloadUrl(string privateKey, TimeSpan expiry) => $"https://example.test/{privateKey}";
}
