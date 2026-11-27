using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionRiskSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastRiskScanAt",
                table: "Auctions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "Auctions",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RiskScore",
                table: "Auctions",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionRiskAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    RiskLevel = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Summary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DecisionNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ModelVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "rules-v1"),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionRiskAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionRiskAssessments_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AuctionRiskSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BidId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignalCode = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    EvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DetectorVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "rules-v1"),
                    DetectedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionRiskSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionRiskSignals_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionRiskSignals_Bids_BidId",
                        column: x => x.BidId,
                        principalTable: "Bids",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AuctionRiskSignals_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RecommendationImpressions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Surface = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Score = table.Column<decimal>(type: "decimal(6,4)", precision: 6, scale: 4, nullable: false),
                    Rank = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ShownAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClickedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecommendationImpressions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecommendationImpressions_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserInterestProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProfileJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceEventCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Version = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    BuiltAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserInterestProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserInterestProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRiskAssessments_Queue",
                table: "AuctionRiskAssessments",
                columns: new[] { "Status", "RiskLevel", "Score" });

            migrationBuilder.CreateIndex(
                name: "UX_AuctionRiskAssessments_Auction_Live",
                table: "AuctionRiskAssessments",
                column: "AuctionId",
                unique: true,
                filter: "[Status] IN (N'Open', N'UnderReview')");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRiskSignals_Auction_DetectedAt",
                table: "AuctionRiskSignals",
                columns: new[] { "AuctionId", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRiskSignals_BidId",
                table: "AuctionRiskSignals",
                column: "BidId");

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRiskSignals_Code_DetectedAt",
                table: "AuctionRiskSignals",
                columns: new[] { "SignalCode", "DetectedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuctionRiskSignals_UserId",
                table: "AuctionRiskSignals",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationImpressions_Auction_ShownAt",
                table: "RecommendationImpressions",
                columns: new[] { "AuctionId", "ShownAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecommendationImpressions_User_Surface_ShownAt",
                table: "RecommendationImpressions",
                columns: new[] { "UserId", "Surface", "ShownAt" });

            migrationBuilder.CreateIndex(
                name: "UX_UserInterestProfiles_UserId",
                table: "UserInterestProfiles",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionRiskAssessments");

            migrationBuilder.DropTable(
                name: "AuctionRiskSignals");

            migrationBuilder.DropTable(
                name: "RecommendationImpressions");

            migrationBuilder.DropTable(
                name: "UserInterestProfiles");

            migrationBuilder.DropColumn(
                name: "LastRiskScanAt",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "Auctions");
        }
    }
}
