using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArtTradeAndTradeDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ArtTradeId",
                table: "ArtworkOwnerships",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArtTrades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InitiatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InitiatorRequestNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReceiverRequestNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    InitiatorArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceiverArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InitiatorOriginalFileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InitiatorPreviewUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    InitiatorSubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReceiverOriginalFileUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiverPreviewUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ReceiverSubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsInitiatorConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    InitiatorConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsReceiverConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    ReceiverConfirmedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SubmissionDeadlineAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConfirmationDeadlineAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CancelledById = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CancellationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtTrades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArtTrades_Artworks_InitiatorArtworkId",
                        column: x => x.InitiatorArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtTrades_Artworks_ReceiverArtworkId",
                        column: x => x.ReceiverArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtTrades_Users_CancelledById",
                        column: x => x.CancelledById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtTrades_Users_InitiatorId",
                        column: x => x.InitiatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtTrades_Users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeDisputes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtTradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RaisedById = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EvidenceUrls = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Resolution = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeDisputes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeDisputes_ArtTrades_ArtTradeId",
                        column: x => x.ArtTradeId,
                        principalTable: "ArtTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TradeSlotFreezes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtTradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FrozenFrom = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FrozenUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRevoked = table.Column<bool>(type: "bit", nullable: false),
                    RevokedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RevokedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RevokedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeSlotFreezes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeSlotFreezes_ArtTrades_ArtTradeId",
                        column: x => x.ArtTradeId,
                        principalTable: "ArtTrades",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeSlotFreezes_Users_RevokedByAdminId",
                        column: x => x.RevokedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TradeSlotFreezes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_CancelledById",
                table: "ArtTrades",
                column: "CancelledById");

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_InitiatorArtworkId",
                table: "ArtTrades",
                column: "InitiatorArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_InitiatorId_CreatedAt",
                table: "ArtTrades",
                columns: new[] { "InitiatorId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_ReceiverArtworkId",
                table: "ArtTrades",
                column: "ReceiverArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_ReceiverId_CreatedAt",
                table: "ArtTrades",
                columns: new[] { "ReceiverId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ArtTrades_Status_UpdatedAt",
                table: "ArtTrades",
                columns: new[] { "Status", "UpdatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_TradeDisputes_ArtTradeId",
                table: "TradeDisputes",
                column: "ArtTradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeSlotFreezes_ArtTradeId",
                table: "TradeSlotFreezes",
                column: "ArtTradeId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeSlotFreezes_RevokedByAdminId",
                table: "TradeSlotFreezes",
                column: "RevokedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_TradeSlotFreezes_User_Active",
                table: "TradeSlotFreezes",
                columns: new[] { "UserId", "FrozenUntil", "IsRevoked", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TradeDisputes");

            migrationBuilder.DropTable(
                name: "TradeSlotFreezes");

            migrationBuilder.DropTable(
                name: "ArtTrades");

            migrationBuilder.DropColumn(
                name: "ArtTradeId",
                table: "ArtworkOwnerships");
        }
    }
}
