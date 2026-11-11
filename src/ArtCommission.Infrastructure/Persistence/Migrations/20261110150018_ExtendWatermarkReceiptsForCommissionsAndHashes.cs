using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendWatermarkReceiptsForCommissionsAndHashes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArtworkWatermarkReceipts_Artworks_ArtworkId",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtworkId",
                table: "ArtworkWatermarkReceipts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "CommissionId",
                table: "ArtworkWatermarkReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "ArtworkWatermarkReceipts",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileSha256",
                table: "ArtworkWatermarkReceipts",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "MilestoneId",
                table: "ArtworkWatermarkReceipts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtworkWatermarkReceipts_CommissionId",
                table: "ArtworkWatermarkReceipts",
                column: "CommissionId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtworkWatermarkReceipts_FileSha256",
                table: "ArtworkWatermarkReceipts",
                column: "FileSha256");

            migrationBuilder.AddForeignKey(
                name: "FK_ArtworkWatermarkReceipts_Artworks_ArtworkId",
                table: "ArtworkWatermarkReceipts",
                column: "ArtworkId",
                principalTable: "Artworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArtworkWatermarkReceipts_Artworks_ArtworkId",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ArtworkWatermarkReceipts_CommissionId",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropIndex(
                name: "IX_ArtworkWatermarkReceipts_FileSha256",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropColumn(
                name: "CommissionId",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropColumn(
                name: "FileSha256",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropColumn(
                name: "MilestoneId",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.AlterColumn<Guid>(
                name: "ArtworkId",
                table: "ArtworkWatermarkReceipts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ArtworkWatermarkReceipts_Artworks_ArtworkId",
                table: "ArtworkWatermarkReceipts",
                column: "ArtworkId",
                principalTable: "Artworks",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
