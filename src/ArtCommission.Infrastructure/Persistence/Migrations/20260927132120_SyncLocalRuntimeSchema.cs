using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ArtCommission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SyncLocalRuntimeSchema : Migration
    {
        /// <inheritdoc />
        // Superseded by AddCreatorWorkstationTables. Keep this migration ID for
        // databases that already applied the original local schema repair.
        protected override void Up(MigrationBuilder migrationBuilder) { }

        // Shared workstation tables may contain user data from either branch.
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
