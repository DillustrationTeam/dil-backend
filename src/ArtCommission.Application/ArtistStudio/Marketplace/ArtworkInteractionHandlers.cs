using ArtCommission.Application.ArtistStudio.DTOs;
using ArtCommission.Application.Common.Interfaces;
using ArtCommission.Domain.Entities.ArtistStudio;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ArtCommission.Application.ArtistStudio.Marketplace;

public record ArtworkInteractionsDto(bool IsLiked, int LikeCount, IReadOnlyList<Guid> CollectionIds);
public record GetArtworkInteractionsQuery(Guid ArtworkId, Guid UserId) : IRequest<ArtworkInteractionsDto?>;
public record GetCollectionArtworksQuery(Guid CollectionId, Guid UserId) : IRequest<IReadOnlyList<ArtworkDto>?>;
public record SetArtworkLikeCommand(Guid ArtworkId, Guid UserId, bool Liked) : IRequest<ArtworkInteractionsDto?>;
public record SetCollectionArtworkCommand(Guid CollectionId, Guid ArtworkId, Guid UserId, bool Saved) : IRequest<ArtworkInteractionsDto?>;

public sealed class ArtworkInteractionHandler :
    IRequestHandler<GetArtworkInteractionsQuery, ArtworkInteractionsDto?>,
    IRequestHandler<GetCollectionArtworksQuery, IReadOnlyList<ArtworkDto>?>,
    IRequestHandler<SetArtworkLikeCommand, ArtworkInteractionsDto?>,
    IRequestHandler<SetCollectionArtworkCommand, ArtworkInteractionsDto?>
{
    private readonly IApplicationDbContext _db;
    public ArtworkInteractionHandler(IApplicationDbContext db) => _db = db;

    private Task<bool> IsVisible(Guid id, CancellationToken ct) => _db.Artworks.AnyAsync(a =>
        a.Id == id && !a.IsDeleted && a.ModerationStatus == "Approved" && !a.CreatorProfile!.IsDeleted, ct);

    public async Task<ArtworkInteractionsDto?> Handle(GetArtworkInteractionsQuery q, CancellationToken ct)
    {
        if (!await IsVisible(q.ArtworkId, ct)) return null;
        var liked = q.UserId != Guid.Empty && await _db.ArtworkFavorites.AnyAsync(f => f.UserId == q.UserId && f.ArtworkId == q.ArtworkId, ct);
        var count = await _db.ArtworkFavorites.CountAsync(f => f.ArtworkId == q.ArtworkId, ct);
        var collections = q.UserId == Guid.Empty ? new List<Guid>() : await _db.CollectionArtworks.AsNoTracking()
            .Where(c => c.ArtworkId == q.ArtworkId && c.Collection!.OwnerUserId == q.UserId && !c.Collection.IsDeleted)
            .Select(c => c.CollectionId).ToListAsync(ct);
        return new(liked, count, collections);
    }

    public async Task<IReadOnlyList<ArtworkDto>?> Handle(GetCollectionArtworksQuery q, CancellationToken ct)
    {
        if (q.UserId == Guid.Empty || !await _db.PersonalCollections.AnyAsync(c => c.Id == q.CollectionId && c.OwnerUserId == q.UserId && !c.IsDeleted, ct)) return null;
        return await _db.CollectionArtworks.AsNoTracking().Where(c => c.CollectionId == q.CollectionId
            && !c.Artwork!.IsDeleted && c.Artwork.ModerationStatus == "Approved" && !c.Artwork.CreatorProfile!.IsDeleted)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ArtworkDto(c.Artwork!.Id, c.Artwork.CreatorProfileId, c.Artwork.Title, c.Artwork.Description,
                c.Artwork.ImageUrl, c.Artwork.ThumbnailUrl, c.Artwork.IsAiGenerated, c.Artwork.AiDetectionScore,
                c.Artwork.ModerationStatus, c.Artwork.ViewCount, c.Artwork.CreatedAt,
                c.Artwork.ArtworkTags.Select(t => t.Tag!.Name).ToList())).ToListAsync(ct);
    }

    public async Task<ArtworkInteractionsDto?> Handle(SetArtworkLikeCommand c, CancellationToken ct)
    {
        if (c.UserId == Guid.Empty || !await IsVisible(c.ArtworkId, ct)) return null;
        var item = await _db.ArtworkFavorites.SingleOrDefaultAsync(f => f.UserId == c.UserId && f.ArtworkId == c.ArtworkId, ct);
        if (c.Liked && item is null)
        {
            item = new ArtworkFavorite { UserId = c.UserId, ArtworkId = c.ArtworkId };
            _db.ArtworkFavorites.Add(item);
            await SaveMembership(item, () => _db.ArtworkFavorites.AnyAsync(f => f.UserId == c.UserId && f.ArtworkId == c.ArtworkId, ct), true, ct);
        }
        else if (!c.Liked && item is not null)
        {
            _db.ArtworkFavorites.Remove(item);
            await SaveMembership(item, () => _db.ArtworkFavorites.AnyAsync(f => f.UserId == c.UserId && f.ArtworkId == c.ArtworkId, ct), false, ct);
        }
        return await Handle(new GetArtworkInteractionsQuery(c.ArtworkId, c.UserId), ct);
    }

    public async Task<ArtworkInteractionsDto?> Handle(SetCollectionArtworkCommand c, CancellationToken ct)
    {
        if (c.UserId == Guid.Empty || !await _db.PersonalCollections.AnyAsync(p => p.Id == c.CollectionId && p.OwnerUserId == c.UserId && !p.IsDeleted, ct)) return null;
        // Removal remains available even if a previously saved artwork has become hidden.
        if (c.Saved && !await IsVisible(c.ArtworkId, ct)) return null;
        var item = await _db.CollectionArtworks.SingleOrDefaultAsync(a => a.CollectionId == c.CollectionId && a.ArtworkId == c.ArtworkId, ct);
        if (c.Saved && item is null)
        {
            item = new CollectionArtwork { CollectionId = c.CollectionId, ArtworkId = c.ArtworkId };
            _db.CollectionArtworks.Add(item);
            await SaveMembership(item, () => _db.CollectionArtworks.AnyAsync(a => a.CollectionId == c.CollectionId && a.ArtworkId == c.ArtworkId, ct), true, ct);
        }
        else if (!c.Saved && item is not null)
        {
            _db.CollectionArtworks.Remove(item);
            await SaveMembership(item, () => _db.CollectionArtworks.AnyAsync(a => a.CollectionId == c.CollectionId && a.ArtworkId == c.ArtworkId, ct), false, ct);
        }
        return await Handle(new GetArtworkInteractionsQuery(c.ArtworkId, c.UserId), ct)
            ?? new ArtworkInteractionsDto(false, 0, Array.Empty<Guid>());
    }

    // Composite primary keys prevent duplicates across concurrent requests. Only suppress
    // a failed write when another request has already established the requested state.
    private async Task SaveMembership<T>(T entity, Func<Task<bool>> exists, bool desired, CancellationToken ct) where T : class
    {
        try { await _db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            _db.Entry(entity).State = EntityState.Detached;
            if (await exists() != desired) throw;
        }
    }
}
