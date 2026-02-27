using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillingTariffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "billing_tariffs",
                columns: table => new
                {
                    Code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Description = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    MonthlyFee = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    Currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_billing_tariffs", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_billing_tariffs_IsActive",
                table: "billing_tariffs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_billing_tariffs_SortOrder",
                table: "billing_tariffs",
                column: "SortOrder");

            migrationBuilder.InsertData(
                table: "billing_tariffs",
                columns: new[] { "Code", "Name", "Description", "MonthlyFee", "Currency", "IsActive", "SortOrder", "UpdatedAtUtc" },
                values: new object[,]
                {
                    { "STANDARD", "Стандарт", "Базовый тариф", 0m, "KZT", true, 10, new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc) },
                    { "PREMIUM", "Премиум", "Расширенный тариф", 0m, "KZT", true, 20, new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc) },
                    { "VIP", "VIP", "Индивидуальный тариф", 0m, "KZT", true, 30, new DateTime(2026, 2, 25, 0, 0, 0, DateTimeKind.Utc) },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "billing_tariffs",
                keyColumn: "Code",
                keyValue: "STANDARD");

            migrationBuilder.DeleteData(
                table: "billing_tariffs",
                keyColumn: "Code",
                keyValue: "PREMIUM");

            migrationBuilder.DeleteData(
                table: "billing_tariffs",
                keyColumn: "Code",
                keyValue: "VIP");

            migrationBuilder.DropTable(
                name: "billing_tariffs");
        }
    }
}
