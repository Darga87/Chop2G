using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuardLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guard_locations",
                columns: table => new
                {
                    GuardUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Latitude = table.Column<double>(type: "double precision", nullable: false),
                    Longitude = table.Column<double>(type: "double precision", nullable: false),
                    AccuracyMeters = table.Column<double>(type: "double precision", nullable: true),
                    DeviceTimeUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ShiftId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_locations", x => x.GuardUserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guard_locations_IncidentId",
                table: "guard_locations",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_guard_locations_UpdatedAtUtc",
                table: "guard_locations",
                column: "UpdatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guard_locations");
        }
    }
}
