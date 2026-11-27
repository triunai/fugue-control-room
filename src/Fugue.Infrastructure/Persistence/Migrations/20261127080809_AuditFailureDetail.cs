using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditFailureDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_ms",
                table: "audit_log",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_message",
                table: "audit_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "error_type",
                table: "audit_log",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "duration_ms",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "error_message",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "error_type",
                table: "audit_log");
        }
    }
}
