using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claim_imports",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    item = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    imported_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_imports", x => x.key);
                    table.CheckConstraint("ck_claim_imports_key_sha256", "key ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_claim_imports_source", "source IN ('Spreadsheet', 'Email', 'WhatsApp')");
                    table.ForeignKey(
                        name: "fk_claim_imports_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claim_imports_users_imported_by",
                        column: x => x.imported_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_imports_created_at",
                table: "claim_imports",
                columns: new[] { "created_at", "key" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_claim_imports_imported_by",
                table: "claim_imports",
                column: "imported_by");

            migrationBuilder.CreateIndex(
                name: "ux_claim_imports_claim",
                table: "claim_imports",
                column: "claim_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_imports");
        }
    }
}
