using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtworkModerationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AdultScore",
                table: "Artworks",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                table: "Artworks",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FlagReason",
                table: "Artworks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ModeratedAt",
                table: "Artworks",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ModerationNote",
                table: "Artworks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ModeratorId",
                table: "Artworks",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Resolution",
                table: "Artworks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SafeScore",
                table: "Artworks",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ViolenceScore",
                table: "Artworks",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdultScore",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "FlagReason",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "ModeratedAt",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "ModerationNote",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "ModeratorId",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "Resolution",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "SafeScore",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "ViolenceScore",
                table: "Artworks");
        }
    }
}
