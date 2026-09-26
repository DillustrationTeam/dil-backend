using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorApplicationAndAiVerifiedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAiVerified",
                table: "CreatorProfiles",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsNationalIdVerified",
                table: "CreatorApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryStyle",
                table: "CreatorApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpeedpaintVideoUrl",
                table: "CreatorApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAiVerified",
                table: "CreatorProfiles");

            migrationBuilder.DropColumn(
                name: "IsNationalIdVerified",
                table: "CreatorApplications");

            migrationBuilder.DropColumn(
                name: "PrimaryStyle",
                table: "CreatorApplications");

            migrationBuilder.DropColumn(
                name: "SpeedpaintVideoUrl",
                table: "CreatorApplications");
        }
    }
}
