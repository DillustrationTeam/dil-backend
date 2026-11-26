using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAiReferenceSketch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAiReferenceSketch",
                table: "CreatorTerms",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AiReferenceImagesJson",
                table: "Commissions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFreestyle",
                table: "Commissions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LayoutHint",
                table: "Commissions",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AiReferenceGenerations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BriefText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LayoutHint = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    VariantsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Model = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    ProgressPercent = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    BlockReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Error = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReferenceGenerations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiReferenceImages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GenerationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VariantIndex = table.Column<int>(type: "int", nullable: false),
                    TempStorageKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PermanentUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LabelVi = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LabelEn = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CommissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiReferenceImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AiReferenceImages_AiReferenceGenerations_GenerationId",
                        column: x => x.GenerationId,
                        principalTable: "AiReferenceGenerations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiReferenceGenerations_Client_Creator_CreatedAt",
                table: "AiReferenceGenerations",
                columns: new[] { "ClientId", "CreatorProfileId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AiReferenceImages_CommissionId",
                table: "AiReferenceImages",
                column: "CommissionId");

            migrationBuilder.CreateIndex(
                name: "IX_AiReferenceImages_Generation_VariantIndex",
                table: "AiReferenceImages",
                columns: new[] { "GenerationId", "VariantIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiReferenceImages");

            migrationBuilder.DropTable(
                name: "AiReferenceGenerations");

            migrationBuilder.DropColumn(
                name: "AllowAiReferenceSketch",
                table: "CreatorTerms");

            migrationBuilder.DropColumn(
                name: "AiReferenceImagesJson",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "IsFreestyle",
                table: "Commissions");

            migrationBuilder.DropColumn(
                name: "LayoutHint",
                table: "Commissions");
        }
    }
}
