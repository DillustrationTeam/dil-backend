using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuctionPhysicalDeliveryAndReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VoucherRedemption_Voucher_User",
                table: "VoucherRedemption");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Auctions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Scheduled",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldDefaultValue: "Scheduled");

            migrationBuilder.AddColumn<bool>(
                name: "HasPhysicalCopy",
                table: "Auctions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PhysicalChoiceDeadline",
                table: "Auctions",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhysicalDescription",
                table: "Auctions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresPhysicalDelivery",
                table: "Auctions",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuctionDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AuctionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RecipientPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShippingAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    NoteToSeller = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CarrierName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TrackingUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ShippingReceiptUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PackageImageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DeliveryReceiptUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShippedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DisputeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisputeEvidenceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuctionDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuctionDeliveries_Auctions_AuctionId",
                        column: x => x.AuctionId,
                        principalTable: "Auctions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuctionDeliveries_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "UX_VoucherRedemption_Voucher_User",
                table: "VoucherRedemption",
                columns: new[] { "VoucherId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuctionDeliveries_RecipientId",
                table: "AuctionDeliveries",
                column: "RecipientId");

            migrationBuilder.CreateIndex(
                name: "UX_AuctionDeliveries_Auction",
                table: "AuctionDeliveries",
                column: "AuctionId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuctionDeliveries");

            migrationBuilder.DropIndex(
                name: "UX_VoucherRedemption_Voucher_User",
                table: "VoucherRedemption");

            migrationBuilder.DropColumn(
                name: "HasPhysicalCopy",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "PhysicalChoiceDeadline",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "PhysicalDescription",
                table: "Auctions");

            migrationBuilder.DropColumn(
                name: "RequiresPhysicalDelivery",
                table: "Auctions");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Auctions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Scheduled",
                oldClrType: typeof(string),
                oldType: "nvarchar(30)",
                oldMaxLength: 30,
                oldDefaultValue: "Scheduled");

            migrationBuilder.CreateIndex(
                name: "IX_VoucherRedemption_Voucher_User",
                table: "VoucherRedemption",
                columns: new[] { "VoucherId", "UserId" });
        }
    }
}
