using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Commissions_ClientId_CreatedAt",
                table: "Commissions",
                columns: new[] { "ClientId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Commissions_CreatorId_CreatedAt",
                table: "Commissions",
                columns: new[] { "CreatorId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_Commissions_Status_UpdatedAt",
                table: "Commissions",
                columns: new[] { "Status", "UpdatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Commissions_ClientId_CreatedAt",
                table: "Commissions");

            migrationBuilder.DropIndex(
                name: "IX_Commissions_CreatorId_CreatedAt",
                table: "Commissions");

            migrationBuilder.DropIndex(
                name: "IX_Commissions_Status_UpdatedAt",
                table: "Commissions");
        }
    }
}
