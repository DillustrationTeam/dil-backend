using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIsDisqualifiedToEventSubmissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDisqualified",
                table: "EventSubmissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_EventSubmissions_EventId_IsDisqualified",
                table: "EventSubmissions",
                columns: new[] { "EventId", "IsDisqualified" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EventSubmissions_EventId_IsDisqualified",
                table: "EventSubmissions");

            migrationBuilder.DropColumn(
                name: "IsDisqualified",
                table: "EventSubmissions");
        }
    }
}
