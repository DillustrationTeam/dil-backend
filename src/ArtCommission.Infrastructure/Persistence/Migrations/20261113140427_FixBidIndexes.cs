using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixBidIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TỰ CHỮA DỮ LIỆU TRƯỚC KHI TẠO UNIQUE INDEX.
            // Index mới chỉ xét các dòng [IsDeleted] = 0. Nếu trong dữ liệu cũ có 2 bid
            // Leading cùng một phiên (dữ liệu seed/nhập tay, hoặc dòng cũ bị xoá mềm rồi
            // tạo lại), việc tạo index sẽ thất bại giữa đường và để lại migration ở trạng
            // thái nửa vời. Chuẩn hoá trước: giữ đúng 1 Leading (giá cao nhất, đặt sớm nhất),
            // các dòng còn lại chuyển Outbid — đúng ngữ nghĩa nghiệp vụ của lượt bị đè giá.
            migrationBuilder.Sql("""
                ;WITH dup AS (
                    SELECT Id,
                           ROW_NUMBER() OVER (
                               PARTITION BY AuctionId
                               ORDER BY Amount DESC, PlacedAt ASC, Id ASC) AS rn
                    FROM Bids
                    WHERE [Status] = 'Leading' AND IsDeleted = 0
                )
                UPDATE b
                   SET b.[Status] = 'Outbid',
                       b.UpdatedAt = SYSDATETIMEOFFSET()
                  FROM Bids b
                  JOIN dup d ON d.Id = b.Id
                 WHERE d.rn > 1;
                """);

            // DROP có điều kiện: máy của thành viên khác có thể chưa từng có index này
            // (DB tạo bằng EnsureCreated, hoặc schema cũ). migrationBuilder.DropIndex sẽ ném lỗi
            // "index does not exist" và chặn cả quá trình khởi động app.
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'UX_Bids_OneLeadingPerAuction'
                             AND object_id = OBJECT_ID('dbo.Bids'))
                    DROP INDEX UX_Bids_OneLeadingPerAuction ON dbo.Bids;
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_Bids_Bidder_Auction_PlacedAt'
                                 AND object_id = OBJECT_ID('dbo.Bids'))
                    CREATE INDEX IX_Bids_Bidder_Auction_PlacedAt
                        ON dbo.Bids (BidderId, AuctionId, PlacedAt);
                """);

            migrationBuilder.CreateIndex(
                name: "UX_Bids_OneLeadingPerAuction",
                table: "Bids",
                column: "AuctionId",
                unique: true,
                filter: "[Status] = 'Leading' AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Bids_Bidder_Auction_PlacedAt",
                table: "Bids");

            migrationBuilder.DropIndex(
                name: "UX_Bids_OneLeadingPerAuction",
                table: "Bids");

            migrationBuilder.CreateIndex(
                name: "UX_Bids_OneLeadingPerAuction",
                table: "Bids",
                column: "AuctionId",
                unique: true,
                filter: "[Status] = 'Leading'");
        }
    }
}
