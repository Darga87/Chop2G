using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClientProfileBillingAndAdminClientUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BillingStatus",
                table: "client_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "HasDebt",
                table: "client_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPaymentAtUtc",
                table: "client_profiles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Tariff",
                table: "client_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BillingStatus",
                table: "client_profiles");

            migrationBuilder.DropColumn(
                name: "HasDebt",
                table: "client_profiles");

            migrationBuilder.DropColumn(
                name: "LastPaymentAtUtc",
                table: "client_profiles");

            migrationBuilder.DropColumn(
                name: "Tariff",
                table: "client_profiles");
        }
    }
}
