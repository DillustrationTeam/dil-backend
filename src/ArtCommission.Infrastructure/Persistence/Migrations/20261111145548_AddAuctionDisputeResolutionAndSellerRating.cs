using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionDisputeResolutionAndSellerRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AuctionId",
                table: "CreatorReviews",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "CreatorReviews",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeResolution",
                table: "AuctionDeliveries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DisputeResolvedAt",
                table: "AuctionDeliveries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisputeResolvedNote",
                table: "AuctionDeliveries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPostPayoutDispute",
                table: "AuctionDeliveries",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CreatorReviews_AuctionId_ReviewerUserId",
                table: "CreatorReviews",
                columns: new[] { "AuctionId", "ReviewerUserId" },
                unique: true,
                filter: "[AuctionId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CreatorReviews_AuctionId_ReviewerUserId",
                table: "CreatorReviews");

            migrationBuilder.DropColumn(
                name: "AuctionId",
                table: "CreatorReviews");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "CreatorReviews");

            migrationBuilder.DropColumn(
                name: "DisputeResolution",
                table: "AuctionDeliveries");

            migrationBuilder.DropColumn(
                name: "DisputeResolvedAt",
                table: "AuctionDeliveries");

            migrationBuilder.DropColumn(
                name: "DisputeResolvedNote",
                table: "AuctionDeliveries");

            migrationBuilder.DropColumn(
                name: "IsPostPayoutDispute",
                table: "AuctionDeliveries");
        }
    }
}
