using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPurposeToEmailVerificationCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Toàn bộ mã hiện có trong bảng đều là mã xác minh email trước đăng ký (chưa có
            // luồng quên mật khẩu trước đây), nên backfill mặc định là EmailVerification.
            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "EmailVerificationCodes",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "EmailVerification");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EmailVerificationCodes");
        }
    }
}
