using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ArtCommission.Infrastructure.Persistence;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260922120000_AddCreatorRateCardJson")]
public partial class AddCreatorRateCardJson : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "RateCardJson",
            table: "CreatorProfiles",
            type: "nvarchar(max)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RateCardJson", table: "CreatorProfiles");
    }
}
