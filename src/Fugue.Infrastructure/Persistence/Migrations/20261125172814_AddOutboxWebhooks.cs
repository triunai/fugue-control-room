using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxWebhooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    friendly_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    xml = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    aggregate_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    aggregate_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    saga_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    traceparent = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    dispatched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                    table.CheckConstraint("ck_outbox_messages_status", "status IN ('pending', 'dispatched', 'dead')");
                });

            migrationBuilder.CreateTable(
                name: "webhook_subscriptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    event_types = table.Column<string[]>(type: "text[]", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    secret_ciphertext = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_subscriptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "webhook_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subscription_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_status_code = table.Column<int>(type: "integer", nullable: true),
                    last_error = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    duration_ms = table.Column<int>(type: "integer", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dead_lettered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_webhook_deliveries", x => x.id);
                    table.CheckConstraint("ck_webhook_deliveries_status", "status IN ('pending', 'in_flight', 'succeeded', 'dead')");
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_outbox_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "outbox_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_webhook_deliveries_webhook_subscriptions_subscription_id",
                        column: x => x.subscription_id,
                        principalTable: "webhook_subscriptions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "modules",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "description", "is_active", "is_deleted", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8001-000000000005"), "WEBHOOKS", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, null, true, false, "Webhooks", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "resources",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "module_id", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8002-000000000006"), "WEBHOOKS", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, new Guid("01900000-0000-7000-8001-000000000005"), "Webhooks", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "action_id", "code", "created_at", "created_by", "description", "resource_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000014"), new Guid("01900000-0000-7000-8003-000000000007"), "WEBHOOKS_MANAGE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000006"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000014"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.CreateIndex(
                name: "ux_data_protection_keys_friendly_name",
                table: "data_protection_keys",
                column: "friendly_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_aggregate",
                table: "outbox_messages",
                columns: new[] { "aggregate_type", "aggregate_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_pending",
                table: "outbox_messages",
                columns: new[] { "next_attempt_at", "id" },
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_due",
                table: "webhook_deliveries",
                columns: new[] { "next_attempt_at", "id" },
                filter: "status IN ('pending', 'in_flight')");

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_occurred_at_id",
                table: "webhook_deliveries",
                columns: new[] { "occurred_at", "id" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_deliveries_subscription",
                table: "webhook_deliveries",
                columns: new[] { "subscription_id", "occurred_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ux_webhook_deliveries_message_subscription",
                table: "webhook_deliveries",
                columns: new[] { "message_id", "subscription_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_webhook_subscriptions_created_at_id",
                table: "webhook_subscriptions",
                columns: new[] { "created_at", "id" });

            // Payload immutability (ADR-011, D-037): status, attempts, next_attempt_at, last_error and dispatched_at move; every other column is frozen.
            // DELETE stays legal (retention). The owner or a superuser can still disable the trigger (same limit as audit_log).
            migrationBuilder.Sql(
                """
                CREATE FUNCTION outbox_messages_reject_immutable_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW.id IS DISTINCT FROM OLD.id
                       OR NEW.occurred_at IS DISTINCT FROM OLD.occurred_at
                       OR NEW.event_type IS DISTINCT FROM OLD.event_type
                       OR NEW.aggregate_type IS DISTINCT FROM OLD.aggregate_type
                       OR NEW.aggregate_id IS DISTINCT FROM OLD.aggregate_id
                       OR NEW.payload IS DISTINCT FROM OLD.payload
                       OR NEW.correlation_id IS DISTINCT FROM OLD.correlation_id
                       OR NEW.saga_id IS DISTINCT FROM OLD.saga_id
                       OR NEW.traceparent IS DISTINCT FROM OLD.traceparent THEN
                        RAISE EXCEPTION 'outbox_messages: the event (payload, type, aggregate, correlation) is immutable' USING ERRCODE = 'restrict_violation';
                    END IF;
                    RETURN NEW;
                END;
                $$;
                """);
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_outbox_messages_immutable BEFORE UPDATE ON outbox_messages FOR EACH ROW EXECUTE FUNCTION outbox_messages_reject_immutable_change();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "webhook_deliveries");

            migrationBuilder.DropTable(
                name: "outbox_messages");

            migrationBuilder.DropTable(
                name: "webhook_subscriptions");

            migrationBuilder.Sql("DROP FUNCTION outbox_messages_reject_immutable_change();");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000014"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000014"));

            migrationBuilder.DeleteData(
                table: "resources",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8002-000000000006"));

            migrationBuilder.DeleteData(
                table: "modules",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8001-000000000005"));
        }
    }
}
