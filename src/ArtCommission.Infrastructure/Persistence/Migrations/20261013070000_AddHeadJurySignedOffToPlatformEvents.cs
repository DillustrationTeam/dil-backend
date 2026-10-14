using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHeadJurySignedOffToPlatformEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "HeadJurySignedOffAt",
                table: "PlatformEvents",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHeadJurySignedOff",
                table: "PlatformEvents",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeadJurySignedOffAt",
                table: "PlatformEvents");

            migrationBuilder.DropColumn(
                name: "IsHeadJurySignedOff",
                table: "PlatformEvents");
        }
    }
}
