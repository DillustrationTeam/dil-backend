using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PreserveSubmissionDisputeHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionDisputes_EventSubmissions_SubmissionId",
                table: "SubmissionDisputes");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmissionId",
                table: "SubmissionDisputes",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "ArtworkImageUrlSnapshot",
                table: "SubmissionDisputes",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmissionTitleSnapshot",
                table: "SubmissionDisputes",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionDisputes_EventSubmissions_SubmissionId",
                table: "SubmissionDisputes",
                column: "SubmissionId",
                principalTable: "EventSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SubmissionDisputes_EventSubmissions_SubmissionId",
                table: "SubmissionDisputes");

            migrationBuilder.DropColumn(
                name: "ArtworkImageUrlSnapshot",
                table: "SubmissionDisputes");

            migrationBuilder.DropColumn(
                name: "SubmissionTitleSnapshot",
                table: "SubmissionDisputes");

            migrationBuilder.AlterColumn<Guid>(
                name: "SubmissionId",
                table: "SubmissionDisputes",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_SubmissionDisputes_EventSubmissions_SubmissionId",
                table: "SubmissionDisputes",
                column: "SubmissionId",
                principalTable: "EventSubmissions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
