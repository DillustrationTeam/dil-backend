using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtTradeIdToChatRooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "TradeDisputes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArtTradeId",
                table: "ChatRooms",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReceiverAcceptedAt",
                table: "ArtTrades",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReceiverResponseNote",
                table: "ArtTrades",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatRooms_ArtTradeId",
                table: "ChatRooms",
                column: "ArtTradeId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatRooms_ArtTrades_ArtTradeId",
                table: "ChatRooms",
                column: "ArtTradeId",
                principalTable: "ArtTrades",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatRooms_ArtTrades_ArtTradeId",
                table: "ChatRooms");

            migrationBuilder.DropIndex(
                name: "IX_ChatRooms_ArtTradeId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "TradeDisputes");

            migrationBuilder.DropColumn(
                name: "ArtTradeId",
                table: "ChatRooms");

            migrationBuilder.DropColumn(
                name: "ReceiverAcceptedAt",
                table: "ArtTrades");

            migrationBuilder.DropColumn(
                name: "ReceiverResponseNote",
                table: "ArtTrades");
        }
    }
}
