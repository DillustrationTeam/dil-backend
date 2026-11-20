using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSellerDeliveryArrivalProof : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArrivalProofUrl",
                table: "AuctionDeliveries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SellerReportedDeliveredAt",
                table: "AuctionDeliveries",
                type: "datetimeoffset",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArrivalProofUrl",
                table: "AuctionDeliveries");

            migrationBuilder.DropColumn(
                name: "SellerReportedDeliveredAt",
                table: "AuctionDeliveries");
        }
    }
}
