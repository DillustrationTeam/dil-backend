using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261003000000_PreserveLegacyArtworkBookmarks")]
public partial class PreserveLegacyArtworkBookmarks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The old bookmark button wrote Favorites. Preserve those saves in a private album.
        migrationBuilder.Sql("""
            DECLARE @Imported TABLE (UserId uniqueidentifier PRIMARY KEY, CollectionId uniqueidentifier);
            INSERT INTO @Imported (UserId, CollectionId)
            SELECT DISTINCT UserId, NEWID() FROM (SELECT DISTINCT UserId FROM ArtworkFavorites) AS Owners;

            INSERT INTO PersonalCollections (Id, OwnerUserId, Name, IsPublic, CreatedAt, UpdatedAt, IsDeleted)
            SELECT CollectionId, UserId, N'Tranh đã lưu trước đây', 0, SYSDATETIMEOFFSET(), NULL, 0
            FROM @Imported;

            INSERT INTO CollectionArtworks (CollectionId, ArtworkId, CreatedAt)
            SELECT imported.CollectionId, favorite.ArtworkId, favorite.CreatedAt
            FROM ArtworkFavorites AS favorite
            INNER JOIN @Imported AS imported ON imported.UserId = favorite.UserId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately retain user data: imported albums may have gained new bookmarks.
    }
}
