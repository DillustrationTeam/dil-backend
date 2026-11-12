using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWatermarkVisualHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "VisualHash",
                table: "ArtworkWatermarkReceipts",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtworkWatermarkReceipts_VisualHash",
                table: "ArtworkWatermarkReceipts",
                column: "VisualHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ArtworkWatermarkReceipts_VisualHash",
                table: "ArtworkWatermarkReceipts");

            migrationBuilder.DropColumn(
                name: "VisualHash",
                table: "ArtworkWatermarkReceipts");
        }
    }
}
