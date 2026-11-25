using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    permission_used = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    outcome = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    error_code = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    changes = table.Column<string>(type: "jsonb", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    saga_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    http_method = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    route_template = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                    table.CheckConstraint("ck_audit_log_actor_type", "actor_type IN ('user', 'system', 'service')");
                    table.CheckConstraint("ck_audit_log_outcome", "outcome IN ('success', 'failure')");
                });

            migrationBuilder.InsertData(
                table: "actions",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8003-000000000008"), "READ", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Read", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "modules",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "description", "is_active", "is_deleted", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8001-000000000004"), "AUDIT", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, null, true, false, "Audit", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "resources",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "module_id", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8002-000000000005"), "AUDIT", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, new Guid("01900000-0000-7000-8001-000000000004"), "Audit log", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "action_id", "code", "created_at", "created_by", "description", "resource_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000013"), new Guid("01900000-0000-7000-8003-000000000008"), "AUDIT_READ", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000005"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000013"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_action",
                table: "audit_log",
                columns: new[] { "action", "occurred_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_actor",
                table: "audit_log",
                columns: new[] { "actor_user_id", "occurred_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_correlation_id",
                table: "audit_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity",
                table: "audit_log",
                columns: new[] { "entity_type", "entity_id", "occurred_at", "id" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at_id",
                table: "audit_log",
                columns: new[] { "occurred_at", "id" },
                descending: new bool[0]);

            // Append-only (ADR-010): rejects UPDATE and DELETE per row and TRUNCATE per statement, for every role. The table owner
            // or a superuser can still DISABLE the trigger or drop the table; run the API as a non-owner role in production.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION audit_log_reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'audit_log is append-only: % is not allowed', TG_OP USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_audit_log_no_update_delete BEFORE UPDATE OR DELETE ON audit_log FOR EACH ROW EXECUTE FUNCTION audit_log_reject_change();");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_audit_log_no_truncate BEFORE TRUNCATE ON audit_log FOR EACH STATEMENT EXECUTE FUNCTION audit_log_reject_change();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log"); // drops its triggers with it

            migrationBuilder.Sql("DROP FUNCTION audit_log_reject_change();");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000013"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000013"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000008"));

            migrationBuilder.DeleteData(
                table: "resources",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8002-000000000005"));

            migrationBuilder.DeleteData(
                table: "modules",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8001-000000000004"));
        }
    }
}
