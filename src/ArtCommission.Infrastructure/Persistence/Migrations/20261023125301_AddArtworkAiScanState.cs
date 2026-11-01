using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtworkAiScanState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "AiDetectionScore",
                table: "Artworks",
                type: "decimal(5,4)",
                precision: 5,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AiScanAttempts",
                table: "Artworks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "AiScanError",
                table: "Artworks",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiScanImageUrl",
                table: "Artworks",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiScanModel",
                table: "Artworks",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiScanProvider",
                table: "Artworks",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AiScanStatus",
                table: "Artworks",
                type: "nvarchar(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "Queued");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AiScanUpdatedAt",
                table: "Artworks",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiScanAttempts",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanError",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanImageUrl",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanModel",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanProvider",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanStatus",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "AiScanUpdatedAt",
                table: "Artworks");

            migrationBuilder.AlterColumn<decimal>(
                name: "AiDetectionScore",
                table: "Artworks",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(5,4)",
                oldPrecision: 5,
                oldScale: 4,
                oldNullable: true);
        }
    }
}
