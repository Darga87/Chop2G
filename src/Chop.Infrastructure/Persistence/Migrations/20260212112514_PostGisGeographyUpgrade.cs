using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PostGisGeographyUpgrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.AddColumn<object>(
                name: "GeoPoint",
                table: "incidents",
                type: "geography (point,4326)",
                nullable: true);

            migrationBuilder.AddColumn<object>(
                name: "GeoPoint",
                table: "client_addresses",
                type: "geography (point,4326)",
                nullable: true);

            migrationBuilder.AddColumn<object>(
                name: "GeoPoint",
                table: "guard_locations",
                type: "geography (point,4326)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE incidents
                SET "GeoPoint" = ST_SetSRID(ST_MakePoint("Longitude", "Latitude"), 4326)::geography
                WHERE "Longitude" IS NOT NULL AND "Latitude" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE client_addresses
                SET "GeoPoint" = ST_SetSRID(ST_MakePoint("Longitude", "Latitude"), 4326)::geography
                WHERE "Longitude" IS NOT NULL AND "Latitude" IS NOT NULL;
                """);

            migrationBuilder.Sql("""
                UPDATE guard_locations
                SET "GeoPoint" = ST_SetSRID(ST_MakePoint("Longitude", "Latitude"), 4326)::geography;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_incidents_GeoPoint",
                table: "incidents",
                column: "GeoPoint")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_client_addresses_GeoPoint",
                table: "client_addresses",
                column: "GeoPoint")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "IX_guard_locations_GeoPoint",
                table: "guard_locations",
                column: "GeoPoint")
                .Annotation("Npgsql:IndexMethod", "GIST");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incidents_GeoPoint",
                table: "incidents");

            migrationBuilder.DropIndex(
                name: "IX_client_addresses_GeoPoint",
                table: "client_addresses");

            migrationBuilder.DropIndex(
                name: "IX_guard_locations_GeoPoint",
                table: "guard_locations");

            migrationBuilder.DropColumn(
                name: "GeoPoint",
                table: "incidents");

            migrationBuilder.DropColumn(
                name: "GeoPoint",
                table: "client_addresses");

            migrationBuilder.DropColumn(
                name: "GeoPoint",
                table: "guard_locations");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:postgis", ",,");
        }
    }
}
