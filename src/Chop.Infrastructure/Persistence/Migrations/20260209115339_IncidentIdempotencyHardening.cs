using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncidentIdempotencyHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAtUtc",
                table: "incident_idempotency",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "incident_idempotency",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_incident_idempotency_ExpiresAtUtc",
                table: "incident_idempotency",
                column: "ExpiresAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_incident_idempotency_ExpiresAtUtc",
                table: "incident_idempotency");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "incident_idempotency");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "incident_idempotency");
        }
    }
}
