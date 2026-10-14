using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissionCancellationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestExpiresAt",
                table: "Commissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRequestStatus",
                table: "Commissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CancellationRequestedAt",
                table: "Commissions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancellationRequestedById",
                table: "Commissions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancellationRequestedByRole",
                table: "Commissions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationRequestExpiresAt",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CancellationRequestStatus",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedAt",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedById",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "CancellationRequestedByRole",
                table: "Commissions");
        }
    }
}
