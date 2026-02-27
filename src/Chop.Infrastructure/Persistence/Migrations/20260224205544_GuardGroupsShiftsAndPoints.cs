using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GuardGroupsShiftsAndPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guard_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_groups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_points",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    GeoPoint = table.Column<Point>(type: "geography (point,4326)", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_points", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "guard_group_members",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IsCommander = table.Column<bool>(type: "boolean", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_group_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guard_group_members_guard_groups_GuardGroupId",
                        column: x => x.GuardGroupId,
                        principalTable: "guard_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guard_shifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GuardGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SecurityPointId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guard_shifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_guard_shifts_guard_groups_GuardGroupId",
                        column: x => x.GuardGroupId,
                        principalTable: "guard_groups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_guard_shifts_security_points_SecurityPointId",
                        column: x => x.SecurityPointId,
                        principalTable: "security_points",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guard_group_members_GuardGroupId_GuardUserId",
                table: "guard_group_members",
                columns: new[] { "GuardGroupId", "GuardUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guard_groups_Name",
                table: "guard_groups",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_guard_shifts_EndedAtUtc",
                table: "guard_shifts",
                column: "EndedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_guard_shifts_GuardGroupId",
                table: "guard_shifts",
                column: "GuardGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_guard_shifts_GuardUserId",
                table: "guard_shifts",
                column: "GuardUserId");

            migrationBuilder.CreateIndex(
                name: "IX_guard_shifts_SecurityPointId",
                table: "guard_shifts",
                column: "SecurityPointId");

            migrationBuilder.CreateIndex(
                name: "IX_guard_shifts_StartedAtUtc",
                table: "guard_shifts",
                column: "StartedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_security_points_Code",
                table: "security_points",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_security_points_GeoPoint",
                table: "security_points",
                column: "GeoPoint");

            migrationBuilder.CreateIndex(
                name: "IX_security_points_Type",
                table: "security_points",
                column: "Type");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guard_group_members");

            migrationBuilder.DropTable(
                name: "guard_shifts");

            migrationBuilder.DropTable(
                name: "guard_groups");

            migrationBuilder.DropTable(
                name: "security_points");
        }
    }
}
