using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IncidentDetailsClientData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_profiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FullName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_profiles", x => x.Id);
                    table.UniqueConstraint("AK_client_profiles_UserId", x => x.UserId);
                });

            migrationBuilder.CreateTable(
                name: "client_addresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AddressText = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_addresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_addresses_client_profiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "client_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "client_phones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_client_phones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_client_phones_client_profiles_ClientProfileId",
                        column: x => x.ClientProfileId,
                        principalTable: "client_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_incidents_ClientUserId",
                table: "incidents",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_client_addresses_ClientProfileId",
                table: "client_addresses",
                column: "ClientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_client_phones_ClientProfileId",
                table: "client_phones",
                column: "ClientProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_client_profiles_UserId",
                table: "client_profiles",
                column: "UserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_incidents_client_profiles_ClientUserId",
                table: "incidents",
                column: "ClientUserId",
                principalTable: "client_profiles",
                principalColumn: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_incidents_client_profiles_ClientUserId",
                table: "incidents");

            migrationBuilder.DropTable(
                name: "client_addresses");

            migrationBuilder.DropTable(
                name: "client_phones");

            migrationBuilder.DropTable(
                name: "client_profiles");

            migrationBuilder.DropIndex(
                name: "IX_incidents_ClientUserId",
                table: "incidents");
        }
    }
}
