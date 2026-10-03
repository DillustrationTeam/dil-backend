using ArtCommission.Application.ArtistStudio.Marketplace;
using ArtCommission.Domain.Entities.ArtistStudio;
using ArtCommission.Domain.Entities.Identity;
using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// Explicit opt-in: creates isolated fixtures in the named local database and removes them in finally.
if (!args.Contains("--local-sql")) throw new ArgumentException("Pass --local-sql to test SQL Server localhost/DillustrationLocal.");
var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(
    "Server=localhost;Database=DillustrationLocal;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true").Options;
await using var fixture = new AppDbContext(options);
var user = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Interaction SQL test", UserName = "interaction-" + Guid.NewGuid(), Email = "interaction-" + Guid.NewGuid() + "@test.invalid" };
var other = new ApplicationUser { Id = Guid.NewGuid(), FullName = "Other SQL test", UserName = "interaction-" + Guid.NewGuid(), Email = "interaction-" + Guid.NewGuid() + "@test.invalid" };
var creator = new CreatorProfile { UserId = user.Id, DisplayName = "Interaction SQL test" };
var artwork = new Artwork { CreatorProfileId = creator.Id, Title = "Interaction SQL fixture", ImageUrl = "/weblogo.png", ModerationStatus = "Approved" };
var album = new PersonalCollection { OwnerUserId = user.Id, Name = "Interaction SQL fixture" };
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; }
async Task Like(bool desired)
{
    await using var db = new AppDbContext(options);
    var state = await new ArtworkInteractionHandler(db).Handle(new SetArtworkLikeCommand(artwork.Id, user.Id, desired), default);
    if (state is null) throw new Exception("Like request failed");
}
async Task Save(bool desired)
{
    await using var db = new AppDbContext(options);
    var state = await new ArtworkInteractionHandler(db).Handle(new SetCollectionArtworkCommand(album.Id, artwork.Id, user.Id, desired), default);
    if (state is null) throw new Exception("Bookmark request failed");
}
try
{
    fixture.Users.AddRange(user, other);
    fixture.CreatorProfiles.Add(creator);
    fixture.Artworks.Add(artwork);
    fixture.PersonalCollections.Add(album);
    await fixture.SaveChangesAsync();
    await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Like(true)));
    Check(await fixture.ArtworkFavorites.CountAsync(f => f.ArtworkId == artwork.Id) == 1, "Concurrent likes create exactly one row");
    await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Save(true)));
    Check(await fixture.CollectionArtworks.CountAsync(c => c.CollectionId == album.Id) == 1, "Concurrent saves create exactly one row");
    await using (var reloaded = new AppDbContext(options))
    {
        var handler = new ArtworkInteractionHandler(reloaded);
        var state = await handler.Handle(new GetArtworkInteractionsQuery(artwork.Id, user.Id), default);
        Check(state is { IsLiked: true, LikeCount: 1 } && state.CollectionIds.Contains(album.Id), "Fresh context reload preserves both states");
        Check((await handler.Handle(new GetCollectionArtworksQuery(album.Id, user.Id), default))?.Count == 1, "SQL collection contents query succeeds");
        var queries = new MarketplaceQueryHandler(reloaded);
        Check((await queries.Handle(new GetMyFavoritesQuery(user.Id), default)).Single().Id == artwork.Id, "SQL liked library query succeeds");
        Check((await queries.Handle(new GetMyCollectionsQuery(user.Id), default)).Single().ArtworkCount == 1, "SQL collection counter query succeeds");
        Check(await handler.Handle(new SetCollectionArtworkCommand(album.Id, artwork.Id, other.Id, false), default) is null, "SQL owner permission blocks other user");
    }
    await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Like(false)));
    Check(await fixture.ArtworkFavorites.CountAsync(f => f.ArtworkId == artwork.Id) == 0, "Concurrent unlikes are idempotent");
    Check(await fixture.CollectionArtworks.CountAsync(c => c.CollectionId == album.Id) == 1, "Unlike preserves bookmark");
    await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => Save(false)));
    Check(await fixture.CollectionArtworks.CountAsync(c => c.CollectionId == album.Id) == 0, "Concurrent removes are idempotent");
    Console.WriteLine($"Passed {checks} SQL interaction checks (12 concurrent requests per mutation).");
}
finally
{
    // Each delete is restricted to this run's random fixture IDs, including cleanup after a failed check.
    await fixture.CollectionArtworks.Where(c => c.CollectionId == album.Id).ExecuteDeleteAsync();
    await fixture.ArtworkFavorites.Where(f => f.ArtworkId == artwork.Id).ExecuteDeleteAsync();
    await fixture.PersonalCollections.Where(c => c.Id == album.Id).ExecuteDeleteAsync();
    await fixture.Artworks.Where(a => a.Id == artwork.Id).ExecuteDeleteAsync();
    await fixture.CreatorProfiles.Where(c => c.Id == creator.Id).ExecuteDeleteAsync();
    await fixture.Users.Where(u => u.Id == user.Id || u.Id == other.Id).ExecuteDeleteAsync();
    Console.WriteLine("Removed SQL test fixtures.");
}
