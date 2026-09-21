using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UniqueCommissionReviewAndDispute : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CommissionId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_CommissionId",
                table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CommissionId",
                table: "Reviews",
                column: "CommissionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_CommissionId",
                table: "Disputes",
                column: "CommissionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reviews_CommissionId",
                table: "Reviews");

            migrationBuilder.DropIndex(
                name: "IX_Disputes_CommissionId",
                table: "Disputes");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_CommissionId",
                table: "Reviews",
                column: "CommissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Disputes_CommissionId",
                table: "Disputes",
                column: "CommissionId");
        }
    }
}
