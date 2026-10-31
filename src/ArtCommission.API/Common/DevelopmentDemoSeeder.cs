using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Auction;
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
    internal const string Password = "Demo@123456";

    public static async Task SeedAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken = default)
    {
        foreach (var roleName in new[] { "Admin", "Client", "Creator" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }

        // ==========================================
        // 1. TẠO CÁC CREATOR & CLIENT CHUYÊN NGHIỆP
        // ==========================================
        var creatorsData = new[]
        {
            (
                Email: "linh.nguyen@dillustration.vn",
                FullName: "Linh Nguyễn",
                DisplayName: "Linh Nguyen (Aura)",
                Headline: "Senior Character Illustrator · Cyberpunk & Dark Fantasy",
                Bio: "Hoạ sĩ minh hoạ tự do với 5 năm kinh nghiệm vẽ bìa tiểu thuyết, character sheet và key visual game RPG.",
                Specialties: "Character Design, Cyberpunk, Dark Fantasy",
                Location: "Hồ Chí Minh, Việt Nam",
                Avatar: "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80",
                Rating: 4.9m,
                RatingCount: 38
            ),
            (
                Email: "kai.art@dillustration.vn",
                FullName: "Ngô Quốc Huy",
                DisplayName: "KAI Art Studio",
                Headline: "Concept Artist & Matte Painter · Epic Environments",
                Bio: "Chuyên thực hiện concept môi trường thần thoại, phong cảnh viễn tưởng đồ sộ cho các studio hoạt hình.",
                Specialties: "Concept Art, Environment, Sci-Fi World",
                Location: "Hà Nội, Việt Nam",
                Avatar: "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=400&q=80",
                Rating: 5.0m,
                RatingCount: 42
            ),
            (
                Email: "anmei.illustration@dillustration.vn",
                FullName: "Mai Phương Anh",
                DisplayName: "AnMei",
                Headline: "Watercolor & Lo-Fi Chill · Romance / Slice of Life",
                Bio: "Yêu thích bảng màu pastel dịu dàng, tranh tình cảm nhẹ nhàng, minh hoạ sách truyện và postcard.",
                Specialties: "Anime Pastel, Watercolor, Slice of Life",
                Location: "Đà Nẵng, Việt Nam",
                Avatar: "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80",
                Rating: 4.8m,
                RatingCount: 26
            ),
            (
                Email: "ren.mecha@dillustration.vn",
                FullName: "Trần Bảo Lâm",
                DisplayName: "Ren MechWorks",
                Headline: "Hard-surface 2D Artist · Mecha & Sci-Fi Vehicles",
                Bio: "Chuyên vẽ robot, vũ khí tương lai và chiến hạm viễn tưởng chi tiết cao.",
                Specialties: "Mecha, Hard-surface, Weapon Concept",
                Location: "Cần Thơ, Việt Nam",
                Avatar: "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80",
                Rating: 4.95m,
                RatingCount: 19
            )
        };

        var createdCreators = new List<(ApplicationUser User, CreatorProfile Profile)>();

        foreach (var c in creatorsData)
        {
            var user = await EnsureUserAsync(userManager, c.Email, c.FullName, ["Client", "Creator"]);
            await EnsureWalletAsync(dbContext, user.Id, 5_000_000m, 0m, cancellationToken);

            var profile = await dbContext.CreatorProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id, cancellationToken);
            if (profile is null)
            {
                profile = new CreatorProfile
                {
                    UserId = user.Id,
                    DisplayName = c.DisplayName,
                    Headline = c.Headline,
                    Bio = c.Bio,
                    Specialties = c.Specialties,
                    Location = c.Location,
                    IsAcceptingOrders = true,
                    IsApproved = true,
                    RatingAverage = c.Rating,
                    RatingCount = c.RatingCount,
                    RateCardJson = """
                        [
                          {"id":"headshot","name":"Bản vẽ Chân dung","description":"Chân dung chi tiết, nền tối giản, độ phân giải 4K.","price":500000},
                          {"id":"half-body","name":"Nửa người (Half-body)","description":"Tạo hình nhân vật nửa người kèm trang phục, phụ kiện.","price":900000},
                          {"id":"full-body","name":"Toàn thân & Background","description":"Toàn thân nhân vật phối cảnh hoàn thiện với ánh sáng chuẩn điện ảnh.","price":1800000}
                        ]
                        """
                };
                dbContext.CreatorProfiles.Add(profile);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            createdCreators.Add((user, profile));
        }

        // Tạo Client demo & Admin
        var adminUser = await EnsureUserAsync(userManager, "admin@dillustration.vn", "Hệ Thống Quản Trị", ["Admin", "Client"]);
        var clientUser = await EnsureUserAsync(userManager, "client.demo@dillustration.test", "Minh Trí (Collector)", ["Client"]);
        await EnsureWalletAsync(dbContext, clientUser.Id, 25_000_000m, 5_000_000m, cancellationToken);

        // ==========================================
        // 2. TẠO KHO TRANH CHUYÊN NGHIỆP (PORTFOLIO & GALLERY)
        // ==========================================
        var artworksData = new[]
        {
            // Tranh của Linh Nguyen (Anime / Dark Fantasy)
            (
                CreatorIdx: 0,
                Title: "Moonlit Sorceress in Neon Bloom",
                Desc: "Nữ pháp sư dưới ánh trăng tím ma mị kết hợp họa tiết phát quang viễn tưởng. Tác phẩm hoàn thiện trong 24 giờ vẽ số.",
                Image: "https://images.unsplash.com/photo-1578632767115-351597cf2477?auto=format&fit=crop&w=1400&q=85",
                Style: "Anime",
                Views: 1250,
                Likes: 340,
                Price: 850_000m
            ),
            (
                CreatorIdx: 0,
                Title: "Cyber Ronin: Final Stance",
                Desc: "Chiến binh samurai thời đại số đứng giữa cơn mưa acid của Tokyo Cyberpunk. Độ phân giải 4K sắc nét.",
                Image: "https://images.unsplash.com/photo-1542751371-adc38448a05e?auto=format&fit=crop&w=1400&q=85",
                Style: "Cyberpunk",
                Views: 2100,
                Likes: 580,
                Price: 1_200_000m
            ),
            (
                CreatorIdx: 0,
                Title: "Crimson Eclipse Assassin",
                Desc: "Thiết kế nhân vật sát thủ bóng đêm cho tựa game thẻ bài ARPG giả tưởng.",
                Image: "https://images.unsplash.com/photo-1563089145-599997674d42?auto=format&fit=crop&w=1400&q=85",
                Style: "Dark Fantasy",
                Views: 940,
                Likes: 215,
                Price: 750_000m
            ),

            // Tranh của KAI Art Studio (Environment / Sci-Fi)
            (
                CreatorIdx: 1,
                Title: "Citadel of Celestial Light",
                Desc: "Concept thành trì thần thánh lơ lửng trên biển mây. Ánh sáng vàng kim rực rỡ phong cách điện ảnh.",
                Image: "https://images.unsplash.com/photo-1518709268805-4e9042af9f23?auto=format&fit=crop&w=1400&q=85",
                Style: "Fantasy Environment",
                Views: 3200,
                Likes: 890,
                Price: 2_500_000m
            ),
            (
                CreatorIdx: 1,
                Title: "The Forgotten Megastructure",
                Desc: "Tàn tích công trình vũ trụ khổng lồ bị bỏ hoang hàng vạn năm giữa hành tinh xa xôi.",
                Image: "https://images.unsplash.com/photo-1506703719100-a0f3a48c0f86?auto=format&fit=crop&w=1400&q=85",
                Style: "Sci-Fi Concept",
                Views: 1850,
                Likes: 460,
                Price: 1_900_000m
            ),

            // Tranh của AnMei (Watercolor / Slice of Life)
            (
                CreatorIdx: 2,
                Title: "Afternoon Cafe Melody",
                Desc: "Buổi chiều êm ả bên ly latte và những trang sách nhỏ. Phong cách màu nước ấm áp gợi hoài niệm.",
                Image: "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?auto=format&fit=crop&w=1400&q=85",
                Style: "Watercolor",
                Views: 1420,
                Likes: 395,
                Price: 650_000m
            ),
            (
                CreatorIdx: 2,
                Title: "Rainy Bus Stop at Twilight",
                Desc: "Cơn mưa rào bất chợt lúc tan trường. Ánh đèn đường loang trên vũng nước tạo nên cảm giác thân thương.",
                Image: "https://images.unsplash.com/photo-1509198397868-475647b2a1e5?auto=format&fit=crop&w=1400&q=85",
                Style: "Lo-Fi Slice of Life",
                Views: 2650,
                Likes: 720,
                Price: 800_000m
            ),

            // Tranh của Ren MechWorks (Mecha)
            (
                CreatorIdx: 3,
                Title: "Valkyrie-04 Orbital Interceptor",
                Desc: "Cơ giáp đánh chặn thế hệ mới trang bị hệ thống phản lực ion kép và cánh năng lượng photon.",
                Image: "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?auto=format&fit=crop&w=1400&q=85",
                Style: "Mecha Concept",
                Views: 1680,
                Likes: 510,
                Price: 1_500_000m
            ),
            (
                CreatorIdx: 3,
                Title: "Titan Heavy Siege Armor",
                Desc: "Người máy hạng nặng phòng thủ đô thị, lớp giáp phản ứng nổ nguyên khối và súng từ trường.",
                Image: "https://images.unsplash.com/photo-1579783902614-a3fb3927b675?auto=format&fit=crop&w=1400&q=85",
                Style: "Hard-surface",
                Views: 1120,
                Likes: 330,
                Price: 1_350_000m
            )
        };

        var seededArtworks = new List<Artwork>();

        foreach (var a in artworksData)
        {
            var creator = createdCreators[a.CreatorIdx];
            var artwork = await dbContext.Artworks.FirstOrDefaultAsync(art => art.Title == a.Title, cancellationToken);
            if (artwork is null)
            {
                artwork = new Artwork
                {
                    CreatorProfileId = creator.Profile.Id,
                    Title = a.Title,
                    Description = a.Desc,
                    ImageUrl = a.Image,
                    ThumbnailUrl = a.Image,
                    Style = a.Style,
                    ViewCount = a.Views,
                    LikeCount = a.Likes,
                    StartingPrice = a.Price,
                    IsAiGenerated = false,
                    ModerationStatus = "Approved",
                    LicenseType = "Commercial"
                };
                dbContext.Artworks.Add(artwork);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            seededArtworks.Add(artwork);
        }

        // ==========================================
        // 3. TẠO CÁC PHIÊN ĐẤU GIÁ SÔI ĐỘNG (AUCTIONS)
        // ==========================================
        if (seededArtworks.Count >= 4)
        {
            // Phiên 1: ĐANG DIỄN RA (Active) - Cực hot, nhiều bid
            var artActive1 = seededArtworks[1]; // Cyber Ronin
            var auction1 = await dbContext.Auctions.FirstOrDefaultAsync(x => x.ArtworkId == artActive1.Id, cancellationToken);
            if (auction1 is null)
            {
                var seller = createdCreators[0].User;
                auction1 = new Auction
                {
                    ArtworkId = artActive1.Id,
                    SellerId = seller.Id,
                    AuctionType = AuctionType.Standard,
                    StartPrice = 1_000_000m,
                    CurrentPrice = 1_600_000m,
                    BidStep = 100_000m,
                    BuyNowPrice = 3_000_000m,
                    BidCount = 6,
                    StartAt = DateTimeOffset.UtcNow.AddHours(-12),
                    EndAt = DateTimeOffset.UtcNow.AddHours(36), // Còn 1.5 ngày
                    Status = AuctionStatus.Active
                };
                dbContext.Auctions.Add(auction1);
                await dbContext.SaveChangesAsync(cancellationToken);

                // Thêm lượt đặt giá mẫu
                dbContext.Bids.Add(new Bid
                {
                    AuctionId = auction1.Id,
                    BidderId = clientUser.Id,
                    Amount = 1_600_000m,
                    Status = BidStatus.Leading,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-15)
                });
            }

            // Phiên 2: ĐANG DIỄN RA (Active) - Tranh phong cảnh vũ trụ của KAI
            var artActive2 = seededArtworks[3]; // Citadel of Celestial Light
            var auction2 = await dbContext.Auctions.FirstOrDefaultAsync(x => x.ArtworkId == artActive2.Id, cancellationToken);
            if (auction2 is null)
            {
                var seller = createdCreators[1].User;
                auction2 = new Auction
                {
                    ArtworkId = artActive2.Id,
                    SellerId = seller.Id,
                    AuctionType = AuctionType.BuyNow,
                    StartPrice = 2_000_000m,
                    CurrentPrice = 2_800_000m,
                    BidStep = 200_000m,
                    BuyNowPrice = 5_000_000m,
                    BidCount = 4,
                    StartAt = DateTimeOffset.UtcNow.AddDays(-1),
                    EndAt = DateTimeOffset.UtcNow.AddHours(8), // Sắp kết thúc
                    Status = AuctionStatus.Active
                };
                dbContext.Auctions.Add(auction2);
                await dbContext.SaveChangesAsync(cancellationToken);

                dbContext.Bids.Add(new Bid
                {
                    AuctionId = auction2.Id,
                    BidderId = clientUser.Id,
                    Amount = 2_800_000m,
                    Status = BidStatus.Leading,
                    CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-40)
                });
            }

            // Phiên 3: SẮP DIỄN RA (Scheduled)
            var artScheduled = seededArtworks[7]; // Valkyrie Mecha
            var auction3 = await dbContext.Auctions.FirstOrDefaultAsync(x => x.ArtworkId == artScheduled.Id, cancellationToken);
            if (auction3 is null)
            {
                var seller = createdCreators[3].User;
                auction3 = new Auction
                {
                    ArtworkId = artScheduled.Id,
                    SellerId = seller.Id,
                    AuctionType = AuctionType.Standard,
                    StartPrice = 1_500_000m,
                    CurrentPrice = 1_500_000m,
                    BidStep = 150_000m,
                    BuyNowPrice = 4_000_000m,
                    BidCount = 0,
                    StartAt = DateTimeOffset.UtcNow.AddHours(6), // 6 tiếng nữa bắt đầu
                    EndAt = DateTimeOffset.UtcNow.AddDays(3),
                    Status = AuctionStatus.Scheduled
                };
                dbContext.Auctions.Add(auction3);
            }

            // Phiên 4: ĐÃ HOÀN TẤT (Settled)
            var artSettled = seededArtworks[5]; // Afternoon Cafe
            var auction4 = await dbContext.Auctions.FirstOrDefaultAsync(x => x.ArtworkId == artSettled.Id, cancellationToken);
            if (auction4 is null)
            {
                var seller = createdCreators[2].User;
                auction4 = new Auction
                {
                    ArtworkId = artSettled.Id,
                    SellerId = seller.Id,
                    AuctionType = AuctionType.Standard,
                    StartPrice = 500_000m,
                    CurrentPrice = 1_200_000m,
                    FinalPrice = 1_200_000m,
                    BidStep = 50_000m,
                    BidCount = 8,
                    WinnerId = clientUser.Id,
                    StartAt = DateTimeOffset.UtcNow.AddDays(-5),
                    EndAt = DateTimeOffset.UtcNow.AddDays(-1),
                    SettledAt = DateTimeOffset.UtcNow.AddHours(-18),
                    Status = AuctionStatus.Settled
                };
                dbContext.Auctions.Add(auction4);
            }
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
                throw new InvalidOperationException($"Không thể tạo tài khoản demo '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
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
            LockedBalance = lockedBalance,
            Status = WalletStatus.Active
        };

        dbContext.Wallets.Add(wallet);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
