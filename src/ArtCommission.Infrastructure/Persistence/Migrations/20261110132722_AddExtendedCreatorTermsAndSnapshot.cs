using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExtendedCreatorTermsAndSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExtendedTermsJson",
                table: "CreatorTerms",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatorTermsSnapshotJson",
                table: "Commissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[ArtworkWatermarkReceipts]', N'U') IS NULL
BEGIN
    CREATE TABLE [ArtworkWatermarkReceipts] (
        [Id] uniqueidentifier NOT NULL,
        [ArtworkId] uniqueidentifier NOT NULL,
        [ReceiptToken] nvarchar(64) NOT NULL,
        [NumericPayload] bigint NULL,
        [ViewerUserId] uniqueidentifier NULL,
        [ViewerEmail] nvarchar(256) NULL,
        [ViewerName] nvarchar(256) NULL,
        [IpAddress] nvarchar(64) NOT NULL,
        [UserAgent] nvarchar(500) NULL,
        [SessionId] nvarchar(128) NULL,
        [WatermarkType] nvarchar(50) NOT NULL DEFAULT N'DwtForensic',
        [Status] nvarchar(50) NOT NULL DEFAULT N'Active',
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetimeoffset NOT NULL,
        [UpdatedAt] datetimeoffset NULL,
        [IsDeleted] bit NOT NULL,
        CONSTRAINT [PK_ArtworkWatermarkReceipts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ArtworkWatermarkReceipts_Artworks_ArtworkId] FOREIGN KEY ([ArtworkId]) REFERENCES [Artworks] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ArtworkWatermarkReceipts_ArtworkId] ON [ArtworkWatermarkReceipts] ([ArtworkId]);
    CREATE INDEX [IX_ArtworkWatermarkReceipts_CreatedAt] ON [ArtworkWatermarkReceipts] ([CreatedAt]);
    CREATE UNIQUE INDEX [IX_ArtworkWatermarkReceipts_ReceiptToken] ON [ArtworkWatermarkReceipts] ([ReceiptToken]);
    CREATE INDEX [IX_ArtworkWatermarkReceipts_ViewerUserId] ON [ArtworkWatermarkReceipts] ([ViewerUserId]);
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExtendedTermsJson",
                table: "CreatorTerms");

            migrationBuilder.DropColumn(
                name: "CreatorTermsSnapshotJson",
                table: "Commissions");
        }
    }
}
