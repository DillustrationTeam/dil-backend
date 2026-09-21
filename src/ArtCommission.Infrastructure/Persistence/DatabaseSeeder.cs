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
    }
}
