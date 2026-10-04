using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Application.Payment.Common;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ArtCommission.Infrastructure.Persistence.Seed;

/// <summary>
/// Sinh dữ liệu mẫu cho môi trường Development — gọi từ Program.cs, có guard IsDevelopment().
/// Idempotent theo từng bước (get-or-create ở mỗi bước, không chỉ 1 guard tổng) — an toàn kể cả khi
/// lần chạy trước bị dừng giữa chừng (vd DB thiếu cột do model/migration lệch nhau).
/// KHÔNG dùng trong Production.
/// </summary>
public static class DevDataSeeder
{
    private const string SeedPassword = "Seed@123456"; // thoả policy: RequiredLength=6, không cần chữ hoa/số/ký tự đặc biệt
    private const decimal InitialWalletBalance = 5_000_000m;

    private const string Client1Email = "client1@seed.dillustration.art";
    private const string Client2Email = "client2@seed.dillustration.art";
    private const string ArtistEmail = "artist1@seed.dillustration.art";
    private const string AdminEmail = "admin@seed.dillustration.art";
    private const string ModeratorEmail = "moderator@seed.dillustration.art";

    public static async Task SeedSampleDataAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<IApplicationDbContext>();
        var walletService = services.GetRequiredService<IWalletService>();

        // 1) Get-or-create 5 tài khoản mẫu + gán role
        var client1 = await GetOrCreateUserAsync(userManager, logger, Client1Email, "Nguyễn Văn An", [UserRoleNames.Client]);
        var client2 = await GetOrCreateUserAsync(userManager, logger, Client2Email, "Trần Thị Bình", [UserRoleNames.Client]);
        var artist = await GetOrCreateUserAsync(userManager, logger, ArtistEmail, "Lê Minh Châu", [UserRoleNames.Client, UserRoleNames.Creator]);
        var admin = await GetOrCreateUserAsync(userManager, logger, AdminEmail, "Phạm Quốc Dũng", [UserRoleNames.Administrator]);
        var moderator = await GetOrCreateUserAsync(userManager, logger, ModeratorEmail, "Hoàng Thị Em", [UserRoleNames.Moderator]);

        var allUsers = new[] { client1, client2, artist, admin, moderator };
        if (allUsers.Any(u => u is null))
        {
            logger.LogWarning("One or more seed users failed to be created — aborting sample data seed.");
            return;
        }

        // 2) Cấp 5,000,000 VND cho mỗi tài khoản — chỉ nạp nếu ví vừa tạo (Balance == 0),
        //    tránh nạp trùng nếu tài khoản đã có từ lần chạy trước.
        foreach (var user in allUsers)
        {
            var wallet = await walletService.GetOrCreateWalletAsync(user!.Id, cancellationToken);
            if (wallet.Balance == 0m)
            {
                await walletService.CreditAsync(
                    wallet, WalletTransactionType.Deposit, InitialWalletBalance,
                    refType: "Seed", refId: null, note: "Seed data initial balance", cancellationToken);
            }
        }

        // 3) Get-or-create CreatorProfile cho Artist
        var creatorProfile = await db.CreatorProfiles.FirstOrDefaultAsync(cp => cp.UserId == artist!.Id, cancellationToken);
        if (creatorProfile is null)
        {
            creatorProfile = new CreatorProfile
            {
                UserId = artist!.Id,
                DisplayName = "Minh Châu Art Studio",
                Headline = "Anime & Fantasy Character Illustrator",
                Bio = "Hoạ sĩ chuyên vẽ nhân vật phong cách anime, fantasy và chibi. Nhận đơn thương mại cho game, light novel, VTuber.",
                Specialties = "Anime, Fantasy, Chibi, Portrait, Watercolor",
                Location = "Hồ Chí Minh City, Vietnam",
                IsAcceptingOrders = true,
                CommissionSlots = 5,
                CompletedOrdersCount = 0,
                IsApproved = true, // seed data: bỏ qua quy trình duyệt thật để artist dùng được ngay
                IsAiVerified = false,
                RatingAverage = 0,
                RatingCount = 0,
                FollowerCount = 0
            };
            db.CreatorProfiles.Add(creatorProfile);
            await db.SaveChangesAsync(cancellationToken); // cần creatorProfile.Id trước khi tạo Artwork
        }

        // 4) Tag vocabulary — get-or-create theo Name (unique index)
        string[] tagNames = ["anime", "fantasy", "chibi", "portrait", "watercolor", "character-design"];
        var tags = new Dictionary<string, Tag>();
        foreach (var name in tagNames)
        {
            var tag = await db.Tags.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);
            if (tag is null)
            {
                tag = new Tag { Name = name, IsAiGenerated = false };
                db.Tags.Add(tag);
            }
            tags[name] = tag;
        }
        await db.SaveChangesAsync(cancellationToken); // cần tag.Id trước khi tạo ArtworkTag

        // 5) Get-or-create 5 Artwork mẫu (khớp theo Title trong cùng CreatorProfile)
        var artworkSeeds = new[]
        {
            new { Title = "Moonlit Sakura Spirit", Style = "Fantasy", Seed = "dil-seed-art-1", Tags = new[] { "fantasy", "character-design", "anime" } },
            new { Title = "Chibi Warrior Girl", Style = "Chibi", Seed = "dil-seed-art-2", Tags = new[] { "chibi", "character-design" } },
            new { Title = "Autumn Portrait Study", Style = "Portrait", Seed = "dil-seed-art-3", Tags = new[] { "portrait", "watercolor" } },
            new { Title = "Cyber Ronin", Style = "Anime", Seed = "dil-seed-art-4", Tags = new[] { "anime", "character-design" } },
            new { Title = "Watercolor Dreamscape", Style = "Watercolor", Seed = "dil-seed-art-5", Tags = new[] { "watercolor", "fantasy" } }
        };

        var artworks = new List<(Artwork Artwork, string[] TagNames)>();
        var newArtworkCount = 0;
        foreach (var a in artworkSeeds)
        {
            var artwork = await db.Artworks.FirstOrDefaultAsync(
                x => x.CreatorProfileId == creatorProfile.Id && x.Title == a.Title, cancellationToken);
            if (artwork is null)
            {
                artwork = new Artwork
                {
                    CreatorProfileId = creatorProfile.Id,
                    Title = a.Title,
                    Description = $"Seed sample artwork — {a.Title}.",
                    ImageUrl = $"https://picsum.photos/seed/{a.Seed}/800/600",
                    ThumbnailUrl = $"https://picsum.photos/seed/{a.Seed}/300/300",
                    Style = a.Style,
                    LikeCount = 0,
                    IsAiGenerated = false,
                    AiDetectionScore = null,
                    ModerationStatus = "Approved",
                    ViewCount = 0
                };
                db.Artworks.Add(artwork);
                newArtworkCount++;
            }
            artworks.Add((artwork, a.Tags));
        }
        await db.SaveChangesAsync(cancellationToken); // cần artwork.Id trước khi tạo ArtworkTag

        // 6) Get-or-create ArtworkTag join rows
        foreach (var (artwork, artworkTagNames) in artworks)
        {
            foreach (var tagName in artworkTagNames)
            {
                var tagId = tags[tagName].Id;
                var exists = await db.ArtworkTags.AnyAsync(
                    at => at.ArtworkId == artwork.Id && at.TagId == tagId, cancellationToken);
                if (!exists)
                {
                    db.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork.Id, TagId = tagId });
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Development sample data ready: 5 users, 5 wallets ({Balance:N0} VND each), 1 CreatorProfile, {NewArtworkCount} new / {TotalArtworkCount} total Artworks, {TagCount} tags.",
            InitialWalletBalance, newArtworkCount, artworks.Count, tags.Count);

        // 7) Get-or-create thêm tài khoản mẫu bổ sung: 8 Client, 5 Creator (kèm CreatorProfile cơ bản), 3 Moderator.
        var extraClientSeeds = new[]
        {
            ("client3@seed.dillustration.art", "Vũ Thị Hoa"),
            ("client4@seed.dillustration.art", "Đặng Văn Khôi"),
            ("client5@seed.dillustration.art", "Bùi Thị Lan"),
            ("client6@seed.dillustration.art", "Ngô Văn Minh"),
            ("client7@seed.dillustration.art", "Đỗ Thị Ngọc"),
            ("client8@seed.dillustration.art", "Phan Thị Oanh"),
            ("client9@seed.dillustration.art", "Lý Văn Phúc"),
            ("client10@seed.dillustration.art", "Trịnh Thị Quỳnh"),
        };

        var extraArtistSeeds = new[]
        {
            ("artist2@seed.dillustration.art", "Đinh Thị Thu", "Thu Dinh Art", "Minh hoạ nhân vật & bìa sách", "Portrait, Book Cover, Digital Painting", "Hà Nội, Vietnam"),
            ("artist3@seed.dillustration.art", "Hồ Văn Tùng", "Tung Ho Illustration", "Concept art & game asset", "Concept Art, Game Asset, Environment", "Đà Nẵng, Vietnam"),
            ("artist4@seed.dillustration.art", "Mai Thị Uyên", "Uyên Mai Studio", "Chibi & sticker nghệ sĩ", "Chibi, Sticker, Cute Art", "Hồ Chí Minh City, Vietnam"),
            ("artist5@seed.dillustration.art", "Dương Văn Vinh", "Vinh Duong Arts", "Tranh fantasy & key visual", "Fantasy, Key Visual, Digital Art", "Huế, Vietnam"),
            ("artist6@seed.dillustration.art", "Lương Thị Yến", "Yến Lương Creative", "Thiết kế nhân vật VTuber", "VTuber Design, Character Design", "Cần Thơ, Vietnam"),
        };

        var extraModeratorSeeds = new[]
        {
            ("moderator2@seed.dillustration.art", "Tô Văn Bảo"),
            ("moderator3@seed.dillustration.art", "Chu Thị Cẩm"),
            ("moderator4@seed.dillustration.art", "Vương Văn Đạt"),
        };

        var extraUsers = new List<ApplicationUser>();

        foreach (var (email, fullName) in extraClientSeeds)
        {
            var created = await GetOrCreateUserAsync(userManager, logger, email, fullName, [UserRoleNames.Client]);
            if (created is not null) extraUsers.Add(created);
        }

        foreach (var (email, fullName, displayName, headline, specialties, location) in extraArtistSeeds)
        {
            var created = await GetOrCreateUserAsync(userManager, logger, email, fullName, [UserRoleNames.Client, UserRoleNames.Creator]);
            if (created is null) continue;
            extraUsers.Add(created);

            var hasProfile = await db.CreatorProfiles.AnyAsync(cp => cp.UserId == created.Id, cancellationToken);
            if (!hasProfile)
            {
                db.CreatorProfiles.Add(new CreatorProfile
                {
                    UserId = created.Id,
                    DisplayName = displayName,
                    Headline = headline,
                    Bio = $"Hoạ sĩ {displayName} — {headline.ToLowerInvariant()}.",
                    Specialties = specialties,
                    Location = location,
                    IsAcceptingOrders = true,
                    CommissionSlots = 3,
                    CompletedOrdersCount = 0,
                    IsApproved = true,
                    IsAiVerified = false,
                    RatingAverage = 0,
                    RatingCount = 0,
                    FollowerCount = 0
                });
            }
        }

        foreach (var (email, fullName) in extraModeratorSeeds)
        {
            var created = await GetOrCreateUserAsync(userManager, logger, email, fullName, [UserRoleNames.Moderator]);
            if (created is not null) extraUsers.Add(created);
        }

        await db.SaveChangesAsync(cancellationToken); // lưu CreatorProfile mới trước khi tạo ví

        foreach (var user in extraUsers)
        {
            var wallet = await walletService.GetOrCreateWalletAsync(user.Id, cancellationToken);
            if (wallet.Balance == 0m)
            {
                await walletService.CreditAsync(
                    wallet, WalletTransactionType.Deposit, InitialWalletBalance,
                    refType: "Seed", refId: null, note: "Seed data initial balance", cancellationToken);
            }
        }

        logger.LogInformation(
            "Extra development sample accounts ready: {ClientCount} clients, {ArtistCount} artists (+CreatorProfile), {ModeratorCount} moderators.",
            extraClientSeeds.Length, extraArtistSeeds.Length, extraModeratorSeeds.Length);
    }

    private static async Task<ApplicationUser?> GetOrCreateUserAsync(
        UserManager<ApplicationUser> userManager,
        ILogger logger,
        string email,
        string fullName,
        string[] roles)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email,
            FullName = fullName,
            IsVerified = true, // seed accounts đã "verified" sẵn để test được ngay, không cần luồng xác thực email
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, SeedPassword);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Failed to create seed user {Email}: {Errors}",
                email, string.Join("; ", result.Errors.Select(e => e.Description)));
            return null;
        }

        await userManager.AddToRolesAsync(user, roles);
        return user;
    }
}
