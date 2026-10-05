using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToApplicationUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LoginUsername",
                table: "Users",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedLoginUsername",
                table: "Users",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedLoginUsername",
                table: "Users",
                column: "NormalizedLoginUsername",
                unique: true,
                filter: "[NormalizedLoginUsername] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedLoginUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LoginUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedLoginUsername",
                table: "Users");
        }
    }
}
