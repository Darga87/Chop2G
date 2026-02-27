using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlertEventsStatusAndWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AckedAtUtc",
                table: "alert_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AckedByUserId",
                table: "alert_events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAtUtc",
                table: "alert_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "alert_events",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "alert_events",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Resolved");

            // Backfill existing rows (created before Status column existed).
            migrationBuilder.Sql("UPDATE alert_events SET \"Status\" = 'Resolved' WHERE \"Status\" = '' OR \"Status\" IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_alert_events_Status_CreatedAtUtc",
                table: "alert_events",
                columns: new[] { "Status", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_alert_events_Status_CreatedAtUtc",
                table: "alert_events");

            migrationBuilder.DropColumn(
                name: "AckedAtUtc",
                table: "alert_events");

            migrationBuilder.DropColumn(
                name: "AckedByUserId",
                table: "alert_events");

            migrationBuilder.DropColumn(
                name: "ResolvedAtUtc",
                table: "alert_events");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "alert_events");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "alert_events");
        }
    }
}
