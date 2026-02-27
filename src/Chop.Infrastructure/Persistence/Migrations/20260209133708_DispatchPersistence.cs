using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DispatchPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispatches_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "incident_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuardUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    PatrolUnitId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_incident_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_incident_assignments_incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalTable: "incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "dispatch_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DispatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    RecipientId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DistanceMeters = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AcceptedBy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AcceptedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedVia = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispatch_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_dispatch_recipients_dispatches_DispatchId",
                        column: x => x.DispatchId,
                        principalTable: "dispatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dispatch_recipients_DispatchId_RecipientType_RecipientId",
                table: "dispatch_recipients",
                columns: new[] { "DispatchId", "RecipientType", "RecipientId" });

            migrationBuilder.CreateIndex(
                name: "IX_dispatches_CreatedAtUtc",
                table: "dispatches",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_dispatches_IncidentId",
                table: "dispatches",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_assignments_GuardUserId",
                table: "incident_assignments",
                column: "GuardUserId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_assignments_IncidentId",
                table: "incident_assignments",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_incident_assignments_PatrolUnitId",
                table: "incident_assignments",
                column: "PatrolUnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dispatch_recipients");

            migrationBuilder.DropTable(
                name: "incident_assignments");

            migrationBuilder.DropTable(
                name: "dispatches");
        }
    }
}
