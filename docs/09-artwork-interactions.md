# Artwork likes and bookmarks

Heart and bookmark now represent separate persistent relationships:

- Heart uses `ArtworkFavorites` (one row per user/artwork). Its count is calculated from those rows.
- Bookmark uses `CollectionArtworks` (one row per collection/artwork). Users choose their own private album, create an album, or remove individual saves.
- Removing a heart does not remove bookmarks; removing a bookmark does not remove a heart.

## API

`GET /api/v1/marketplace/artworks/{artworkId}/interactions` returns `isLiked`, `likeCount`, and the authenticated owner's `collectionIds`. Anonymous requests never return private collection IDs.

`PUT` / `DELETE /api/v1/marketplace/artworks/{artworkId}/favorite` set the desired heart state. The legacy `favorited` response field remains available.

`GET /api/v1/marketplace/me/collections/{collectionId}/artworks` lists approved, visible artwork from the requesting owner's collection.

`PUT` / `DELETE /api/v1/marketplace/me/collections/{collectionId}/artworks/{artworkId}` set the desired bookmark membership and return updated interactions. Other users' collection IDs return 404. Saving requires an approved, visible artwork; removing an existing save remains possible when the artwork becomes hidden.

Membership writes are idempotent. Composite database keys prevent duplicates; concurrent writes are accepted only when the final database membership matches the requested state.

## Existing data

Migration `20261003000000_PreserveLegacyArtworkBookmarks` copies old Favorites into a private “Tranh đã lưu trước đây” album per owner, since the former bookmark button wrote Favorites. Existing Favorites remain intact. It does not change the schema. Migration history makes the import run once; the normal migration transaction makes the import atomic. Downgrading deliberately retains imported albums and their content to avoid deleting subsequent user saves.

## Verification

Run from the backend directory:

```powershell
dotnet build --no-restore
dotnet run --project tests/CreatorFeatureChecks --no-restore
dotnet run --project tests/ArtworkInteractionSqlChecks --no-restore -- --local-sql
./scripts/Test-ArtworkInteractions.ps1
```

The SQL checks target local `DillustrationLocal`, create isolated fixtures, exercise concurrent mutations, and clean up their exact fixture IDs. The HTTP script requires the development API on port 5075 and seeded demo accounts; it restores the original like and removes only its temporary collection. Do not run these local checks against production.

Frontend validation: `npm run build` and targeted ESLint. Browser checks cover reload persistence, album creation, saved-artwork listing, and removing bookmarks while preserving likes.
