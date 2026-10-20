using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PersistWorkroomTermsAcceptance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ClientTermsAcceptedAt",
                table: "Commissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientTermsVersion",
                table: "Commissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatorTermsAcceptedAt",
                table: "Commissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatorTermsVersion",
                table: "Commissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientTermsAcceptedAt",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "ClientTermsVersion",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CreatorTermsAcceptedAt",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CreatorTermsVersion",
                table: "Commissions");
        }
    }
}
