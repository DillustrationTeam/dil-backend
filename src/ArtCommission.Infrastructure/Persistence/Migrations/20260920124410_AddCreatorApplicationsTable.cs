using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatorApplicationsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatorApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PortfolioLinks = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SocialLinks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdProofUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReviewedByModId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorApplications_Users_ApplicantId",
                        column: x => x.ApplicantId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreatorApplications_Users_ReviewedByModId",
                        column: x => x.ReviewedByModId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorApplications_ApplicantId_Status",
                table: "CreatorApplications",
                columns: new[] { "ApplicantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CreatorApplications_ReviewedByModId",
                table: "CreatorApplications",
                column: "ReviewedByModId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatorApplications");
        }
    }
}
