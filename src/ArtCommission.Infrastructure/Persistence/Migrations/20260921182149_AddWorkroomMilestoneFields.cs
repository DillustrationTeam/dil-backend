using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkroomMilestoneFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'[CreatorProfiles]', N'RateCard') IS NOT NULL
                    ALTER TABLE [CreatorProfiles] DROP COLUMN [RateCard];
                """);

            migrationBuilder.AddColumn<string>(
                name: "AttachedImagesJson",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RespondedAt",
                table: "Reviews",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatorNote",
                table: "Milestones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxRevisions",
                table: "Milestones",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OriginalWipUrl",
                table: "Milestones",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RevisionFeedback",
                table: "Milestones",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttachedImagesJson",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "CreatorNote",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "MaxRevisions",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "OriginalWipUrl",
                table: "Milestones");

            migrationBuilder.DropColumn(
                name: "RevisionFeedback",
                table: "Milestones");

            migrationBuilder.AddColumn<string>(
                name: "RateCard",
                table: "CreatorProfiles",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
