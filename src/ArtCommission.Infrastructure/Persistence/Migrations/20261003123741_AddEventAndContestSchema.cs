using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEventAndContestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlatformEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BannerUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Rules = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Prize = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    MaxVote = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Draft"),
                    SubmissionStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmissionEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    JudgingStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    JudgingEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VotingStartAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    VotingEndAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ResultAnnouncementAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedByAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformEvents", x => x.Id);
                    table.CheckConstraint("CK_PlatformEvents_Timeline", "[SubmissionStartAt] < [SubmissionEndAt] AND [SubmissionEndAt] < [JudgingStartAt] AND [JudgingStartAt] < [JudgingEndAt] AND [JudgingEndAt] < [VotingStartAt] AND [VotingStartAt] < [VotingEndAt] AND [VotingEndAt] < [ResultAnnouncementAt]");
                    table.ForeignKey(
                        name: "FK_PlatformEvents_Users_CreatedByAdminId",
                        column: x => x.CreatedByAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventCriteria",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MaxScore = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false, defaultValue: 10.00m),
                    Weight = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventCriteria_PlatformEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PlatformEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentFromAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SentToCreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsHeadJury = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    RespondedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_CreatorProfiles_SentToCreatorId",
                        column: x => x.SentToCreatorId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Invitations_PlatformEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PlatformEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_Users_SentFromAdminId",
                        column: x => x.SentFromAdminId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Juries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsHeadJury = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Juries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Juries_CreatorProfiles_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "CreatorProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Juries_PlatformEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PlatformEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EventSubmissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmitterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ArtworkId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    AiScanPassed = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SubmittedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventSubmissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EventSubmissions_Artworks_ArtworkId",
                        column: x => x.ArtworkId,
                        principalTable: "Artworks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EventSubmissions_PlatformEvents_EventId",
                        column: x => x.EventId,
                        principalTable: "PlatformEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventSubmissions_Users_SubmitterId",
                        column: x => x.SubmitterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CriteriaScores",
                columns: table => new
                {
                    EventCriteriaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GradedByJuryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Score = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriteriaScores", x => new { x.EventCriteriaId, x.SubmissionId, x.GradedByJuryId });
                    table.ForeignKey(
                        name: "FK_CriteriaScores_EventCriteria_EventCriteriaId",
                        column: x => x.EventCriteriaId,
                        principalTable: "EventCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CriteriaScores_EventSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "EventSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CriteriaScores_Juries_GradedByJuryId",
                        column: x => x.GradedByJuryId,
                        principalTable: "Juries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EventVotes",
                columns: table => new
                {
                    SubmissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VoterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VotedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EventVotes", x => new { x.SubmissionId, x.VoterId });
                    table.ForeignKey(
                        name: "FK_EventVotes_EventSubmissions_SubmissionId",
                        column: x => x.SubmissionId,
                        principalTable: "EventSubmissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EventVotes_Users_VoterId",
                        column: x => x.VoterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CriteriaScores_GradedByJuryId",
                table: "CriteriaScores",
                column: "GradedByJuryId");

            migrationBuilder.CreateIndex(
                name: "IX_CriteriaScores_SubmissionId",
                table: "CriteriaScores",
                column: "SubmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_EventCriteria_EventId",
                table: "EventCriteria",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSubmissions_ArtworkId",
                table: "EventSubmissions",
                column: "ArtworkId");

            migrationBuilder.CreateIndex(
                name: "IX_EventSubmissions_EventId_Score_VoteCount",
                table: "EventSubmissions",
                columns: new[] { "EventId", "Score", "VoteCount" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSubmissions_EventId_SubmitterId",
                table: "EventSubmissions",
                columns: new[] { "EventId", "SubmitterId" });

            migrationBuilder.CreateIndex(
                name: "IX_EventSubmissions_SubmitterId",
                table: "EventSubmissions",
                column: "SubmitterId");

            migrationBuilder.CreateIndex(
                name: "IX_EventVotes_VoterId",
                table: "EventVotes",
                column: "VoterId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_EventId_SentToCreatorId",
                table: "Invitations",
                columns: new[] { "EventId", "SentToCreatorId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_EventId_Status",
                table: "Invitations",
                columns: new[] { "EventId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SentFromAdminId",
                table: "Invitations",
                column: "SentFromAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_SentToCreatorId_Status",
                table: "Invitations",
                columns: new[] { "SentToCreatorId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Juries_CreatorId",
                table: "Juries",
                column: "CreatorId");

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

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEvents_CreatedAt",
                table: "PlatformEvents",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEvents_CreatedByAdminId",
                table: "PlatformEvents",
                column: "CreatedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_PlatformEvents_Status_SubmissionStartAt_SubmissionEndAt",
                table: "PlatformEvents",
                columns: new[] { "Status", "SubmissionStartAt", "SubmissionEndAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CriteriaScores");

            migrationBuilder.DropTable(
                name: "EventVotes");

            migrationBuilder.DropTable(
                name: "EventCriteria");

            migrationBuilder.DropTable(
                name: "EventSubmissions");

            migrationBuilder.DropTable(
                name: "Juries");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "PlatformEvents");
        }
    }
}
