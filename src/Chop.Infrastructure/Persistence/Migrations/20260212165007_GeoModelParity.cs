using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeoModelParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Snapshot parity migration:
            // physical schema was introduced by PostGisGeographyUpgrade.
            // This migration only aligns model snapshot for runtime auto-migrate.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op (see Up).
        }
    }
}
