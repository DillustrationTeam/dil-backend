using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FilterSoftDeletedJuryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Juries_EventId' AND object_id = OBJECT_ID(N'[dbo].[Juries]')
                )
                    DROP INDEX [IX_Juries_EventId] ON [dbo].[Juries];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Juries_EventId_CreatorId' AND object_id = OBJECT_ID(N'[dbo].[Juries]')
                )
                    DROP INDEX [IX_Juries_EventId_CreatorId] ON [dbo].[Juries];

                UPDATE [dbo].[Juries]
                SET [IsHeadJury] = 0
                WHERE [IsDeleted] = 1 AND [IsHeadJury] = 1;

                WITH RankedHeadJuries AS (
                    SELECT
                        [Id],
                        ROW_NUMBER() OVER (
                            PARTITION BY [EventId]
                            ORDER BY COALESCE([UpdatedAt], [CreatedAt]) DESC, [CreatedAt] DESC, [Id] DESC
                        ) AS [rn]
                    FROM [dbo].[Juries]
                    WHERE [IsDeleted] = 0 AND [IsHeadJury] = 1
                )
                UPDATE j
                SET j.[IsHeadJury] = 0
                FROM [dbo].[Juries] j
                INNER JOIN RankedHeadJuries r ON r.[Id] = j.[Id]
                WHERE r.[rn] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId",
                table: "Juries",
                column: "EventId",
                unique: true,
                filter: "[IsHeadJury] = 1 AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries",
                columns: new[] { "EventId", "CreatorId" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Juries_EventId' AND object_id = OBJECT_ID(N'[dbo].[Juries]')
                )
                    DROP INDEX [IX_Juries_EventId] ON [dbo].[Juries];

                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE name = N'IX_Juries_EventId_CreatorId' AND object_id = OBJECT_ID(N'[dbo].[Juries]')
                )
                    DROP INDEX [IX_Juries_EventId_CreatorId] ON [dbo].[Juries];
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId",
                table: "Juries",
                column: "EventId",
                unique: true,
                filter: "[IsHeadJury] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Juries_EventId_CreatorId",
                table: "Juries",
                columns: new[] { "EventId", "CreatorId" },
                unique: true);
        }
    }
}
