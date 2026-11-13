using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Seller penalty ladder for confirmed auction delivery disputes.
    ///
    /// Written by hand instead of scaffolded: the shared model snapshot in this branch was
    /// behind the migrations merged from develop, so the scaffolded version replayed their
    /// changes (SubmissionDisputes / ArtworkCategories) and failed on duplicate columns.
    /// This migration only touches what the penalty feature needs.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261112160000_AuctionDeliveryPenalties")]
    public partial class AuctionDeliveryPenalties : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EffectiveFeePercent",
                table: "Auctions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeSurchargePercent",
                table: "Auctions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionPenalties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SellerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StrikeLevel = table.Column<int>(type: "int", nullable: false),
                    SurchargePercent = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    SessionsTotal = table.Column<int>(type: "int", nullable: false),
                    SessionsUsed = table.Column<int>(type: "int", nullable: false),
                    BanUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EscrowReleasedAtStrike = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceAuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceDisputeResolvedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionPenalties", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionPenalties_Auctions_SourceAuctionId",
                        column: x => x.SourceAuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPenalties_SellerId_Status",
                table: "AuctionPenalties",
                columns: new[] { "SellerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPenalties_SourceAuctionId",
                table: "AuctionPenalties",
                column: "SourceAuctionId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionPenalties_SourceDeliveryId",
                table: "AuctionPenalties",
                column: "SourceDeliveryId",
                unique: true,
                filter: "[SourceDeliveryId] IS NOT NULL AND [IsDeleted] = 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AuctionPenalties");

            migrationBuilder.DropColumn(name: "FeeSurchargePercent", table: "Auctions");
            migrationBuilder.DropColumn(name: "EffectiveFeePercent", table: "Auctions");
        }
    }
}
