using ArtCommission.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260930103000_AddCommissionLicensePricing")]
public class AddCommissionLicensePricing : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "LicenseType",
            table: "Commissions",
            type: "nvarchar(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "Personal");

        migrationBuilder.AddColumn<decimal>(
            name: "LicenseMultiplierApplied",
            table: "Commissions",
            type: "decimal(5,2)",
            precision: 5,
            scale: 2,
            nullable: false,
            defaultValue: 1m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn("LicenseType", "Commissions");
        migrationBuilder.DropColumn("LicenseMultiplierApplied", "Commissions");
    }
}
