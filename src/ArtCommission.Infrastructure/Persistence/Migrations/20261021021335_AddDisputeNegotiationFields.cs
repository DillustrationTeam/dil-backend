using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeNegotiationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ClaimantRefundPercent",
                table: "Disputes",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespondentEvidenceUrls",
                table: "Disputes",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RespondentRefundPercent",
                table: "Disputes",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondentRespondedAt",
                table: "Disputes",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RespondentResponse",
                table: "Disputes",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClaimantRefundPercent",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RespondentEvidenceUrls",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RespondentRefundPercent",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RespondentRespondedAt",
                table: "Disputes");

            migrationBuilder.DropColumn(
                name: "RespondentResponse",
                table: "Disputes");
        }
    }
}
