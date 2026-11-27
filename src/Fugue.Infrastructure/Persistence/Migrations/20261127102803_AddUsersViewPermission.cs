using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fugue.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUsersViewPermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "resources",
                columns: new[] { "id", "code", "created_at", "created_by", "deleted_at", "deleted_by", "is_deleted", "module_id", "name", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8002-000000000008"), "USERS", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, null, false, new Guid("01900000-0000-7000-8001-000000000003"), "Users", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "id", "action_id", "code", "created_at", "created_by", "description", "resource_id", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000023"), new Guid("01900000-0000-7000-8003-000000000001"), "USERS_VIEW", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", null, new Guid("01900000-0000-7000-8002-000000000008"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });

            migrationBuilder.InsertData(
                table: "role_permissions",
                columns: new[] { "permission_id", "role_id", "created_at", "created_by", "updated_at", "updated_by" },
                values: new object[] { new Guid("01900000-0000-7000-8004-000000000023"), new Guid("01900000-0000-7000-8005-000000000001"), new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration", new DateTimeOffset(new DateTime(2026, 11, 25, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "migration" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "role_permissions",
                keyColumns: new[] { "permission_id", "role_id" },
                keyValues: new object[] { new Guid("01900000-0000-7000-8004-000000000023"), new Guid("01900000-0000-7000-8005-000000000001") });

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8004-000000000023"));

            migrationBuilder.DeleteData(
                table: "resources",
                keyColumn: "id",
                keyValue: new Guid("01900000-0000-7000-8002-000000000008"));
        }
    }
}
