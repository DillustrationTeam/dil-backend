using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistCommissionCancellationReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Older local builds may already have these columns from the cancellation fix.
            migrationBuilder.Sql("IF COL_LENGTH('Commissions', 'CancellationDetails') IS NULL ALTER TABLE [Commissions] ADD [CancellationDetails] nvarchar(2000) NULL;");
            migrationBuilder.Sql("IF COL_LENGTH('Commissions', 'CancellationReason') IS NULL ALTER TABLE [Commissions] ADD [CancellationReason] nvarchar(500) NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationDetails",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "Commissions");
        }
    }
}
