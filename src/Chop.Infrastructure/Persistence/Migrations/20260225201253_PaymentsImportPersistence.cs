using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Chop.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentsImportPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bank_imports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    MatchedRows = table.Column<int>(type: "integer", nullable: false),
                    AmbiguousRows = table.Column<int>(type: "integer", nullable: false),
                    InvalidRows = table.Column<int>(type: "integer", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AppliedByUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_imports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImportRowId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaidAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "bank_import_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reference = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PaymentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MatchStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ClientUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ClientDisplayName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CandidateClientIdsJson = table.Column<string>(type: "jsonb", nullable: true),
                    DocType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DocNo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DocDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PayerName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    PayerInn = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PayerAccount = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ReceiverAccount = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    ExtraJson = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bank_import_rows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bank_import_rows_bank_imports_ImportId",
                        column: x => x.ImportId,
                        principalTable: "bank_imports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_ClientUserId",
                table: "bank_import_rows",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_ImportId",
                table: "bank_import_rows",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_bank_import_rows_MatchStatus",
                table: "bank_import_rows",
                column: "MatchStatus");

            migrationBuilder.CreateIndex(
                name: "IX_bank_imports_CreatedAtUtc",
                table: "bank_imports",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_bank_imports_FileHash",
                table: "bank_imports",
                column: "FileHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_ClientUserId",
                table: "payments",
                column: "ClientUserId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ImportId",
                table: "payments",
                column: "ImportId");

            migrationBuilder.CreateIndex(
                name: "IX_payments_ImportRowId",
                table: "payments",
                column: "ImportRowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_PaidAtUtc",
                table: "payments",
                column: "PaidAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bank_import_rows");

            migrationBuilder.DropTable(
                name: "payments");

            migrationBuilder.DropTable(
                name: "bank_imports");
        }
    }
}
