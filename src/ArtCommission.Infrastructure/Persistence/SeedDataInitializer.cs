using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Domain.Entities.Payment;
using ArtCommission.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Infrastructure.Persistence;

/// <summary>
/// Inserts the development/demo data required to exercise authentication, wallets and portfolios.
/// Every record has a stable natural or primary-key lookup so startup is safe to repeat.
/// </summary>
public static class SeedDataInitializer
{
    private const decimal InitialWalletBalance = 5_000_000m;
    private const string InitialWalletRefType = "SeedDataInitializer";
    private const string SeedPassword = "Seed123!";

    private static readonly Guid ClientUserId = Guid.Parse("0b63f48f-0c3e-4a0b-9e18-cf90ba7b2011");
    private static readonly Guid CreatorUserId = Guid.Parse("8935bf9e-e2dc-4b2c-87d4-5baa354ee104");
    private static readonly Guid AdministratorUserId = Guid.Parse("8c348eaa-9334-47e2-9b3e-d70b143a80a1");
    private static readonly Guid CreatorProfileId = Guid.Parse("bbfd745c-a9a5-4e39-a5d6-dd9e2e27fd7f");

    public static async Task InitializeAsync(
        AppDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        CancellationToken cancellationToken = default)
    {
        await EnsureRolesAsync(roleManager);

        var client = await EnsureUserAsync(userManager, ClientUserId, "client.demo@dillustration.local", "Demo Client", [UserRoleNames.Client]);
        var creator = await EnsureUserAsync(userManager, CreatorUserId, "artist.demo@dillustration.local", "Demo Artist", [UserRoleNames.Client, UserRoleNames.Creator]);
        var administrator = await EnsureUserAsync(userManager, AdministratorUserId, "admin.demo@dillustration.local", "Demo Administrator", [UserRoleNames.Administrator]);

        await EnsureWalletAsync(dbContext, client.Id, cancellationToken);
        await EnsureWalletAsync(dbContext, creator.Id, cancellationToken);
        await EnsureWalletAsync(dbContext, administrator.Id, cancellationToken);

        var creatorProfile = await EnsureCreatorProfileAsync(dbContext, creator.Id, cancellationToken);
        await EnsurePortfolioAsync(dbContext, creatorProfile, cancellationToken);
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
    {
        foreach (var roleName in UserRoleNames.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureSuccess(result, $"create role '{roleName}'");
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        Guid id,
        string email,
        string fullName,
        string[] roles)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = id,
                Email = email,
                UserName = email,
                FullName = fullName,
                IsVerified = true
            };

            var result = await userManager.CreateAsync(user, SeedPassword);
            EnsureSuccess(result, $"create seed user '{email}'");
        }

        foreach (var roleName in roles)
        {
            if (!await userManager.IsInRoleAsync(user, roleName))
            {
                var result = await userManager.AddToRoleAsync(user, roleName);
                EnsureSuccess(result, $"assign role '{roleName}' to '{email}'");
            }
        }

        return user;
    }

    private static async Task EnsureWalletAsync(AppDbContext dbContext, Guid userId, CancellationToken cancellationToken)
    {
        if (await dbContext.Wallets.AnyAsync(wallet => wallet.UserId == userId, cancellationToken))
        {
            return;
        }

        var wallet = new Wallet
        {
            UserId = userId,
            Balance = InitialWalletBalance,
            LockedBalance = 0m,
            Currency = "VND",
            Status = WalletStatus.Active
        };

        dbContext.Wallets.Add(wallet);
        dbContext.WalletTransactions.Add(new WalletTransaction
        {
            WalletId = wallet.Id,
            Type = WalletTransactionType.Deposit,
            Direction = WalletTransactionDirection.In,
            Amount = InitialWalletBalance,
            BalanceAfter = InitialWalletBalance,
            RefType = InitialWalletRefType,
            RefId = userId,
            Note = "Initial demo wallet balance"
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<CreatorProfile> EnsureCreatorProfileAsync(
        AppDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.CreatorProfiles
            .SingleOrDefaultAsync(candidate => candidate.UserId == userId, cancellationToken);
        if (profile is not null)
        {
            return profile;
        }

        profile = new CreatorProfile
        {
            Id = CreatorProfileId,
            UserId = userId,
            DisplayName = "Minh Anh Studio",
            Bio = "Digital illustrator specializing in colorful character art and cozy scenes.",
            RateCard = "{\"startingPrice\":500000,\"currency\":\"VND\"}",
            CommissionSlots = 3,
            RatingAvg = 4.9m,
            IsAiVerified = true
        };
        dbContext.CreatorProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        return profile;
    }

    private static async Task EnsurePortfolioAsync(
        AppDbContext dbContext,
        CreatorProfile creatorProfile,
        CancellationToken cancellationToken)
    {
        var tagDefinitions = new[]
        {
            (Id: Guid.Parse("04f5c23a-4889-4b2c-b0f7-0850cfd59b6b"), Name: "Character Design", IsAiGenerated: false),
            (Id: Guid.Parse("0c0c0f9e-5ad2-4bdb-8651-10c7ed5e4413"), Name: "Fantasy", IsAiGenerated: false),
            (Id: Guid.Parse("6245a65c-6a98-42e1-ae6a-4d9e7eaf873f"), Name: "Anime", IsAiGenerated: false),
            (Id: Guid.Parse("8489501d-f64e-42ac-88c5-04f251cdc79a"), Name: "Cozy", IsAiGenerated: false),
            (Id: Guid.Parse("ea0d9857-c11c-462d-9f1f-f8ca963c1b4c"), Name: "Digital Painting", IsAiGenerated: true)
        };

        var tagsByName = await dbContext.Tags.ToDictionaryAsync(tag => tag.Name, cancellationToken);
        foreach (var definition in tagDefinitions)
        {
            if (!tagsByName.ContainsKey(definition.Name))
            {
                var tag = new Tag { Id = definition.Id, Name = definition.Name, IsAiGenerated = definition.IsAiGenerated };
                dbContext.Tags.Add(tag);
                tagsByName.Add(tag.Name, tag);
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var artworks = new[]
        {
            (Id: Guid.Parse("c4d0b285-3f5b-4f34-bcb2-d837fee9bf69"), Title: "Lanterns of Hoi An", Description: "A warm evening walk under silk lanterns.", ImageUrl: "https://images.unsplash.com/photo-1528127269322-539801943592", Style: "Digital Painting", Tags: new[] { "Cozy", "Digital Painting" }),
            (Id: Guid.Parse("c7cac8e7-c595-4d84-9e9b-b40725c4cfa4"), Title: "Skybound Courier", Description: "An anime-inspired courier crossing floating islands.", ImageUrl: "https://images.unsplash.com/photo-1518709268805-4e9042af2176", Style: "Anime", Tags: new[] { "Anime", "Fantasy", "Character Design" }),
            (Id: Guid.Parse("2734c63d-33a1-4577-a9ce-df47595c4e1f"), Title: "Moonlit Guardian", Description: "A quiet guardian watches over an enchanted forest.", ImageUrl: "https://images.unsplash.com/photo-1519608487953-e999c86e7452", Style: "Fantasy", Tags: new[] { "Fantasy", "Character Design" }),
            (Id: Guid.Parse("5143b2c3-4206-4713-ba0f-bc25a1adcddf"), Title: "Sunday Sketchbook", Description: "Soft colors and a slow morning at home.", ImageUrl: "https://images.unsplash.com/photo-1455390582262-044cdead277a", Style: "Digital Painting", Tags: new[] { "Cozy", "Digital Painting" }),
            (Id: Guid.Parse("2ce243c8-dd28-47ab-9841-75e8d9bea9cf"), Title: "Crimson Blade", Description: "A character study of a determined wandering swordswoman.", ImageUrl: "https://images.unsplash.com/photo-1579546929518-9e396f3cc809", Style: "Anime", Tags: new[] { "Anime", "Character Design" })
        };

        foreach (var definition in artworks)
        {
            var artwork = await dbContext.Artworks.FindAsync([definition.Id], cancellationToken);
            if (artwork is null)
            {
                artwork = new Artwork
                {
                    Id = definition.Id,
                    CreatorId = creatorProfile.Id,
                    Title = definition.Title,
                    Description = definition.Description,
                    ImageUrl = definition.ImageUrl,
                    Style = definition.Style,
                    Status = ArtworkStatus.Published
                };
                dbContext.Artworks.Add(artwork);
            }

            foreach (var tagName in definition.Tags)
            {
                var tag = tagsByName[tagName];
                if (!await dbContext.ArtworkTags.AnyAsync(link => link.ArtworkId == artwork.Id && link.TagId == tag.Id, cancellationToken))
                {
                    dbContext.ArtworkTags.Add(new ArtworkTag { ArtworkId = artwork.Id, TagId = tag.Id });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void EnsureSuccess(IdentityResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"Unable to {operation}: {string.Join("; ", result.Errors.Select(error => error.Description))}");
        }
    }
}
