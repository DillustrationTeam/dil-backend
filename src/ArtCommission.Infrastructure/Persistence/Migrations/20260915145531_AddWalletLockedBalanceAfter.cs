using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWalletLockedBalanceAfter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LockedBalanceAfter",
                table: "WalletTransaction",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockedBalanceAfter",
                table: "WalletTransaction");
        }
    }
}
