using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Commission;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.API.Common;

internal static class DevelopmentDemoSeeder
{
    internal const string ClientEmail = "client.demo@dillustration.test";
    internal const string CreatorEmail = "creator.demo@dillustration.test";
    internal const string Password = "Demo@123456";

    public static async Task SeedAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken = default)
    {
        foreach (var roleName in new[] { "Client", "Creator" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }

        var client = await EnsureUserAsync(userManager, ClientEmail, "Demo Client", ["Client"]);
        var creator = await EnsureUserAsync(userManager, CreatorEmail, "Linh Nguyen", ["Client", "Creator"]);

        var creatorProfile = await dbContext.CreatorProfiles
            .FirstOrDefaultAsync(profile => profile.UserId == creator.Id, cancellationToken);

        if (creatorProfile is null)
        {
            creatorProfile = new CreatorProfile
            {
                UserId = creator.Id,
                DisplayName = "Linh Nguyen",
                Headline = "Digital illustrator · Character art & key visuals",
                Bio = "Demo creator profile for the commission workroom.",
                Specialties = "Character illustration, key visual, anime",
                Location = "Ho Chi Minh City",
                IsAcceptingOrders = true,
                IsApproved = true,
                RatingAverage = 4.9m,
                RatingCount = 28
            };
            dbContext.CreatorProfiles.Add(creatorProfile);
        }

        creatorProfile.RateCardJson ??= """
            [
              {"id":"portrait","name":"Chân dung bán thân","description":"1 nhân vật, nền đơn giản, PNG độ phân giải cao.","price":350000,"milestones":[{"sequence":1,"title":"Phác thảo","price":105000},{"sequence":2,"title":"Lineart & màu cơ bản","price":140000},{"sequence":3,"title":"Hoàn thiện","price":105000}]},
              {"id":"full-body","name":"Minh hoạ toàn thân","description":"1 nhân vật toàn thân, nền gradient hoặc đạo cụ đơn giản.","price":650000,"milestones":[{"sequence":1,"title":"Phác thảo bố cục","price":195000},{"sequence":2,"title":"Lineart & màu","price":260000},{"sequence":3,"title":"Hoàn thiện & bàn giao","price":195000}]},
              {"id":"key-visual","name":"Key visual nhân vật","description":"Key visual chi tiết với nền minh hoạ và hiệu ứng ánh sáng.","price":1200000,"milestones":[{"sequence":1,"title":"Concept & thumbnail","price":240000},{"sequence":2,"title":"Bản phác thảo chi tiết","price":360000},{"sequence":3,"title":"Render hoàn thiện","price":600000}]}
            ]
            """;

        await EnsureWalletAsync(dbContext, client.Id, 320_000m, 180_000m, cancellationToken);
        await EnsureWalletAsync(dbContext, creator.Id, 0m, 0m, cancellationToken);

        var demoWorkroom = await dbContext.Commissions.FirstOrDefaultAsync(
            commission => commission.ClientId == client.Id &&
                          commission.Title == "Character illustration — Demo workroom",
            cancellationToken);

        if (demoWorkroom is null)
        {
            dbContext.Commissions.Add(new Commission
            {
                Title = "Character illustration — Demo workroom",
                Description = "Minh hoạ nhân vật full-body, phong cách anime mềm. Bảng màu tím và xanh, nền tối giản.",
                ClientId = client.Id,
                CreatorId = creatorProfile.Id,
                TotalPrice = 180_000m,
                FinalPrice = 180_000m,
                EscrowHeldAmount = 180_000m,
                DisbursedAmount = 0m,
                EscrowStatus = EscrowStatus.Deposited,
                Status = CommissionStatus.InProgress,
                CurrentStage = 1,
                DeadlineAt = DateTimeOffset.UtcNow.AddDays(10),
                Milestones =
                [
                    new Milestone
                    {
                        Sequence = 1,
                        Title = "Bản phác thảo nhân vật",
                        Price = 60_000m,
                        Status = MilestoneStatus.Submitted,
                        SubmittedAt = DateTimeOffset.UtcNow.AddHours(-6),
                        CreatorNote = "Mình đã hoàn thiện bố cục và pose chính. Bạn xem giúp phần gương mặt nhé.",
                        WipPreviewUrl = "https://images.unsplash.com/photo-1618005198919-d3d4b5a92ead?auto=format&fit=crop&w=1200&q=80",
                        WatermarkedUrl = "https://images.unsplash.com/photo-1618005198919-d3d4b5a92ead?auto=format&fit=crop&w=1200&q=80"
                    },
                    new Milestone
                    {
                        Sequence = 2,
                        Title = "Lineart và màu cơ bản",
                        Price = 70_000m,
                        Status = MilestoneStatus.Pending
                    },
                    new Milestone
                    {
                        Sequence = 3,
                        Title = "Hoàn thiện và bàn giao",
                        Price = 50_000m,
                        Status = MilestoneStatus.Pending
                    }
                ]
            });
        }
        else if (demoWorkroom.CreatorId != creatorProfile.Id)
        {
            // Commission ownership is based on CreatorProfile.Id, not the user's Id.
            demoWorkroom.CreatorId = creatorProfile.Id;
            demoWorkroom.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var hasDemoArtwork = await dbContext.Artworks.AnyAsync(
            artwork => artwork.CreatorProfileId == creatorProfile.Id &&
                       artwork.Title == "Moonlit Character Portrait",
            cancellationToken);

        if (!hasDemoArtwork)
        {
            dbContext.Artworks.Add(new Artwork
            {
                CreatorProfileId = creatorProfile.Id,
                Title = "Moonlit Character Portrait",
                Description = "Portrait phong cách anime với ánh sáng xanh tím. Tác phẩm mẫu của Linh Nguyen cho khách hàng tham khảo trước khi gửi yêu cầu đặt vẽ.",
                ImageUrl = "https://images.unsplash.com/photo-1618005198919-d3d4b5a92ead?auto=format&fit=crop&w=1200&q=85",
                ThumbnailUrl = "https://images.unsplash.com/photo-1618005198919-d3d4b5a92ead?auto=format&fit=crop&w=600&q=80",
                IsAiGenerated = false,
                ModerationStatus = "Approved",
                ViewCount = 126
            });
        }

        var hasIncomingRequest = await dbContext.Commissions.AnyAsync(
            commission => commission.ClientId == client.Id && commission.CreatorId == creatorProfile.Id &&
                          commission.Title == "Portrait commission — Awaiting creator response",
            cancellationToken);

        if (!hasIncomingRequest)
        {
            dbContext.Commissions.Add(new Commission
            {
                Title = "Portrait commission — Awaiting creator response",
                Description = "Minh hoạ chân dung bán thân phong cách anime. Khách hàng đã gửi brief và đang chờ Creator phản hồi.",
                ClientId = client.Id,
                CreatorId = creatorProfile.Id,
                TotalPrice = 450_000m,
                FinalPrice = 450_000m,
                EscrowStatus = EscrowStatus.Pending,
                Status = CommissionStatus.PendingAcceptance,
                CurrentStage = 1,
                DeadlineAt = DateTimeOffset.UtcNow.AddDays(14),
                Milestones =
                [
                    new Milestone { Sequence = 1, Title = "Bản phác thảo", Price = 150_000m, Status = MilestoneStatus.Pending },
                    new Milestone { Sequence = 2, Title = "Tô màu và hoàn thiện", Price = 300_000m, Status = MilestoneStatus.Pending }
                ]
            });
        }
        // Demo: Creator accepted → system now in escrow phase (client must deposit)
        var hasEscrowPendingDemo = await dbContext.Commissions.AnyAsync(
            commission => commission.ClientId == client.Id && commission.CreatorId == creatorProfile.Id &&
                        commission.Title == "Key visual — Creator accepted, awaiting escrow",
            cancellationToken);

        if (!hasEscrowPendingDemo)
        {
            dbContext.Commissions.Add(new Commission
            {
                Title = "Key visual — Creator accepted, awaiting escrow",
                Description = "Key visual nhân vật chính cho dự án game indie. Creator đã xác nhận yêu cầu, Client cần ký quỹ để bắt đầu sản xuất.",
                ClientId = client.Id,
                CreatorId = creatorProfile.Id,
                TotalPrice = 1_200_000m,
                FinalPrice = 1_200_000m,
                DiscountAmount = 0m,
                EscrowHeldAmount = 0m,
                DisbursedAmount = 0m,
                EscrowStatus = EscrowStatus.Pending,
                Status = CommissionStatus.InProgress,
                CurrentStage = 1,
                DeadlineAt = DateTimeOffset.UtcNow.AddDays(21),
                Milestones =
                [
                    new Milestone { Sequence = 1, Title = "Concept & thumbnail", Price = 240_000m, Status = MilestoneStatus.Pending },
                    new Milestone { Sequence = 2, Title = "Bản phác thảo chi tiết", Price = 360_000m, Status = MilestoneStatus.Pending },
                    new Milestone { Sequence = 3, Title = "Render hoàn thiện", Price = 600_000m, Status = MilestoneStatus.Pending }
                ]
            });
        }


        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string fullName,
        string[] roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                UserName = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsVerified = true
            };

            var result = await userManager.CreateAsync(user, Password);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Could not create demo user {email}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }

        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
                await userManager.AddToRoleAsync(user, role);
        }

        return user;
    }

    private static async Task EnsureWalletAsync(
        AppDbContext dbContext,
        Guid userId,
        decimal balance,
        decimal lockedBalance,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Wallets.AnyAsync(wallet => wallet.UserId == userId, cancellationToken))
            return;

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = balance,
            LockedBalance = lockedBalance
        };
        dbContext.Wallets.Add(wallet);

        if (balance > 0)
        {
            dbContext.WalletTransactions.Add(new WalletTransaction
            {
                WalletId = wallet.Id,
                Type = WalletTransactionType.Deposit,
                Direction = WalletTransactionDirection.In,
                Amount = balance + lockedBalance,
                BalanceAfter = balance,
                LockedBalanceAfter = lockedBalance,
                RefType = "DemoSeed",
                Note = "Demo balance for the seeded workroom."
            });
        }
    }
}
