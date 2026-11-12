using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using System.IO.Compression;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Application.Commission.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CommissionService = ArtCommission.Infrastructure.Services.CommissionService;

internal static class TieredEscrowAndAutoAcceptChecks
{
    public static async Task RunAsync()
    {
        var clientId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("tiered-auto-" + Guid.NewGuid()).Options);
        db.Roles.Add(new IdentityRole<Guid>("Creator") { Id = roleId });
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = creatorId, RoleId = roleId });

        var smallPkgId = Guid.NewGuid();
        var largePkgId = Guid.NewGuid();
        var profile = new CreatorProfile
        {
            UserId = creatorId,
            DisplayName = "Tiered Creator",
            IsAcceptingOrders = true,
            RateCardJson = JsonSerializer.Serialize(new[]
            {
                new RateCardPackageDto(smallPkgId, "Small", null, 600_000m,
                [
                    new(1, "Sketch", 200_000m),
                    new(2, "Color", 200_000m),
                    new(3, "Final", 200_000m)
                ]),
                new RateCardPackageDto(largePkgId, "Large", null, 3_000_000m,
                [
                    new(1, "Sketch", 900_000m),
                    new(2, "Lineart", 900_000m),
                    new(3, "Final", 1_200_000m)
                ])
            })
        };
        db.CreatorProfiles.Add(profile);
        db.Wallets.Add(new Wallet { UserId = clientId, Balance = 10_000_000m });
        var voucher = new Voucher
        {
            VoucherCode = "STAGE10",
            DiscountType = DiscountType.Percent,
            DiscountValue = 10m,
            MinOrderAmount = 1_000_000m,
            Scope = VoucherScope.Commission,
            IsActive = true,
            StartDate = VoucherCheckService.TodayInVietnam().AddDays(-1),
            EndDate = VoucherCheckService.TodayInVietnam().AddDays(30)
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var notifications = new TestNotificationPublisher();
        var storage = new TestStorage();
        var service = new CommissionService(db, new WalletService(db), new TestWatermark(), storage, new VoucherCheckService(db), notifications);
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
            throw new Exception($"{name}: invalid transition was accepted");
        }

        async Task RejectedInput(Func<Task> action, string name)
        {
            try { await action(); }
            catch (ArgumentException) { checks++; return; }
            throw new Exception($"{name}: invalid file was accepted");
        }

        // 1) Small order (< 2M): 100% upfront escrow + PartialReleased flow
        var small = await service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = smallPkgId, Title = "Small order"
        }, clientId);
        await service.RespondCommissionAsync(small.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
        await service.AcceptWorkroomTermsAsync(small.Id, clientId);
        await service.AcceptWorkroomTermsAsync(small.Id, creatorId);
        var smallDetail = await service.GetCommissionByIdAsync(small.Id, clientId);
        Equal(false, smallDetail!.IsStagedEscrow, "small order is not staged");
        Equal(600_000m, smallDetail.NextEscrowDepositAmount, "small order requires 100% upfront");
        var smallAfterDep = await service.DepositEscrowAsync(small.Id, clientId, "Wallet");
        Equal(600_000m, smallAfterDep.EscrowHeldAmount, "small order holds full 600k");
        Equal("Deposited", smallAfterDep.EscrowStatus, "small order deposited");

        // 2) Large order (>= 2M) with voucher: staged escrow per milestone
        var large = await service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = largePkgId, Title = "Large order", VoucherCode = "STAGE10"
        }, clientId);
        await service.RespondCommissionAsync(large.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
        await service.AcceptWorkroomTermsAsync(large.Id, clientId);
        await service.AcceptWorkroomTermsAsync(large.Id, creatorId);
        var largeDetail = await service.GetCommissionByIdAsync(large.Id, clientId);
        Equal(true, largeDetail!.IsStagedEscrow, "large order is staged");
        Equal(810_000m, largeDetail.NextEscrowDepositAmount, "large stage 1 next deposit matches discounted stage 1 (810k)");

        var walletBefore = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == clientId)).Balance;
        var afterStage1Dep = await service.DepositEscrowAsync(large.Id, clientId, "Wallet");
        var walletAfterStage1 = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == clientId)).Balance;
        Equal(810_000m, walletBefore - walletAfterStage1, "client wallet only debited 810k for stage 1");
        Equal(810_000m, afterStage1Dep.EscrowHeldAmount, "staged deposit holds stage 1 amount");
        Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucher.Id)).UsedCount, "voucher used once on stage 1");

        var m1 = largeDetail.Milestones.Single(m => m.Sequence == 1);
        var m2 = largeDetail.Milestones.Single(m => m.Sequence == 2);
        await service.SubmitMilestoneWipAsync(large.Id, m1.Id, "s1", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        await service.ApproveMilestoneAsync(large.Id, m1.Id, clientId);

        var afterApprove1 = await service.GetCommissionByIdAsync(large.Id, clientId);
        Equal("Pending", afterApprove1!.EscrowStatus, "staged order resets to Pending awaiting stage 2 funding");
        Equal(0m, afterApprove1.EscrowHeldAmount, "staged order held amount becomes 0 between stages");
        Equal(810_000m, afterApprove1.NextEscrowDepositAmount, "stage 2 next deposit is 810k");
        Equal(2, afterApprove1.CurrentStage, "current stage advances to 2");

        await Rejected(() => service.SubmitMilestoneWipAsync(large.Id, m2.Id, "s2", new MemoryStream([1, 2, 3]), "image/png", creatorId),
            "creator cannot submit stage 2 before client funds stage 2");

        var afterStage2Dep = await service.DepositEscrowAsync(large.Id, clientId, "Wallet");
        Equal("PartialReleased", afterStage2Dep.EscrowStatus, "stage 2 deposit transitions to PartialReleased");
        Equal(810_000m, afterStage2Dep.EscrowHeldAmount, "stage 2 deposit holds 810k");
        Equal(1, (await db.Vouchers.AsNoTracking().SingleAsync(v => v.Id == voucher.Id)).UsedCount, "voucher is NOT double-counted on stage 2 deposit");
        var holdLedgerKeys = await db.WalletTransactions.AsNoTracking()
            .Where(t => t.Type == WalletTransactionType.EscrowHold && (t.RefId == m1.Id || t.RefId == m2.Id))
            .Select(t => new { t.RefType, t.RefId, t.Type })
            .ToListAsync();
        Equal(2, holdLedgerKeys.Distinct().Count(), "staged escrow holds use distinct UX_WalletTransaction_Ref keys per milestone");

        // 3) 72h Hangfire Milestone Auto-Accept (including boundary & stage 3 full completion)
        await service.SubmitMilestoneWipAsync(large.Id, m2.Id, "s2", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        var m2Entity = await db.Milestones.SingleAsync(m => m.Id == m2.Id);
        m2Entity.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-71).AddMinutes(-59);
        await db.SaveChangesAsync();

        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        Equal(MilestoneStatus.Submitted, (await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == m2.Id)).Status,
            "milestone at 71h59m is NOT auto-accepted");

        m2Entity.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-73);
        await db.SaveChangesAsync();

        var creatorBalanceBeforeAuto = (await db.Wallets.AsNoTracking().FirstOrDefaultAsync(w => w.UserId == creatorId))?.Balance ?? 0m;
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        var creatorBalanceAfterAuto = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == creatorId)).Balance;
        Equal(MilestoneStatus.Approved, (await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == m2.Id)).Status,
            "milestone submitted 73h ago IS auto-accepted");
        Equal(810_000m, creatorBalanceAfterAuto - creatorBalanceBeforeAuto, "creator receives stage 2 payout on 72h auto-accept");
        Equal(true, notifications.Items.Any(n => n.UserId == clientId && n.DedupKey == $"MilestoneAutoAccepted:{m2.Id}:Client"),
            "72h auto-accept notifies client");
        Equal(true, notifications.Items.Any(n => n.UserId == creatorId && n.DedupKey == $"MilestoneAutoAccepted:{m2.Id}:Creator"),
            "72h auto-accept notifies creator");

        var beforeStage3Detail = await service.GetCommissionByIdAsync(large.Id, clientId);
        Equal("Pending", beforeStage3Detail!.EscrowStatus, "after auto-accept of stage 2, staged order waits for stage 3 funding");
        Equal(1_080_000m, beforeStage3Detail.NextEscrowDepositAmount, "stage 3 next deposit is discounted 1.08M");

        // Idempotent re-run does not double pay or emit duplicate state changes
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        Equal(creatorBalanceAfterAuto, (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == creatorId)).Balance,
            "auto-accept is idempotent");

        // Add-on proposal blocked between staged funding phases
        var addonService = new AddonService(db, new WalletService(db));
        var extra = await addonService.SaveAsync(new SaveCreatorAddonRequest
        {
            Title = "Extra detail", Price = 120_000m, AllowedMilestone = "Final", PackageId = largePkgId
        }, creatorId, default);
        await Rejected(() => addonService.ProposeAsync(large.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, clientId, default),
            "cannot propose add-on while staged order awaits funding");

        // Fund stage 3, add-on during funded stage 3, auto-accept stage 3 -> Released + Complete
        var m3 = largeDetail.Milestones.Single(m => m.Sequence == 3);
        await service.DepositEscrowAsync(large.Id, clientId, "Wallet");
        var proposedAddon = await addonService.ProposeAsync(large.Id, new ProposeAddonRequest { CreatorAddonId = extra.Id }, clientId, default);
        await addonService.RespondAsync(large.Id, proposedAddon.Id, new RespondAddonRequest("Accept"), clientId, default);
        var largeAfterAddon = await service.GetCommissionByIdAsync(large.Id, clientId);
        Equal(1_200_000m, largeAfterAddon!.EscrowHeldAmount, "stage 3 + add-on increases held amount to 1.2M");

        await service.SubmitMilestoneWipAsync(large.Id, m3.Id, "s3", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        var m3Entity = await db.Milestones.SingleAsync(m => m.Id == m3.Id);
        m3Entity.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-72).AddMinutes(-1);
        await db.SaveChangesAsync();
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();

        var afterFinalAuto = await service.GetCommissionByIdAsync(large.Id, clientId);
        Equal("Released", afterFinalAuto!.EscrowStatus, "final stage 72h auto-accept releases escrow cleanly");
        Equal(0m, afterFinalAuto.EscrowHeldAmount, "no escrow held after final stage release");
        Equal(2_820_000m, afterFinalAuto.DisbursedAmount, "all staged payouts + add-on disbursed (2.82M)");

        MemoryStream MakeValidPng() { using var img = new Image<Rgba32>(2, 2); var ms = new MemoryStream(); img.SaveAsPng(ms); ms.Position = 0; return ms; }
        MemoryStream MakeValidPsd() => new([0x38, 0x42, 0x50, 0x53, 0x00, 0x01, 0, 0, 0, 0, 0, 0]);
        MemoryStream MakeZipWithEntry(string entryName, byte[] content)
        {
            var ms = new MemoryStream();
            using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = zip.CreateEntry(entryName);
                using var s = entry.Open();
                s.Write(content, 0, content.Length);
            }
            ms.Position = 0;
            return ms;
        }

        // Security checks on deliverable uploads before accepting valid multi-file bundle:
        await RejectedInput(() => service.DeliverFinalWorkAsync(large.Id, new MemoryStream([0x4D, 0x5A, 0x90, 0x00]), "image/png", "malware.png", creatorId),
            "reject MZ executable disguised as PNG");
        await RejectedInput(() => service.DeliverFinalWorkAsync(large.Id, new MemoryStream([0x4D, 0x5A, 0x90, 0x00]), "application/octet-stream", "fake.psd", creatorId),
            "reject MZ executable disguised as PSD");
        await RejectedInput(() => service.DeliverFinalWorkAsync(large.Id, MakeZipWithEntry("payload.exe", [0x4D, 0x5A]), "application/zip", "bundle.zip", creatorId),
            "reject ZIP containing executable");
        await RejectedInput(() => service.DeliverFinalWorkAsync(large.Id, MakeZipWithEntry("../escape.png", [1, 2, 3]), "application/zip", "slip.zip", creatorId),
            "reject Zip Slip path traversal");
        await RejectedInput(() => service.DeliverFinalWorkAsync(large.Id, new MemoryStream([0x3C, 0x73, 0x76, 0x67]), "image/svg+xml", "script.svg", creatorId),
            "reject disallowed SVG extension");

        // Multi-file delivery keeps individual files (no forced zip) so Creator can freely add/remove before completion:
        await service.DeliverFinalWorkAsync(large.Id,
        [
            new DeliverableUploadItem(MakeValidPsd(), "application/octet-stream", "master.psd", 12),
            new DeliverableUploadItem(MakeValidPng(), "image/png", "preview.png", 68)
        ], creatorId);
        var initialManifest = await service.GetDeliverablesManifestAsync(large.Id, clientId);
        Equal(2, initialManifest.Files.Count, "multi-file delivery preserves 2 individual files");
        var psdFile = initialManifest.Files.Single(f => f.Name == "master.psd");
        var psdDownload = await service.GetFinalDownloadUrlAsync(large.Id, clientId, psdFile.Id);
        Equal(true, psdDownload.DownloadUrl.Contains("master.psd"), "individual file download targets master.psd directly without zip");

        var zipAllDownload = await service.GetFinalDownloadUrlAsync(large.Id, clientId);
        var zipBundleKey = zipAllDownload.DownloadUrl.Replace("https://example.test/", "");
        using (var savedZip = new ZipArchive(new MemoryStream(storage.UploadedPrivate[zipBundleKey]), ZipArchiveMode.Read))
        {
            Equal(2, savedZip.Entries.Count, "on-demand download-all zip contains both files");
        }

        // Creator deletes preview.png and appends revised-final.png before completion; stale client snapshot is blocked!
        var previewFile = initialManifest.Files.Single(f => f.Name == "preview.png");
        var afterDeleteManifest = await service.DeleteDeliverableFileAsync(large.Id, previewFile.Id, creatorId);
        Equal(1, afterDeleteManifest.Files.Count, "creator can remove individual deliverable file");
        await Rejected(() => service.DeleteDeliverableFileAsync(large.Id, psdFile.Id, creatorId),
            "creator cannot delete the last remaining deliverable file");

        var updatedDelivery = await service.DeliverFinalWorkAsync(large.Id, MakeValidPng(), "image/png", "revised-final.png", creatorId);
        Equal("Delivered", updatedDelivery.Status, "creator can append files before completion");
        var latestManifest = await service.GetDeliverablesManifestAsync(large.Id, clientId);
        Equal(2, latestManifest.Files.Count, "appended file appears in manifest");

        await Rejected(() => service.CompleteCommissionAsync(large.Id, clientId, initialManifest.Version),
            "anti-bait-and-switch blocks completion with stale manifest version");
        var completedLarge = await service.CompleteCommissionAsync(large.Id, clientId, latestManifest.Version);
        Equal("Completed", completedLarge.Status, "staged large order completes with latest manifest version");
        await Rejected(() => service.DeliverFinalWorkAsync(large.Id, MakeValidPng(), "image/png", "late.png", creatorId),
            "creator cannot upload deliverables after client completes");
        await Rejected(() => service.DeleteDeliverableFileAsync(large.Id, psdFile.Id, creatorId),
            "creator cannot delete deliverables after client completes");

        // 4) Guard checks on 72h auto-accept: pending cancellation, disputed, and revision-requested
        var smallM1 = smallDetail.Milestones.Single(m => m.Sequence == 1);
        await service.SubmitMilestoneWipAsync(small.Id, smallM1.Id, "s1", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        var smallM1Entity = await db.Milestones.SingleAsync(m => m.Id == smallM1.Id);
        smallM1Entity.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-80);
        var smallEntity = await db.Commissions.SingleAsync(c => c.Id == small.Id);
        smallEntity.CancellationRequestStatus = "Pending";
        await db.SaveChangesAsync();
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        Equal(MilestoneStatus.Submitted, (await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == smallM1.Id)).Status,
            "pending cancellation blocks 72h auto-accept");

        smallEntity.CancellationRequestStatus = null;
        await db.SaveChangesAsync();
        await service.RequestMilestoneRevisionAsync(small.Id, smallM1.Id, new RequestRevisionRequest { FeedbackComment = "Fix shading" }, clientId);
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        Equal(MilestoneStatus.RevisionRequested, (await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == smallM1.Id)).Status,
            "revision-requested milestone is NOT auto-accepted");

        await service.SubmitMilestoneWipAsync(small.Id, smallM1.Id, "s1-rev", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        smallM1Entity = await db.Milestones.SingleAsync(m => m.Id == smallM1.Id);
        smallM1Entity.SubmittedAt = DateTimeOffset.UtcNow.AddHours(-80);
        await db.SaveChangesAsync();
        await service.CreateDisputeAsync(small.Id, new CreateDisputeRequest { Reason = "Dispute blocks auto-accept" }, clientId);
        await service.ProcessMilestoneAutoAcceptDeadlinesAsync();
        Equal(MilestoneStatus.Submitted, (await db.Milestones.AsNoTracking().SingleAsync(m => m.Id == smallM1.Id)).Status,
            "disputed order blocks 72h auto-accept");

        // 5) Cancellation mid-flight on Staged order refunds ONLY currently held stage & Negotiation crosses 2M threshold
        var negotiated = await service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = smallPkgId, Title = "Negotiated over threshold"
        }, clientId);
        await service.RespondCommissionAsync(negotiated.Id, new RespondCommissionRequest { Action = "Negotiate", NegotiatePrice = 2_400_000m }, creatorId);
        await service.RespondToCounterofferAsync(negotiated.Id, true, clientId);
        await service.AcceptWorkroomTermsAsync(negotiated.Id, clientId);
        await service.AcceptWorkroomTermsAsync(negotiated.Id, creatorId);

        var negDetail = await service.GetCommissionByIdAsync(negotiated.Id, clientId);
        Equal(true, negDetail!.IsStagedEscrow, "negotiated order >= 2M becomes staged escrow");
        Equal(200_000m, negDetail.NextEscrowDepositAmount, "negotiated staged order stage 1 deposits only stage 1 (200k)");

        var clientBalBeforeNeg = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == clientId)).Balance;
        await service.DepositEscrowAsync(negotiated.Id, clientId, "Wallet");
        var negM1 = negDetail.Milestones.Single(m => m.Sequence == 1);
        await service.SubmitMilestoneWipAsync(negotiated.Id, negM1.Id, "neg-s1", new MemoryStream([1, 2, 3]), "image/png", creatorId);
        await service.ApproveMilestoneAsync(negotiated.Id, negM1.Id, clientId);
        await service.DepositEscrowAsync(negotiated.Id, clientId, "Wallet"); // funds stage 2 (200k)

        // Creator initiates cancel and after 48h client doesn't respond -> refunds ONLY stage 2 (200k), stage 1 (200k) stays disbursed
        await service.CancelCommissionAsync(negotiated.Id, new CancelWithPolicyRequest { CancellationReason = "Unable to continue" }, creatorId);
        var negEntity = await db.Commissions.SingleAsync(c => c.Id == negotiated.Id);
        negEntity.CancellationRequestExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();
        await service.ProcessCancellationDeadlinesAsync();

        var clientBalAfterCancel = (await db.Wallets.AsNoTracking().SingleAsync(w => w.UserId == clientId)).Balance;
        var cancelledNeg = await service.GetCommissionByIdAsync(negotiated.Id, clientId);
        Equal("Cancelled", cancelledNeg!.Status, "negotiated staged order cancelled");
        Equal("Refunded", cancelledNeg.EscrowStatus, "stage 2 held funds refunded");
        Equal(200_000m, clientBalBeforeNeg - clientBalAfterCancel, "net client spend equals only approved stage 1 (200k)");

        // 6) Per-package slot limits: limited (MaxSlots = 1) vs unlimited (MaxSlots = null)
        var limitedPkgId = Guid.NewGuid();
        var unlimitedPkgId = Guid.NewGuid();
        profile.RateCardJson = JsonSerializer.Serialize(new[]
        {
            new RateCardPackageDto(limitedPkgId, "Limited Slot", null, 500_000m,
                [new(1, "Final", 500_000m)], MaxSlots: 1),
            new RateCardPackageDto(unlimitedPkgId, "Unlimited Slot", null, 300_000m,
                [new(1, "Final", 300_000m)], MaxSlots: null)
        });
        await db.SaveChangesAsync();

        var rateHandler = new GetCreatorRateCardQueryHandler(db);
        var rateInit = await rateHandler.Handle(new GetCreatorRateCardQuery(profile.Id), default);
        Equal(1, rateInit.Data!.Single(x => x.Id == limitedPkgId).RemainingSlots, "limited package starts with 1 remaining slot");
        Equal((int?)null, rateInit.Data!.Single(x => x.Id == unlimitedPkgId).RemainingSlots, "unlimited package has null remaining slots");

        var slotReq1 = await service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = limitedPkgId, Title = "Slot 1"
        }, clientId);
        var slotReq2 = await service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = limitedPkgId, Title = "Slot 2 competing"
        }, clientId);

        await service.RespondCommissionAsync(slotReq1.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
        var rateFull = await rateHandler.Handle(new GetCreatorRateCardQuery(profile.Id), default);
        Equal(0, rateFull.Data!.Single(x => x.Id == limitedPkgId).RemainingSlots, "accepting order fills limited package slot (0 left)");

        await RejectedInput(() => service.CreateCommissionAsync(new CreateCommissionRequest
        {
            CreatorId = profile.Id, PackageId = limitedPkgId, Title = "Slot 3 blocked"
        }, clientId), "new booking rejected when package has 0 remaining slots");
        await Rejected(() => service.RespondCommissionAsync(slotReq2.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId),
            "creator cannot accept competing request when limited package is full");

        // Cancelling slotReq1 frees the slot automatically:
        await service.AcceptWorkroomTermsAsync(slotReq1.Id, clientId);
        await service.AcceptWorkroomTermsAsync(slotReq1.Id, creatorId);
        await service.CancelCommissionAsync(slotReq1.Id, new CancelWithPolicyRequest { CancellationReason = "No longer needed" }, clientId);
        await service.RespondCancellationRequestAsync(slotReq1.Id, true, creatorId);

        var rateAfterCancel = await rateHandler.Handle(new GetCreatorRateCardQuery(profile.Id), default);
        Equal(1, rateAfterCancel.Data!.Single(x => x.Id == limitedPkgId).RemainingSlots, "cancelling order frees package slot automatically");
        await service.RespondCommissionAsync(slotReq2.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);

        // Unlimited package allows multiple simultaneous InProgress orders:
        var unlim1 = await service.CreateCommissionAsync(new CreateCommissionRequest { CreatorId = profile.Id, PackageId = unlimitedPkgId, Title = "U1" }, clientId);
        var unlim2 = await service.CreateCommissionAsync(new CreateCommissionRequest { CreatorId = profile.Id, PackageId = unlimitedPkgId, Title = "U2" }, clientId);
        await service.RespondCommissionAsync(unlim1.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
        await service.RespondCommissionAsync(unlim2.Id, new RespondCommissionRequest { Action = "Accept" }, creatorId);
        var rateUnlim = await rateHandler.Handle(new GetCreatorRateCardQuery(profile.Id), default);
        Equal(2, rateUnlim.Data!.Single(x => x.Id == unlimitedPkgId).ActiveOrderCount, "unlimited package tracks active orders without blocking");

        Console.WriteLine($"Passed {checks} tiered escrow & 72h auto-accept checks.");
    }
}
