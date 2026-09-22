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

        // Seed default PlatformConfig values (SCR-18 / UC33)
        var defaultConfigs = new Dictionary<string, (string Value, string Description)>
        {
            [PlatformConfigKeys.PlatformFeePercent] = ("10.0", "Phần trăm phí nền tảng trên mỗi giao dịch (5.0% - 15.0%)"),
            [PlatformConfigKeys.PlatformFeeRateSqlKey] = ("0.10", "Tỷ lệ phí sàn áp dụng cho mọi giao dịch commission (database.sql)"),
            [PlatformConfigKeys.MilestoneAutoApprovalDays] = ("7", "Số ngày tự động duyệt cột mốc nếu Client không phản hồi (BR-37)"),
            [PlatformConfigKeys.EscrowHoldDaysSqlKey] = ("7", "Số ngày tạm giữ tiền escrow sau khi hoàn thành commission (database.sql)"),
            [PlatformConfigKeys.DefaultFreeRevisionLimit] = ("2", "Số lượt yêu cầu sửa đổi miễn phí mặc định cho mỗi dịch vụ/cột mốc"),
            [PlatformConfigKeys.MaxRevisionCountSqlKey] = ("2", "Số lần yêu cầu sửa đổi tối đa mặc định cho 1 milestone (database.sql)"),
            [PlatformConfigKeys.PresignedUrlExpirationMinutes] = ("15", "Thời gian hết hạn của AWS S3 / Cloudinary presigned URL (phút, BR-39)")
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

                var artwork1 = new Artwork
                {
                    Id = Guid.NewGuid(),
                    CreatorProfileId = creatorProfile.Id,
                    Title = "Lumina Cyber Sequence",
                    Description = "Cybernetic neon illustration rendered in high resolution anime aesthetic. Inspected for community guidelines compliance and AI generation check.",
                    ImageUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?q=80&w=1280&auto=format&fit=crop",
                    ThumbnailUrl = "https://images.unsplash.com/photo-1578632767115-351597cf2477?q=80&w=400&auto=format&fit=crop",
                    Style = "Cyberpunk Anime",
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
                    Style = "Surrealist Concept",
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
                    Style = "Dark Fantasy",
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

