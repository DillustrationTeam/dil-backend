using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncLocalRuntimeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {






            migrationBuilder.CreateTable(
                name: "CreatorAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AssetType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AssetUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorAssets_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorAutoReplySettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    BriefTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorAutoReplySettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorAutoReplySettings_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorFaqs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Question = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Answer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorFaqs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorFaqs_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });


            migrationBuilder.CreateTable(
                name: "CreatorTerms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommercialLicenseMultiplier = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RevisionPolicy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CancellationPolicy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorTerms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorTerms_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatorWorkItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Stage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatorWorkItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatorWorkItems_CreatorProfiles_CreatorProfileId",
                        column: x => x.CreatorProfileId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });







            migrationBuilder.CreateIndex(
                name: "IX_CreatorAssets_CreatorProfileId",
                table: "CreatorAssets",
                column: "CreatorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorAutoReplySettings_CreatorProfileId",
                table: "CreatorAutoReplySettings",
                column: "CreatorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorFaqs_CreatorProfileId",
                table: "CreatorFaqs",
                column: "CreatorProfileId");


            migrationBuilder.CreateIndex(
                name: "IX_CreatorTerms_CreatorProfileId",
                table: "CreatorTerms",
                column: "CreatorProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatorWorkItems_CreatorProfileId",
                table: "CreatorWorkItems",
                column: "CreatorProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {




            migrationBuilder.DropTable(
                name: "CreatorAssets");

            migrationBuilder.DropTable(
                name: "CreatorAutoReplySettings");

            migrationBuilder.DropTable(
                name: "CreatorFaqs");


            migrationBuilder.DropTable(
                name: "CreatorTerms");

            migrationBuilder.DropTable(
                name: "CreatorWorkItems");




        }
    }
}
