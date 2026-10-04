using System.Text.Json;
using ArtCommission.Application.ArtistStudio.Queries.GetCreatorRateCard;
using ArtCommission.Application.Commission.DTOs;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Infrastructure.Persistence;
using ArtCommission.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var creatorId = Guid.NewGuid();
var packageId = Guid.NewGuid();
await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseInMemoryDatabase("license-pricing-" + Guid.NewGuid()).Options);
db.CreatorProfiles.Add(new CreatorProfile
{
    Id = creatorId,
    UserId = Guid.NewGuid(),
    DisplayName = "Creator",
    IsAcceptingOrders = true,
    RateCardJson = JsonSerializer.Serialize(new[]
    {
        new RateCardPackageDto(packageId, "Package", null, 300m,
        [new(1, "Sketch", 100m), new(2, "Final", 200m)])
    })
});
db.CreatorTerms.Add(new CreatorTerms { CreatorProfileId = creatorId, CommercialLicenseMultiplier = 1.5m });
await db.SaveChangesAsync();

var service = new ArtCommission.Infrastructure.Services.CommissionService(db, null!, null!, null!, new ArtCommission.Application.Payment.Common.VoucherCheckService(db));

async Task<CommissionDto> Create(string licenseType) => await service.CreateCommissionAsync(new CreateCommissionRequest
{
    CreatorId = creatorId,
    PackageId = packageId,
    LicenseType = licenseType,
    Title = licenseType
}, Guid.NewGuid());

var personal = await Create("Personal");
var commercial = await Create("Commercial");
if (personal.FinalPrice != 300m || personal.LicenseMultiplierApplied != 1m)
    throw new Exception("Personal pricing is incorrect.");
if (commercial.FinalPrice != 450m || commercial.LicenseMultiplierApplied != 1.5m)
    throw new Exception("Commercial pricing is incorrect.");
if ((await service.GetCommissionByIdAsync(commercial.Id, commercial.ClientId))!.Milestones.Sum(x => x.Price) != 450m)
    throw new Exception("Commercial milestones do not match the final price.");

try
{
    await service.CreateCommissionAsync(new CreateCommissionRequest
    {
        CreatorId = creatorId,
        PackageId = Guid.NewGuid(),
        LicenseType = "Personal",
        Title = "Invalid package"
    }, Guid.NewGuid());
    throw new Exception("Invalid package was accepted.");
}
catch (ArgumentException) { }

Console.WriteLine("License pricing checks passed.");
