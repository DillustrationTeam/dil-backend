using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilterSoftDeletedJuryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Juries_EventId",
                table: "Juries");

            migrationBuilder.DropIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId",
                table: "Juries",
                column: "EventId",
                unique: true,
                filter: "[IsHeadJury] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries",
                columns: new[] { "EventId", "CreatorId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Juries_EventId",
                table: "Juries");

            migrationBuilder.DropIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId",
                table: "Juries",
                column: "EventId",
                unique: true,
                filter: "[IsHeadJury] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries",
                columns: new[] { "EventId", "CreatorId" },
                unique: true);
        }
    }
}
