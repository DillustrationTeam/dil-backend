using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AuctionConcurrencyAndAutoBid : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(
            name: "RowVersion",
            table: "Auctions",
            type: "rowversion",
            rowVersion: true,
            nullable: false);

        migrationBuilder.CreateTable(
            name: "AuctionAutoBids",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                BidderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                MaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                RegisteredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AuctionAutoBids", x => x.Id);
                table.ForeignKey(
                    name: "FK_AuctionAutoBids_Auctions_AuctionId",
                    column: x => x.AuctionId,
                    principalTable: "Auctions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AuctionAutoBids_Users_BidderId",
                    column: x => x.BidderId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_AuctionAutoBids_BidderId",
            table: "AuctionAutoBids",
            column: "BidderId");
        migrationBuilder.CreateIndex(
            name: "IX_AuctionAutoBids_ProxyOrder",
            table: "AuctionAutoBids",
            columns: new[] { "AuctionId", "MaxAmount", "RegisteredAt" });
        migrationBuilder.CreateIndex(
            name: "UX_AuctionAutoBids_Auction_Bidder",
            table: "AuctionAutoBids",
            columns: new[] { "AuctionId", "BidderId" },
            unique: true);

        // Lấy trần mới nhất của từng user/auction, nhưng giữ PlacedAt sớm nhất để hòa trần
        // vẫn chọn đúng người đã đăng ký auto-bid trước.
        migrationBuilder.Sql("""
            ;WITH RankedAutoBids AS
            (
                SELECT AuctionId, BidderId, MaxAutoBid,
                       MIN(PlacedAt) OVER (PARTITION BY AuctionId, BidderId) AS RegisteredAt,
                       ROW_NUMBER() OVER (PARTITION BY AuctionId, BidderId ORDER BY PlacedAt DESC, Id DESC) AS LatestRank
                FROM dbo.Bids
                WHERE IsAuto = 1 AND MaxAutoBid IS NOT NULL AND IsDeleted = 0
            )
            INSERT INTO dbo.AuctionAutoBids
                (Id, AuctionId, BidderId, MaxAmount, RegisteredAt, CreatedAt, UpdatedAt, IsDeleted)
            SELECT NEWID(), AuctionId, BidderId, MaxAutoBid, RegisteredAt, RegisteredAt, NULL, 0
            FROM RankedAutoBids
            WHERE LatestRank = 1;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AuctionAutoBids");
        migrationBuilder.DropColumn(name: "RowVersion", table: "Auctions");
    }
}
