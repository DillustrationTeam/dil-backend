using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ArtCommission.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var dbContext = serviceProvider.GetRequiredService<AppDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.ArtworkWatermarkReceipts', 'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ArtworkWatermarkReceipts] (
                    [Id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [ArtworkId] uniqueidentifier NULL,
                    [CommissionId] uniqueidentifier NULL,
                    [MilestoneId] uniqueidentifier NULL,
                    [ReceiptToken] nvarchar(64) NOT NULL,
                    [NumericPayload] bigint NULL,
                    [FileSha256] nvarchar(128) NULL,
                    [FileName] nvarchar(256) NULL,
                    [ViewerUserId] uniqueidentifier NULL,
                    [ViewerEmail] nvarchar(256) NULL,
                    [ViewerName] nvarchar(256) NULL,
                    [IpAddress] nvarchar(64) NOT NULL,
                    [UserAgent] nvarchar(500) NULL,
                    [SessionId] nvarchar(128) NULL,
                    [WatermarkType] nvarchar(50) NOT NULL DEFAULT 'DwtForensic',
                    [Status] nvarchar(50) NOT NULL DEFAULT 'Active',
                    [Notes] nvarchar(1000) NULL,
                    [CreatedAt] datetimeoffset NOT NULL,
                    [UpdatedAt] datetimeoffset NULL,
                    [IsDeleted] bit NOT NULL DEFAULT 0,
                    CONSTRAINT [FK_ArtworkWatermarkReceipts_Artworks_ArtworkId]
                        FOREIGN KEY ([ArtworkId]) REFERENCES [dbo].[Artworks] ([Id]) ON DELETE SET NULL
                );
                CREATE UNIQUE INDEX [IX_ArtworkWatermarkReceipts_ReceiptToken] ON [dbo].[ArtworkWatermarkReceipts] ([ReceiptToken]);
                CREATE INDEX [IX_ArtworkWatermarkReceipts_ArtworkId] ON [dbo].[ArtworkWatermarkReceipts] ([ArtworkId]);
                CREATE INDEX [IX_ArtworkWatermarkReceipts_CommissionId] ON [dbo].[ArtworkWatermarkReceipts] ([CommissionId]);
                CREATE INDEX [IX_ArtworkWatermarkReceipts_FileSha256] ON [dbo].[ArtworkWatermarkReceipts] ([FileSha256]);
                CREATE INDEX [IX_ArtworkWatermarkReceipts_ViewerUserId] ON [dbo].[ArtworkWatermarkReceipts] ([ViewerUserId]);
                CREATE INDEX [IX_ArtworkWatermarkReceipts_CreatedAt] ON [dbo].[ArtworkWatermarkReceipts] ([CreatedAt]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH('dbo.ArtworkWatermarkReceipts', 'CommissionId') IS NULL
                    ALTER TABLE [dbo].[ArtworkWatermarkReceipts] ADD [CommissionId] uniqueidentifier NULL;
                IF COL_LENGTH('dbo.ArtworkWatermarkReceipts', 'MilestoneId') IS NULL
                    ALTER TABLE [dbo].[ArtworkWatermarkReceipts] ADD [MilestoneId] uniqueidentifier NULL;
                IF COL_LENGTH('dbo.ArtworkWatermarkReceipts', 'FileSha256') IS NULL
                    ALTER TABLE [dbo].[ArtworkWatermarkReceipts] ADD [FileSha256] nvarchar(128) NULL;
                IF COL_LENGTH('dbo.ArtworkWatermarkReceipts', 'FileName') IS NULL
                    ALTER TABLE [dbo].[ArtworkWatermarkReceipts] ADD [FileName] nvarchar(256) NULL;
                IF COL_LENGTH('dbo.ArtworkWatermarkReceipts', 'VisualHash') IS NULL
                    ALTER TABLE [dbo].[ArtworkWatermarkReceipts] ADD [VisualHash] bigint NULL;
            END");

        var roles = new[]
        {
            UserRoleNames.Administrator,
            UserRoleNames.Moderator,
            UserRoleNames.Creator,
            UserRoleNames.Client
        };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var adminEmail = "admin.demo@dillustration.local";
        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "System Administrator",
                EmailConfirmed = true,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createAdminResult = await userManager.CreateAsync(admin, "Seed123!");
            if (createAdminResult.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, UserRoleNames.Administrator);
            }
        }
        else
        {
            if (!await userManager.IsInRoleAsync(existingAdmin, UserRoleNames.Administrator))
            {
                await userManager.AddToRoleAsync(existingAdmin, UserRoleNames.Administrator);
            }
        }

        var creatorEmail = "creator.demo@dillustration.local";
        var existingCreator = await userManager.FindByEmailAsync(creatorEmail);
        if (existingCreator == null)
        {
            var creator = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = creatorEmail,
                Email = creatorEmail,
                FullName = "Demo Artist Creator",
                EmailConfirmed = true,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createCreatorResult = await userManager.CreateAsync(creator, "Seed123!");
            if (createCreatorResult.Succeeded)
            {
                await userManager.AddToRoleAsync(creator, UserRoleNames.Creator);
                await userManager.AddToRoleAsync(creator, UserRoleNames.Client);

                if (!await dbContext.CreatorProfiles.AnyAsync(p => p.UserId == creator.Id))
                {
                    dbContext.CreatorProfiles.Add(new CreatorProfile
                    {
                        UserId = creator.Id,
                        DisplayName = "Demo Artist Studio",
                        Bio = "Digital Illustrator & Concept Artist demo profile.",
                        IsAcceptingOrders = true,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                    await dbContext.SaveChangesAsync();
                }
            }
        }

        var clientEmail = "client.demo@dillustration.local";
        var existingClient = await userManager.FindByEmailAsync(clientEmail);
        if (existingClient == null)
        {
            var client = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = clientEmail,
                Email = clientEmail,
                FullName = "Demo Client",
                EmailConfirmed = true,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createClientResult = await userManager.CreateAsync(client, "Seed123!");
            if (createClientResult.Succeeded)
            {
                await userManager.AddToRoleAsync(client, UserRoleNames.Client);
            }
        }

        var quanEmail = "minhquaningenious@gmail.com";
        var existingQuan = await userManager.FindByEmailAsync(quanEmail);
        if (existingQuan == null)
        {
            var quanUser = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = quanEmail,
                Email = quanEmail,
                FullName = "Ngo Le Minh Quan",
                EmailConfirmed = true,
                IsVerified = true,
                CreatedAt = DateTimeOffset.UtcNow
            };

            var createQuanResult = await userManager.CreateAsync(quanUser, "abc123");
            if (createQuanResult.Succeeded)
            {
                await userManager.AddToRoleAsync(quanUser, UserRoleNames.Client);
            }
        }

        // Seed default PlatformConfig values (SCR-18 / UC33)
        var defaultConfigs = new Dictionary<string, (string Value, string Description)>
        {
            [PlatformConfigKeys.PlatformFeePercent] = ("10.0", "Phần trăm phí nền tảng trên mỗi giao dịch (5.0% - 15.0%)"),
            [PlatformConfigKeys.PlatformFeeRateSqlKey] = ("0.10", "Tỷ lệ phí sàn áp dụng cho mọi giao dịch commission (database.sql)"),
            [PlatformConfigKeys.MilestoneAutoApprovalDays] = ("7", "Số ngày tự động duyệt cột mốc nếu Client không phản hồi (BR-37)"),
            [PlatformConfigKeys.EscrowHoldDaysSqlKey] = ("7", "Số ngày tạm giữ tiền escrow sau khi hoàn thành commission (database.sql)"),
            [PlatformConfigKeys.DefaultFreeRevisionLimit] = ("2", "Số lượt yêu cầu sửa đổi miễn phí mặc định cho mỗi dịch vụ/cột mốc"),
            [PlatformConfigKeys.MaxRevisionCountSqlKey] = ("2", "Số lần yêu cầu sửa đổi tối đa mặc định cho 1 milestone (database.sql)"),
            [PlatformConfigKeys.PresignedUrlExpirationMinutes] = ("15", "Thời gian hết hạn của AWS S3 / Cloudinary presigned URL (phút, BR-39)"),
            [PlatformConfigKeys.AiScanAdultThreshold] = ("0.70", "Ngưỡng cảnh báo nội dung người lớn (Adult/NSFW) từ AI (0.0 - 1.0)"),
            [PlatformConfigKeys.AiScanViolenceThreshold] = ("0.70", "Ngưỡng cảnh báo bạo lực/máu me (Violence/Gore) từ AI (0.0 - 1.0)"),
            [PlatformConfigKeys.AiScanAiDetectionThreshold] = ("0.75", "Ngưỡng xác suất tranh do AI tạo để tự động gắn cờ (0.0 - 1.0)"),
            [PlatformConfigKeys.AiScanAutoTaggingEnabled] = ("true", "Bật/tắt tính năng AI tự động gợi ý và gắn tag phong cách cho tác phẩm (true/false)")
        };

        var configKeys = defaultConfigs.Keys.ToList();
        var existingConfigKeys = await dbContext.PlatformConfigs
            .Where(c => configKeys.Contains(c.Key))
            .Select(c => c.Key)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;
        var needsSave = false;
        foreach (var (key, (val, desc)) in defaultConfigs)
        {
            if (!existingConfigKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                dbContext.PlatformConfigs.Add(new PlatformConfig
                {
                    Id = Guid.NewGuid(),
                    Key = key,
                    Value = val,
                    Description = desc,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false
                });
                needsSave = true;
            }
        }

        if (needsSave)
        {
            await dbContext.SaveChangesAsync();
        }

        // Seed 8 default ArtworkCategories (idempotent)
        var defaultCategories = new (string Name, string Slug, string Description, int DisplayOrder)[]
        {
            ("Anime / Manga", "anime-manga", "Minh họa nhân vật, phong cách anime và manga Nhật Bản", 1),
            ("Concept Art", "concept-art", "Thiết kế ý tưởng nhân vật, bối cảnh, vũ khí và thế giới game/phim", 2),
            ("Chibi / Cute", "chibi-cute", "Tranh vẽ nhân vật tỷ lệ nhỏ dễ thương, mascot và sticker", 3),
            ("Semi-Realistic", "semi-realistic", "Phong cách bán tả thực kết hợp mỹ thuật số hiện đại", 4),
            ("Realistic / Portrait", "realistic-portrait", "Tranh chân dung và minh họa tả thực chi tiết cao", 5),
            ("Mecha / Sci-Fi", "mecha-sci-fi", "Robot, cơ khí, phi thuyền và bối cảnh khoa học viễn tưởng", 6),
            ("Landscape / Background", "landscape-background", "Phong cảnh thiên nhiên, kiến trúc và phông nền môi trường", 7),
            ("Pixel Art", "pixel-art", "Nghệ thuật điểm ảnh retro, sprite game và hoạt họa pixel", 8)
        };

        var existingSlugs = await dbContext.ArtworkCategories
            .Select(c => c.Slug)
            .ToListAsync();

        var categoriesToAdd = new List<ArtworkCategory>();
        foreach (var (name, slug, description, displayOrder) in defaultCategories)
        {
            if (!existingSlugs.Contains(slug, StringComparer.OrdinalIgnoreCase))
            {
                categoriesToAdd.Add(new ArtworkCategory
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Slug = slug,
                    Description = description,
                    DisplayOrder = displayOrder,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false
                });
            }
        }

        if (categoriesToAdd.Count > 0)
        {
            dbContext.ArtworkCategories.AddRange(categoriesToAdd);
            await dbContext.SaveChangesAsync();
        }

        // Seed 3 artworks for Content & AI Moderation Queue (SCR-20 / UC28)
        if (!await dbContext.Artworks.AnyAsync())
        {
            var creatorProfile = await dbContext.CreatorProfiles.FirstOrDefaultAsync();
            if (creatorProfile != null)
            {
                var tagCyberpunk = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "Cyberpunk")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "Cyberpunk", IsAiGenerated = false };
                var tagAnime = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "Anime")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "Anime", IsAiGenerated = false };
                var tagDigital = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "DigitalPainting")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "DigitalPainting", IsAiGenerated = false };
                var tagAndroid = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "Android")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "Android", IsAiGenerated = true };
                var tagSciFi = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "SciFi")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "SciFi", IsAiGenerated = false };
                var tagDarkFantasy = await dbContext.Tags.FirstOrDefaultAsync(t => t.Name == "DarkFantasy")
                    ?? new Tag { Id = Guid.NewGuid(), Name = "DarkFantasy", IsAiGenerated = false };

                var tagsToSeed = new[] { tagCyberpunk, tagAnime, tagDigital, tagAndroid, tagSciFi, tagDarkFantasy };
                foreach (var tag in tagsToSeed)
                {
                    if (!await dbContext.Tags.AnyAsync(t => t.Id == tag.Id || t.Name == tag.Name))
                    {
                        dbContext.Tags.Add(tag);
                    }
                }
                await dbContext.SaveChangesAsync();

                var catAnime = await dbContext.ArtworkCategories.FirstOrDefaultAsync(c => c.Slug == "anime-manga");
                var catSciFi = await dbContext.ArtworkCategories.FirstOrDefaultAsync(c => c.Slug == "mecha-sci-fi");
                var catConcept = await dbContext.ArtworkCategories.FirstOrDefaultAsync(c => c.Slug == "concept-art");

                var artwork1 = new Artwork
                {
                    Id = Guid.NewGuid(),
                    CreatorProfileId = creatorProfile.Id,
                    Title = "Lumina Cyber Sequence",
                    Description = "Cybernetic neon illustration rendered in high resolution anime aesthetic. Inspected for community guidelines compliance and AI generation check.",
                    ImageUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?q=80&w=1280&auto=format&fit=crop",
                    ThumbnailUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?q=80&w=400&auto=format&fit=crop",
                    CategoryId = catAnime?.Id,
                    Style = catAnime?.Name ?? "Cyberpunk Anime",
                    LikeCount = 342,
                    ViewCount = 1280,
                    SafeScore = 0.984m,
                    AdultScore = 0.012m,
                    ViolenceScore = 0.002m,
                    IsAiGenerated = false,
                    AiDetectionScore = 0.04m,
                    FlagReason = "AI NSFW Threshold Borderline",
                    ModerationStatus = "Pending",
                    Resolution = "3840x2160",
                    FileSizeBytes = 19293798,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                artwork1.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork1.Id, TagId = tagCyberpunk.Id });
                artwork1.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork1.Id, TagId = tagAnime.Id });
                artwork1.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork1.Id, TagId = tagDigital.Id });

                var artwork2 = new Artwork
                {
                    Id = Guid.NewGuid(),
                    CreatorProfileId = creatorProfile.Id,
                    Title = "Solaris Android Genesis",
                    Description = "Futuristic synthetic humanoid awakening in neon chamber.",
                    ImageUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?q=80&w=1280&auto=format&fit=crop",
                    ThumbnailUrl = "https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?q=80&w=400&auto=format&fit=crop",
                    CategoryId = catSciFi?.Id,
                    Style = catSciFi?.Name ?? "Surrealist Concept",
                    LikeCount = 195,
                    ViewCount = 890,
                    SafeScore = 0.912m,
                    AdultScore = 0.045m,
                    ViolenceScore = 0.018m,
                    IsAiGenerated = true,
                    AiDetectionScore = 0.96m,
                    FlagReason = "AI Generated Elements Detected",
                    ModerationStatus = "Pending",
                    Resolution = "2560x1440",
                    FileSizeBytes = 12450000,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-2)
                };
                artwork2.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork2.Id, TagId = tagAndroid.Id });
                artwork2.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork2.Id, TagId = tagSciFi.Id });

                var artwork3 = new Artwork
                {
                    Id = Guid.NewGuid(),
                    CreatorProfileId = creatorProfile.Id,
                    Title = "Abyssal Crimson Knight",
                    Description = "Armored warrior standing before a blood moon eclipse.",
                    ImageUrl = "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?q=80&w=1280&auto=format&fit=crop",
                    ThumbnailUrl = "https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?q=80&w=400&auto=format&fit=crop",
                    CategoryId = catConcept?.Id,
                    Style = catConcept?.Name ?? "Dark Fantasy",
                    LikeCount = 512,
                    ViewCount = 2140,
                    SafeScore = 0.765m,
                    AdultScore = 0.082m,
                    ViolenceScore = 0.185m,
                    IsAiGenerated = false,
                    AiDetectionScore = 0.08m,
                    FlagReason = "User Reported: Violence / Gore Warning",
                    ModerationStatus = "Pending",
                    Resolution = "1920x1080",
                    FileSizeBytes = 8540000,
                    CreatedAt = DateTimeOffset.UtcNow.AddHours(-5)
                };
                artwork3.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork3.Id, TagId = tagDarkFantasy.Id });

                dbContext.Artworks.AddRange(artwork1, artwork2, artwork3);
                await dbContext.SaveChangesAsync();
            }
        }
    }
}

