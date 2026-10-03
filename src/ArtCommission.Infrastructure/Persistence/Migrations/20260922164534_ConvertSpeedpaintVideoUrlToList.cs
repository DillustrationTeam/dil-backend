using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSpeedpaintVideoUrlToList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpeedpaintVideoUrls",
                table: "CreatorApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            // Chuyển dữ liệu cũ (1 URL dạng string) sang mảng JSON để không mất dữ liệu đã nộp trước đó.
            migrationBuilder.Sql(@"
                UPDATE CreatorApplications
                SET SpeedpaintVideoUrls = CASE
                    WHEN SpeedpaintVideoUrl IS NULL OR LTRIM(RTRIM(SpeedpaintVideoUrl)) = '' THEN '[]'
                    ELSE '[""' + STRING_ESCAPE(SpeedpaintVideoUrl, 'json') + '""]'
                END
            ");

            migrationBuilder.DropColumn(
                name: "SpeedpaintVideoUrl",
                table: "CreatorApplications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SpeedpaintVideoUrl",
                table: "CreatorApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Khôi phục lại link đầu tiên trong mảng JSON (nếu có) — các link phụ (nếu >1) sẽ bị mất khi downgrade.
            migrationBuilder.Sql(@"
                UPDATE CreatorApplications
                SET SpeedpaintVideoUrl = JSON_VALUE(SpeedpaintVideoUrls, '$[0]')
                WHERE ISJSON(SpeedpaintVideoUrls) = 1
            ");

            migrationBuilder.DropColumn(
                name: "SpeedpaintVideoUrls",
                table: "CreatorApplications");
        }
    }
}
