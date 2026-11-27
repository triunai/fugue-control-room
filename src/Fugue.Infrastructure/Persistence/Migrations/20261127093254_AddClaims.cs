using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddClaims : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claims",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    market = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    line = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    claimant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_officer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    policy_number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    incident_date = table.Column<DateOnly>(type: "date", nullable: false),
                    incident_description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    incident_location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reserve_minor = table.Column<long>(type: "bigint", nullable: true),
                    approved_amount_minor = table.Column<long>(type: "bigint", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claims", x => x.id);
                    table.CheckConstraint("ck_claims_approved_positive", "approved_amount_minor IS NULL OR approved_amount_minor > 0");
                    table.CheckConstraint("ck_claims_reserve_positive", "reserve_minor IS NULL OR reserve_minor > 0");
                    table.ForeignKey(
                        name: "fk_claims_users_assigned_officer_id",
                        column: x => x.assigned_officer_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claims_users_claimant_user_id",
                        column: x => x.claimant_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_info_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    response = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_info_requests", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_info_requests_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claim_info_requests_users_requested_by_user_id",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "claim_timeline",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    from_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    to_status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_display = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    note = table.Column<string>(type: "character varying(4100)", maxLength: 4100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_claim_timeline", x => x.id);
                    table.ForeignKey(
                        name: "fk_claim_timeline_claims_claim_id",
                        column: x => x.claim_id,
                        principalTable: "claims",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_claim_timeline_users_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "actions",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "name", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8003-000000000009"), "SUBMIT", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Submit", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8003-000000000010"), "RESPOND", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Respond", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8003-000000000011"), "HANDLE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Handle", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8003-000000000012"), "SETTLE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Settle", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8003-000000000013"), "WORKLOAD", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Workload", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8003-000000000014"), "EXPOSURE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, "Exposure", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" }
                });

            migrationBuilder.InsertData(
                table: "modules",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "description", "is_active", "is_deleted", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8001-000000000006"), "CLAIMS", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, null, true, false, "Claims", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "code", "created_at", "created_by", "description", "is_active", "is_system", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8005-000000000004"), "MANAGER", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, true, true, "Manager", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "resources",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "module_id", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8002-000000000007"), "CLAIMS", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, new Guid("01900000-0000-7000-8001-000000000006"), "Claims", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8004-000000000001"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000002"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000003"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000004"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000005"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000006"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" }
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "action_id", "code", "created_at", "created_by", "description", "resource_id", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8004-000000000015"), new Guid("01900000-0000-7000-8003-000000000009"), "CLAIMS_SUBMIT", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8003-000000000008"), "CLAIMS_READ", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000017"), new Guid("01900000-0000-7000-8003-000000000010"), "CLAIMS_RESPOND", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8003-000000000006"), "CLAIMS_ALL", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8003-000000000011"), "CLAIMS_HANDLE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000020"), new Guid("01900000-0000-7000-8003-000000000012"), "CLAIMS_SETTLE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8003-000000000013"), "CLAIMS_WORKLOAD", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000022"), new Guid("01900000-0000-7000-8003-000000000014"), "CLAIMS_EXPOSURE", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000007"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" }
                });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[,]
                {
                    { new Guid("01900000-0000-7000-8004-000000000015"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000017"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000020"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000022"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000002"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000002"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000002"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000002"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000015"), new Guid("01900000-0000-7000-8005-000000000003"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000003"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000017"), new Guid("01900000-0000-7000-8005-000000000003"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000020"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" },
                    { new Guid("01900000-0000-7000-8004-000000000022"), new Guid("01900000-0000-7000-8005-000000000004"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" }
                });

            migrationBuilder.CreateIndex(
                name: "ix_claim_info_requests_claim",
                table: "claim_info_requests",
                columns: new[] { "claim_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "ix_claim_info_requests_requested_by_user_id",
                table: "claim_info_requests",
                column: "requested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_claim_info_requests_open",
                table: "claim_info_requests",
                column: "claim_id",
                unique: true,
                filter: "responded_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_claim_timeline_actor_user_id",
                table: "claim_timeline",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_claim_timeline_claim",
                table: "claim_timeline",
                columns: new[] { "claim_id", "occurred_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_claims_assigned_officer_id",
                table: "claims",
                column: "assigned_officer_id");

            migrationBuilder.CreateIndex(
                name: "ix_claims_claimant",
                table: "claims",
                columns: new[] { "claimant_user_id", "submitted_at", "id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "ix_claims_exposure",
                table: "claims",
                columns: new[] { "status", "market", "line" });

            migrationBuilder.CreateIndex(
                name: "ix_claims_queue",
                table: "claims",
                columns: new[] { "status", "assigned_officer_id", "submitted_at", "id" });

            migrationBuilder.CreateIndex(
                name: "ux_claims_number",
                table: "claims",
                column: "number",
                unique: true);

            // claim_timeline is append-only (decision D): the same guard as audit_log. A table owner or superuser can still DISABLE the trigger
            // or drop the table; run the API as a non-owner role in production.
            migrationBuilder.Sql(
                """
                CREATE FUNCTION claim_timeline_reject_change() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'claim_timeline is append-only: % is not allowed', TG_OP USING ERRCODE = 'restrict_violation';
                END;
                $$;
                """);
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_claim_timeline_no_update_delete BEFORE UPDATE OR DELETE ON claim_timeline FOR EACH ROW EXECUTE FUNCTION claim_timeline_reject_change();");
            migrationBuilder.Sql(
                "CREATE TRIGGER trg_claim_timeline_no_truncate BEFORE TRUNCATE ON claim_timeline FOR EACH STATEMENT EXECUTE FUNCTION claim_timeline_reject_change();");

            // The claim number's sequence part: nextval is atomic, so two concurrent submissions can never get the same number (gaps on rollback are fine).
            migrationBuilder.Sql("CREATE SEQUENCE claims_number_seq AS bigint START WITH 1 INCREMENT BY 1 NO CYCLE;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claim_info_requests");

            migrationBuilder.DropTable(
                name: "claim_timeline");

            migrationBuilder.DropTable(
                name: "claims");

            migrationBuilder.Sql("DROP FUNCTION claim_timeline_reject_change();");
            migrationBuilder.Sql("DROP SEQUENCE claims_number_seq;");

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000015"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000017"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000020"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000022"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000002") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000015"), new Guid("01900000-0000-7000-8005-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000017"), new Guid("01900000-0000-7000-8005-000000000003") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000001"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000002"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000003"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000004"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000005"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000006"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000016"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000018"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000019"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000020"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000021"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000022"), new Guid("01900000-0000-7000-8005-000000000004") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000015"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000016"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000017"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000018"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000019"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000020"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000021"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000022"));

            migrationBuilder.DeleteData(
                table: "roles",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8005-000000000004"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000009"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000010"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000011"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000012"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000013"));

            migrationBuilder.DeleteData(
                table: "actions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8003-000000000014"));

            migrationBuilder.DeleteData(
                table: "resources",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8002-000000000007"));

            migrationBuilder.DeleteData(
                table: "modules",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8001-000000000006"));
        }
    }
}
