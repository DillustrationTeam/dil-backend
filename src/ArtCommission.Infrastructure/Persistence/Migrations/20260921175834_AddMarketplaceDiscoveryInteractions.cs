using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketplaceDiscoveryInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvailableSlots",
                table: "CreatorProfiles",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LicenseType",
                table: "Artworks",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Personal");

            migrationBuilder.AddColumn<decimal>(
                name: "StartingPrice",
                table: "Artworks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Style",
                table: "Artworks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArtworkComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtworkComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtworkComments_Artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ArtworkFavorites",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtworkFavorites", x => new { x.UserId, x.ArtworkId });
                    table.ForeignKey(
                        name: "FK_ArtworkFavorites_Artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CommissionServices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DeliveryDays = table.Column<int>(type: "int", nullable: false),
                    MaxRevisions = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommissionServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommissionServices_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReviewerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorReviews_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonalCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonalCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CollectionArtworks",
                columns: table => new
                {
                    CollectionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionArtworks", x => new { x.CollectionId, x.ArtworkId });
                    table.ForeignKey(
                        name: "FK_CollectionArtworks_Artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CollectionArtworks_PersonalCollections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "PersonalCollections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtworkComments_ArtworkId_CreatedAt",
                table: "ArtworkComments",
                columns: new[] { "ArtworkId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtworkFavorites_ArtworkId",
                table: "ArtworkFavorites",
                column: "ArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionArtworks_ArtworkId",
                table: "CollectionArtworks",
                column: "ArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_CommissionServices_CreatorProfileId_IsActive",
                table: "CommissionServices",
                columns: new[] { "CreatorProfileId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorReviews_CreatorProfileId_CreatedAt",
                table: "CreatorReviews",
                columns: new[] { "CreatorProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonalCollections_OwnerUserId_CreatedAt",
                table: "PersonalCollections",
                columns: new[] { "OwnerUserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtworkComments");

            migrationBuilder.DropTable(
                name: "ArtworkFavorites");

            migrationBuilder.DropTable(
                name: "CollectionArtworks");

            migrationBuilder.DropTable(
                name: "CommissionServices");

            migrationBuilder.DropTable(
                name: "CreatorReviews");

            migrationBuilder.DropTable(
                name: "PersonalCollections");

            migrationBuilder.DropColumn(
                name: "AvailableSlots",
                table: "CreatorProfiles");

            migrationBuilder.DropColumn(
                name: "LicenseType",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "StartingPrice",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "Style",
                table: "Artworks");
        }
    }
}
